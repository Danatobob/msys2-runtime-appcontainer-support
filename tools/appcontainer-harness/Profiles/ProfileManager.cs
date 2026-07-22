using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;

namespace AppContainerHarness.Profiles;

internal sealed class AppContainerProfile
{
    internal required string Name { get; init; }
    internal required FreeSidSafeHandle SidHandle { get; init; }
    internal required string SidString { get; init; }

    internal unsafe PSID Sid => (PSID)SidHandle.DangerousGetHandle();
}

internal static class ProfileManager
{
    public const string ProfileName = "AIDLC.AppContainerHarness";
    private const string DisplayName = "AI-DLC AppContainer Test Harness";
    private const string Description = "Sandbox profile used to test msys2-runtime AppContainer startup fix";

    // BR-7 / NFR-design Idempotent Find-or-Create pattern.
    // CreateAppContainerProfile is the only reliable "does it already exist" signal for
    // AppContainer profiles (DeriveAppContainerSidFromAppContainerName computes a SID
    // algorithmically regardless of registration, so it can't be used as an existence check).
    public static AppContainerProfile FindOrCreateProfile()
    {
        HRESULT hr = PInvoke.CreateAppContainerProfile(
            ProfileName,
            DisplayName,
            Description,
            Span<SID_AND_ATTRIBUTES>.Empty,
            out FreeSidSafeHandle sidHandle);

        if (hr.Succeeded)
        {
            return ToProfile(sidHandle);
        }

        // HRESULT_FROM_WIN32(ERROR_ALREADY_EXISTS) = 0x800700B7
        if ((uint)hr.Value == 0x800700B7)
        {
            HRESULT hr2 = PInvoke.DeriveAppContainerSidFromAppContainerName(ProfileName, out FreeSidSafeHandle sidHandle2);
            if (hr2.Failed)
            {
                throw new InvalidOperationException($"Profile '{ProfileName}' already exists but SID derivation failed: {hr2}");
            }
            return ToProfile(sidHandle2);
        }

        throw new InvalidOperationException($"CreateAppContainerProfile('{ProfileName}') failed: {hr}");
    }

    private static unsafe AppContainerProfile ToProfile(FreeSidSafeHandle sidHandle)
    {
        bool ok = PInvoke.ConvertSidToStringSid(sidHandle, out PWSTR sidStrPtr);
        if (!ok)
        {
            throw new InvalidOperationException("ConvertSidToStringSid failed for AppContainer profile SID.");
        }

        string sidString = sidStrPtr.ToString();
        PInvoke.LocalFree((HLOCAL)(IntPtr)sidStrPtr.Value);

        return new AppContainerProfile
        {
            Name = ProfileName,
            SidHandle = sidHandle,
            SidString = sidString,
        };
    }

    // Explicit --cleanup only (per user decision - not invoked automatically after runs).
    public static void DeleteProfile()
    {
        HRESULT hr = PInvoke.DeleteAppContainerProfile(ProfileName);
        if (hr.Failed && (uint)hr.Value != 0x80070002 /* ERROR_FILE_NOT_FOUND - already gone */)
        {
            throw new InvalidOperationException($"DeleteAppContainerProfile('{ProfileName}') failed: {hr}");
        }
    }

    // Grants the profile's SID read+execute on binFolder and read+write(modify) on dataFolder via icacls.
    public static void GrantScratchFolderAccess(AppContainerProfile profile, string binFolder, string dataFolder)
    {
        Directory.CreateDirectory(binFolder);
        Directory.CreateDirectory(dataFolder);

        RunIcacls(binFolder, profile.SidString, "(OI)(CI)RX");
        RunIcacls(dataFolder, profile.SidString, "(OI)(CI)M");
    }

    // FINDING (see patch-summary.md): a "raw" (non-packaged) AppContainer profile does not
    // get \AppContainerNamedObjects\<sid> auto-provisioned by Windows the way a fully
    // packaged/activated UWP app's sandbox does. The sandboxed process itself cannot create
    // it (STATUS_ACCESS_DENIED - its restricted token has no create rights on the parent
    // \AppContainerNamedObjects directory). So the launching/broker process - us - must
    // pre-create the AppContainer's own object-manager namespace root before starting the
    // sandboxed child, exactly analogous to the scratch-folder ACL grants above.
    [StructLayout(LayoutKind.Sequential)]
    private struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OBJECT_ATTRIBUTES
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    private const uint OBJ_OPENIF = 0x00000080;
    private const uint DIRECTORY_ALL_ACCESS = 0x000F000F;
    // Not included in DIRECTORY_ALL_ACCESS -- must be explicitly requested (and SeSecurityPrivilege
    // held) to be allowed to modify an object's SACL via SetKernelObjectSecurity afterwards.
    private const uint ACCESS_SYSTEM_SECURITY = 0x01000000;

