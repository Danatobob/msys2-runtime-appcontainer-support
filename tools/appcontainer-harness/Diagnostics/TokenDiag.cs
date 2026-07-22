using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace AppContainerHarness.Diagnostics;

// Round-3 investigation tool (per user request): dump TokenGroups AND TokenCapabilities
// (distinct TOKEN_INFORMATION_CLASS values -- round 2 only checked TokenGroups) for a process
// token, and inspect \Sessions' real security descriptor via raw Nt* calls.
//
// Uses manual DllImport declarations throughout (not CsWin32's PInvoke.* friendly overloads,
// except for the two calls -- ConvertSidToStringSid, LocalFree -- already proven working
// elsewhere in this project) to avoid the friendly-vs-raw-overload ambiguity bug documented in
// Unit 2's build summary (mixing argument styles resolves to the wrong overload silently).
internal static class TokenDiag
{
    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenGroupsClass = 2;
    private const int TokenRestrictedSidsClass = 11;
    private const int TokenCapabilitiesClass = 30; // TOKEN_INFORMATION_CLASS enum, Win 8+ headers
    private const int TokenAppContainerSidClass = 31;

    [StructLayout(LayoutKind.Sequential)]
    private struct SID_AND_ATTRIBUTES_RAW
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, uint tokenInformationLength, out uint returnLength);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool LookupAccountSidW(string? systemName, IntPtr sid, StringBuilder name, ref uint cchName, StringBuilder referencedDomainName, ref uint cchReferencedDomainName, out int use);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr stringSid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public static unsafe void DumpProcessToken(IntPtr hProcess, string label)
    {
        if (!OpenProcessToken(hProcess, TOKEN_QUERY, out IntPtr token))
        {
            Console.WriteLine($"[{label}] OpenProcessToken failed: {Marshal.GetLastWin32Error()}");
            return;
        }
        try
        {
            DumpClass(token, TokenGroupsClass, $"{label} TokenGroups");
            DumpClass(token, TokenRestrictedSidsClass, $"{label} TokenRestrictedSids");
            DumpClass(token, TokenCapabilitiesClass, $"{label} TokenCapabilities");
            DumpAppContainerSid(token, $"{label} TokenAppContainerSid");
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static void DumpClass(IntPtr token, int infoClass, string label)
    {
        GetTokenInformation(token, infoClass, IntPtr.Zero, 0, out uint len);
        if (len == 0)
        {
            Console.WriteLine($"[{label}] size query returned 0 (error {Marshal.GetLastWin32Error()}) -- likely not present/empty on this token.");
            return;
        }

        IntPtr buf = Marshal.AllocHGlobal((int)len);
        try
        {
            if (!GetTokenInformation(token, infoClass, buf, len, out _))
            {
                Console.WriteLine($"[{label}] GetTokenInformation failed: {Marshal.GetLastWin32Error()}");
                return;
            }

            // Native layout: DWORD GroupCount; SID_AND_ATTRIBUTES Groups[ANYSIZE_ARRAY];
            // (identical for TokenGroups and TokenCapabilities -- same struct shape per MSDN).
            // On x64, the array is 8-byte aligned, so there are 4 bytes of padding after the
            // 4-byte GroupCount before the array starts.
            uint count = (uint)Marshal.ReadInt32(buf);
            Console.WriteLine($"[{label}] count={count}");
            IntPtr arrayStart = IntPtr.Add(buf, 8);
            int entrySize = Marshal.SizeOf<SID_AND_ATTRIBUTES_RAW>();
            for (int i = 0; i < count; i++)
            {
                var saa = Marshal.PtrToStructure<SID_AND_ATTRIBUTES_RAW>(IntPtr.Add(arrayStart, i * entrySize));

                string sidStr = "?";
                if (ConvertSidToStringSidW(saa.Sid, out IntPtr sidStrPtr))
                {
                    sidStr = Marshal.PtrToStringUni(sidStrPtr) ?? "?";
                    LocalFree(sidStrPtr);
                }

                string friendly = "(no friendly name)";
                var name = new StringBuilder(256);
                var domain = new StringBuilder(256);
                uint cchName = 256, cchDomain = 256;
                if (LookupAccountSidW(null, saa.Sid, name, ref cchName, domain, ref cchDomain, out _))
                {
                    friendly = domain.Length == 0 ? name.ToString() : $"{domain}\\{name}";
                }

                Console.WriteLine($"  [{i}] {sidStr}  attrs=0x{saa.Attributes:X}  {friendly}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    // TokenAppContainerSid (info class 31) returns a single-field struct: { PSID TokenAppContainer; }
    // -- NULL if the token is not an AppContainer token at all.
    private static void DumpAppContainerSid(IntPtr token, string label)
    {
        GetTokenInformation(token, TokenAppContainerSidClass, IntPtr.Zero, 0, out uint len);
        if (len == 0)
        {
            Console.WriteLine($"[{label}] size query returned 0 (error {Marshal.GetLastWin32Error()})");
            return;
        }
        IntPtr buf = Marshal.AllocHGlobal((int)len);
        try
        {
            if (!GetTokenInformation(token, TokenAppContainerSidClass, buf, len, out _))
            {
                Console.WriteLine($"[{label}] GetTokenInformation failed: {Marshal.GetLastWin32Error()}");
                return;
            }
            IntPtr sidPtr = Marshal.ReadIntPtr(buf);
            if (sidPtr == IntPtr.Zero)
            {
                Console.WriteLine($"[{label}] NULL (token is not an AppContainer token per this info class)");
                return;
            }
            string sidStr = "?";
            if (ConvertSidToStringSidW(sidPtr, out IntPtr sidStrPtr))
            {
                sidStr = Marshal.PtrToStringUni(sidStrPtr) ?? "?";
                LocalFree(sidStrPtr);
            }
            Console.WriteLine($"[{label}] {sidStr}");
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    // --- Raw Nt* calls for \Sessions security-descriptor inspection (no CsWin32 coverage) ---

    [StructLayout(LayoutKind.Sequential)]
    private struct UNICODE_STRING_RAW
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OBJECT_ATTRIBUTES_RAW
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    private const uint DIRECTORY_QUERY = 0x0001;
    private const uint DIRECTORY_TRAVERSE = 0x0002;
    private const uint READ_CONTROL = 0x00020000;
    private const uint ACCESS_SYSTEM_SECURITY = 0x01000000;
    private const uint OWNER_SECURITY_INFORMATION = 0x1;
    private const uint GROUP_SECURITY_INFORMATION = 0x2;
    private const uint DACL_SECURITY_INFORMATION = 0x4;
    private const uint SACL_SECURITY_INFORMATION = 0x8;
    private const uint LABEL_SECURITY_INFORMATION = 0x10;

    [DllImport("ntdll.dll")]
    private static extern int NtOpenDirectoryObject(out IntPtr handle, uint desiredAccess, ref OBJECT_ATTRIBUTES_RAW objectAttributes);

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySecurityObject(IntPtr handle, uint securityInformation, IntPtr securityDescriptor, uint length, out uint lengthNeeded);

    [DllImport("ntdll.dll")]
    private static extern int NtClose(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ConvertSecurityDescriptorToStringSecurityDescriptorW(
        IntPtr securityDescriptor, uint requestedRevision, uint securityInformation, out IntPtr stringSecurityDescriptor, out uint stringSecurityDescriptorLen);

    public static void DumpObjectSecurity(string path, string label)
    {
        IntPtr nameBuf = Marshal.StringToHGlobalUni(path);
        try
        {
            var uname = new UNICODE_STRING_RAW
            {
                Length = (ushort)(path.Length * 2),
                MaximumLength = (ushort)((path.Length + 1) * 2),
                Buffer = nameBuf,
            };
            IntPtr unamePtr = Marshal.AllocHGlobal(Marshal.SizeOf<UNICODE_STRING_RAW>());
            try
            {
                Marshal.StructureToPtr(uname, unamePtr, false);
                var attr = new OBJECT_ATTRIBUTES_RAW
                {
                    Length = Marshal.SizeOf<OBJECT_ATTRIBUTES_RAW>(),
                    RootDirectory = IntPtr.Zero,
                    ObjectName = unamePtr,
                    Attributes = 0,
                    SecurityDescriptor = IntPtr.Zero,
                    SecurityQualityOfService = IntPtr.Zero,
                };

                int status = NtOpenDirectoryObject(out IntPtr handle,
                    DIRECTORY_QUERY | DIRECTORY_TRAVERSE | READ_CONTROL | ACCESS_SYSTEM_SECURITY, ref attr);
                bool withSacl = status >= 0;
                if (status < 0)
                {
                    status = NtOpenDirectoryObject(out handle, DIRECTORY_QUERY | DIRECTORY_TRAVERSE | READ_CONTROL, ref attr);
                    if (status < 0)
                    {
                        Console.WriteLine($"[{label}] NtOpenDirectoryObject('{path}') failed: NTSTATUS 0x{status:X8}");
                        return;
                    }
                }

                try
                {
                    uint info = OWNER_SECURITY_INFORMATION | GROUP_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION;
                    if (withSacl) info |= SACL_SECURITY_INFORMATION | LABEL_SECURITY_INFORMATION;

                    NtQuerySecurityObject(handle, info, IntPtr.Zero, 0, out uint needed);
                    if (needed == 0)
                    {
                        Console.WriteLine($"[{label}] NtQuerySecurityObject size query returned 0.");
                        return;
                    }
                    IntPtr sdBuf = Marshal.AllocHGlobal((int)needed);
                    try
                    {
                        int qstatus = NtQuerySecurityObject(handle, info, sdBuf, needed, out _);
                        if (qstatus < 0)
                        {
                            Console.WriteLine($"[{label}] NtQuerySecurityObject('{path}') failed: NTSTATUS 0x{qstatus:X8} (withSacl={withSacl})");
                            return;
                        }
                        if (ConvertSecurityDescriptorToStringSecurityDescriptorW(sdBuf, 1, info, out IntPtr sddlPtr, out _))
                        {
                            string sddl = Marshal.PtrToStringUni(sddlPtr) ?? "?";
                            LocalFree(sddlPtr);
                            Console.WriteLine($"[{label}] '{path}' (withSacl={withSacl}) SDDL: {sddl}");
                        }
                        else
                        {
                            Console.WriteLine($"[{label}] ConvertSecurityDescriptorToStringSecurityDescriptorW failed: {Marshal.GetLastWin32Error()}");
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(sdBuf);
                    }
                }
                finally
                {
                    NtClose(handle);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(unamePtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(nameBuf);
        }
    }

    // --- Round 5: non-interactive, no-GUI-tool location of a named object (per coordinator
    // direction, after Procmon/API-Monitor-style interactive tracing proved unusable in this
    // headless environment) ---

    private const uint MAXIMUM_ALLOWED = 0x02000000;
    private const int STATUS_OBJECT_NAME_NOT_FOUND = unchecked((int)0xC0000034);
    private const int STATUS_ACCESS_DENIED = unchecked((int)0xC0000022);

    [DllImport("ntdll.dll")]
    private static extern int NtOpenEvent(out IntPtr handle, uint desiredAccess, ref OBJECT_ATTRIBUTES_RAW objectAttributes);

    [StructLayout(LayoutKind.Sequential)]
    private struct OBJECT_DIRECTORY_INFORMATION_RAW
    {
        public UNICODE_STRING_RAW Name;
        public UNICODE_STRING_RAW TypeName;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryDirectoryObject(IntPtr directoryHandle, IntPtr buffer, uint length, [MarshalAs(UnmanagedType.U1)] bool returnSingleEntry, [MarshalAs(UnmanagedType.U1)] bool restartScan, ref uint context, out uint returnLength);

    // Attempts to directly open a fully-qualified NT object path (e.g. an event) and reports
    // whether it exists (and is accessible), doesn't exist, or exists-but-denied.
    public static void TryLocateNamedObject(string fullPath, string label)
    {
        IntPtr nameBuf = Marshal.StringToHGlobalUni(fullPath);
        try
        {
            var uname = new UNICODE_STRING_RAW
            {
                Length = (ushort)(fullPath.Length * 2),
                MaximumLength = (ushort)((fullPath.Length + 1) * 2),
                Buffer = nameBuf,
            };
            IntPtr unamePtr = Marshal.AllocHGlobal(Marshal.SizeOf<UNICODE_STRING_RAW>());
            try
            {
                Marshal.StructureToPtr(uname, unamePtr, false);
                var attr = new OBJECT_ATTRIBUTES_RAW
                {
                    Length = Marshal.SizeOf<OBJECT_ATTRIBUTES_RAW>(),
                    RootDirectory = IntPtr.Zero,
                    ObjectName = unamePtr,
                    Attributes = 0,
                    SecurityDescriptor = IntPtr.Zero,
                    SecurityQualityOfService = IntPtr.Zero,
                };

                int status = NtOpenEvent(out IntPtr handle, MAXIMUM_ALLOWED, ref attr);
                if (status == 0)
                {
                    Console.WriteLine($"[{label}] '{fullPath}' -> FOUND (NtOpenEvent succeeded)");
                    NtClose(handle);
                }
                else if (status == STATUS_OBJECT_NAME_NOT_FOUND)
                {
                    Console.WriteLine($"[{label}] '{fullPath}' -> NOT FOUND (STATUS_OBJECT_NAME_NOT_FOUND)");
                }
                else if (status == STATUS_ACCESS_DENIED)
                {
                    Console.WriteLine($"[{label}] '{fullPath}' -> EXISTS BUT ACCESS_DENIED (even with MAXIMUM_ALLOWED)");
                }
                else
                {
                    Console.WriteLine($"[{label}] '{fullPath}' -> NtOpenEvent NTSTATUS 0x{status:X8}");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(unamePtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(nameBuf);
        }
    }

    // Opens a directory object and enumerates its immediate children, reporting whether
    // "AIDLCSelfTestEvent" appears among them (and the total count, for context).
    public static void TryEnumerateDirectory(string dirPath, string label)
    {
        IntPtr nameBuf = Marshal.StringToHGlobalUni(dirPath);
        try
        {
            var uname = new UNICODE_STRING_RAW
            {
                Length = (ushort)(dirPath.Length * 2),
                MaximumLength = (ushort)((dirPath.Length + 1) * 2),
                Buffer = nameBuf,
            };
            IntPtr unamePtr = Marshal.AllocHGlobal(Marshal.SizeOf<UNICODE_STRING_RAW>());
            try
            {
                Marshal.StructureToPtr(uname, unamePtr, false);
                var attr = new OBJECT_ATTRIBUTES_RAW
                {
                    Length = Marshal.SizeOf<OBJECT_ATTRIBUTES_RAW>(),
                    RootDirectory = IntPtr.Zero,
                    ObjectName = unamePtr,
                    Attributes = 0,
                    SecurityDescriptor = IntPtr.Zero,
                    SecurityQualityOfService = IntPtr.Zero,
                };

                int openStatus = NtOpenDirectoryObject(out IntPtr dirHandle, DIRECTORY_QUERY, ref attr);
                if (openStatus != 0)
                {
                    Console.WriteLine($"[{label}] NtOpenDirectoryObject('{dirPath}') failed: NTSTATUS 0x{openStatus:X8}");
                    return;
                }

                try
                {
                    uint context = 0;
                    int found = 0;
                    int total = 0;
                    bool restart = true;
                    IntPtr buf = Marshal.AllocHGlobal(8192);
                    try
                    {
                        while (true)
                        {
                            int status = NtQueryDirectoryObject(dirHandle, buf, 8192, false, restart, ref context, out uint retLen);
                            restart = false;
                            if (status != 0) break; // STATUS_NO_MORE_ENTRIES or error
                            // Parse the returned array of OBJECT_DIRECTORY_INFORMATION entries
                            // (terminated by a zero-length Name).
                            int entrySize = Marshal.SizeOf<OBJECT_DIRECTORY_INFORMATION_RAW>();
                            int offset = 0;
                            while (offset + entrySize <= (int)retLen)
                            {
                                var entry = Marshal.PtrToStructure<OBJECT_DIRECTORY_INFORMATION_RAW>(IntPtr.Add(buf, offset));
                                if (entry.Name.Length == 0) break;
                                string name = Marshal.PtrToStringUni(entry.Name.Buffer, entry.Name.Length / 2);
                                total++;
                                if (string.Equals(name, "AIDLCSelfTestEvent", StringComparison.OrdinalIgnoreCase))
                                {
                                    string typeName = Marshal.PtrToStringUni(entry.TypeName.Buffer, entry.TypeName.Length / 2);
                                    Console.WriteLine($"[{label}] FOUND 'AIDLCSelfTestEvent' in '{dirPath}' (type={typeName})");
                                    found++;
                                }
                                offset += entrySize;
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buf);
                    }
                    Console.WriteLine($"[{label}] enumerated {total} entries in '{dirPath}'; AIDLCSelfTestEvent found={found > 0}");
                }
                finally
                {
                    NtClose(dirHandle);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(unamePtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(nameBuf);
        }
    }
}