    [DllImport("ntdll.dll")]
    private static extern int NtCreateDirectoryObject(out IntPtr handle, uint desiredAccess, ref OBJECT_ATTRIBUTES objectAttributes);

    [DllImport("ntdll.dll")]
    private static extern int NtClose(IntPtr handle);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string stringSecurityDescriptor, uint stringSDRevision, out IntPtr securityDescriptor, IntPtr securityDescriptorSize);

    private const uint DACL_SECURITY_INFORMATION = 0x00000004;
    private const uint SACL_SECURITY_INFORMATION = 0x00000008;

    // OBJ_OPENIF's SecurityDescriptor argument is only applied at creation time -- if the
    // directory object already exists (e.g. from an earlier run, since NT directory objects
    // are permanent by default and outlive the process), NtCreateDirectoryObject just opens
    // it and silently ignores the security descriptor we pass. So the DACL/SACL must be set
    // explicitly on every run, regardless of whether the object was just created or already
    // existed, to guarantee idempotent correctness.
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool SetKernelObjectSecurity(IntPtr handle, uint securityInformation, IntPtr securityDescriptor);

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const string SE_SECURITY_NAME = "SeSecurityPrivilege";

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES_1
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privilege;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool LookupPrivilegeValueW(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle, bool disableAllPrivileges, ref TOKEN_PRIVILEGES_1 newState,
        uint bufferLength, IntPtr previousState, IntPtr returnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    // SeSecurityPrivilege is required to set a SACL (mandatory integrity label) and is
    // disabled by default even for Administrators - it must be explicitly enabled for the
    // current process before SetKernelObjectSecurity(SACL_SECURITY_INFORMATION, ...) will
    // succeed. Throws with a clear message if the privilege isn't held at all (e.g. the
    // harness isn't running elevated), since that's an important, actionable finding.
    internal static void EnableSecurityPrivilege()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr tok))
        {
            throw new InvalidOperationException($"OpenProcessToken failed: {Marshal.GetLastWin32Error()}");
        }
        try
        {
            if (!LookupPrivilegeValueW(null, SE_SECURITY_NAME, out LUID luid))
            {
                throw new InvalidOperationException($"LookupPrivilegeValueW({SE_SECURITY_NAME}) failed: {Marshal.GetLastWin32Error()}");
            }
            var tp = new TOKEN_PRIVILEGES_1
            {
                PrivilegeCount = 1,
                Privilege = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED },
            };
            if (!AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero) || Marshal.GetLastWin32Error() == 1300 /* ERROR_NOT_ALL_ASSIGNED */)
            {
                throw new InvalidOperationException(
                    $"Could not enable {SE_SECURITY_NAME} (Win32 error {Marshal.GetLastWin32Error()}) - " +
                    "the harness must run elevated (as Administrator) to set the mandatory integrity " +
                    "label required for AppContainer access to the named-object namespace.");
            }
        }
        finally
        {
            PInvoke.CloseHandle((HANDLE)tok);
        }
    }

    // Pre-creates the AppContainer's own object-manager namespace root (\AppContainerNamedObjects\<sid>,
    // or \Sessions\<n>\AppContainerNamedObjects\<sid> depending on session), one path component at a time
    // (OBJ_OPENIF only auto-creates the final component of a single call, not intermediate ones), with an
    // Everyone-full-access DACL so the sandboxed process (running under the matching AppContainer token)
    // can then create its own subdirectory beneath it - which is exactly what winsup/cygwin's
    // appcontainer_resolve_shared_parent_dir() does. Safe/idempotent to call every run (OBJ_OPENIF opens
    // existing directories rather than erroring).
    public static unsafe void EnsureAppContainerNamedObjectPath(AppContainerProfile profile)
    {
        EnableSecurityPrivilege();

        Span<char> pathBuf = stackalloc char[512];
        uint returnLength = 0;
        bool ok;
        fixed (char* pathPtr = pathBuf)
        {
            ok = PInvoke.GetAppContainerNamedObjectPath(
                default(HANDLE),
                profile.Sid,
                (uint)pathBuf.Length,
                new PWSTR(pathPtr),
                &returnLength);
        }
        if (!ok)
        {
            throw new InvalidOperationException(
                $"GetAppContainerNamedObjectPath failed: Win32 error {Marshal.GetLastWin32Error()}");
        }
        string path = new string(pathBuf).Substring(0, pathBuf.IndexOf('\0') is int i && i >= 0 ? i : pathBuf.Length);

        // NOTE (finding): a generic "Everyone" (WD) ACE alone does NOT satisfy the AppContainer
        // access check -- Windows requires an explicit ACE for the specific AppContainer SID (or
        // the "ALL APPLICATION PACKAGES" well-known SID, alias AC) on namespace objects a LowBox
        // token needs to reach. But dropping WD entirely locks out normal-token processes (like
        // this harness itself, which needs to keep creating/managing child objects in this same
        // tree across the loop below) -- so all three are granted: WD for ordinary processes, the
        // specific profile SID (least-privilege, matches exactly which sandbox should have
        // access) and AC (defense-in-depth/clarity) for the LowBox-token sandboxed process.
        //
        // NOTE (finding, part 2): even with the correct DACL, AppContainer/LowBox tokens still
        // get STATUS_ACCESS_DENIED against an object with the default (Medium) mandatory
        // integrity label, due to Windows Mandatory Integrity Control's default "no write-up"
        // policy -- LowBox tokens run at a trust level MIC treats as very low. A SACL mandatory
        // label ACE explicitly marking the object "Low" integrity (LW) is also required, so the
        // LowBox-token caller's integrity is not considered below the object's.
        string sddl = $"D:(A;OICI;GA;;;WD)(A;OICI;GA;;;{profile.SidString})(A;OICI;GA;;;AC)S:(ML;OICI;;;;LW)";
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1 /* SDDL_REVISION_1 */, out IntPtr sd, IntPtr.Zero))
        {
            throw new InvalidOperationException($"ConvertStringSecurityDescriptorToSecurityDescriptorW failed: {Marshal.GetLastWin32Error()}");
        }

        try
        {
            IntPtr cur = IntPtr.Zero;
            bool first = true;
            foreach (string comp in path.Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                string name = first ? "\\" + comp : comp;
                first = false;

                IntPtr nameBuf = Marshal.StringToHGlobalUni(name);
                try
                {
                    var uname = new UNICODE_STRING
                    {
                        Length = (ushort)(name.Length * 2),
                        MaximumLength = (ushort)((name.Length + 1) * 2),
                        Buffer = nameBuf,
                    };
                    IntPtr unamePtr = Marshal.AllocHGlobal(Marshal.SizeOf<UNICODE_STRING>());
                    try
                    {
                        Marshal.StructureToPtr(uname, unamePtr, false);
                        var attr = new OBJECT_ATTRIBUTES
                        {
                            Length = Marshal.SizeOf<OBJECT_ATTRIBUTES>(),
                            RootDirectory = cur,
                            ObjectName = unamePtr,
                            Attributes = OBJ_OPENIF,
                            SecurityDescriptor = sd,
                            SecurityQualityOfService = IntPtr.Zero,
                        };
                        int status = NtCreateDirectoryObject(out IntPtr next, DIRECTORY_ALL_ACCESS | ACCESS_SYSTEM_SECURITY, ref attr);
                        if (status < 0)
                        {
                            throw new InvalidOperationException(
                                $"NtCreateDirectoryObject('{name}') failed: NTSTATUS 0x{status:X8}");
                        }
                        if (!SetKernelObjectSecurity(next, DACL_SECURITY_INFORMATION | SACL_SECURITY_INFORMATION, sd))
                        {
                            throw new InvalidOperationException(
                                $"SetKernelObjectSecurity('{name}') failed: Win32 error {Marshal.GetLastWin32Error()}");
                        }
                        if (cur != IntPtr.Zero) NtClose(cur);
                        cur = next;
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
            if (cur != IntPtr.Zero) NtClose(cur);
        }
        finally
        {
            PInvoke.LocalFree((HLOCAL)sd);
        }
    }

    private static void RunIcacls(string path, string sidString, string permissionMask)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "icacls.exe",
            ArgumentList = { path, "/grant", $"*{sidString}:{permissionMask}" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = System.Diagnostics.Process.Start(psi)!;
        proc.WaitForExit(15000);
        if (proc.ExitCode != 0)
        {
            string stderr = proc.StandardError.ReadToEnd();
            string stdout = proc.StandardOutput.ReadToEnd();
            throw new InvalidOperationException($"icacls grant on '{path}' failed (exit {proc.ExitCode}): {stdout} {stderr}");
        }
    }
}
