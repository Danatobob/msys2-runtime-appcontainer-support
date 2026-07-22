using System.Diagnostics;
using AppContainerHarness.Diagnostics;
using AppContainerHarness.Launching;
using AppContainerHarness.Models;
using AppContainerHarness.Profiles;
using AppContainerHarness.Reporting;
using AppContainerHarness.Scenarios;

namespace AppContainerHarness;

public static class Program
{
    private const string ScratchRoot = @"E:\Temp\appcontainer-harness";

    // Round-4 diagnostic (self-test): when launched with this single flag, this process is
    // running INSIDE the sandboxed AppContainer (launched by the --self-test-createevent branch
    // below). It does nothing but attempt a plain Win32 named-object creation -- CreateEventW,
    // NOT a raw Nt* call -- to test whether ordinary Win32 named-object creation succeeds under
    // this exact token/profile, as a control against winsup/cygwin's raw NtOpenDirectoryObject
    // walk of \Sessions\<n>\... which currently fails. If this succeeds, it proves the OS's own
    // (presumably different, non-raw-Nt*) mechanism for resolving the AppContainer's named-object
    // namespace works fine for this token -- pointing at our raw-Nt*-walk approach itself, not
    // the token/profile construction, as the thing to fix.
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEventW(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [System.Runtime.InteropServices.DllImport("ntdll.dll")]
    private static extern int RtlGetNamedObjectDirectory(out IntPtr handle);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct UNICODE_STRING_PROBE
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct OBJECT_ATTRIBUTES_PROBE
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [System.Runtime.InteropServices.DllImport("ntdll.dll")]
    private static extern int NtCreateDirectoryObject(out IntPtr handle, uint desiredAccess, ref OBJECT_ATTRIBUTES_PROBE objectAttributes);

    [System.Runtime.InteropServices.DllImport("ntdll.dll")]
    private static extern int NtOpenDirectoryObject(out IntPtr handle, uint desiredAccess, ref OBJECT_ATTRIBUTES_PROBE objectAttributes);

    private const uint DIRECTORY_TRAVERSE_PROBE = 0x0002;
    private const uint DIRECTORY_QUERY_PROBE = 0x0001;

    private static unsafe int TestFullPathOpen(string fullPath)
    {
        IntPtr nameBuf = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(fullPath);
        try
        {
            var uname = new UNICODE_STRING_PROBE
            {
                Length = (ushort)(fullPath.Length * 2),
                MaximumLength = (ushort)((fullPath.Length + 1) * 2),
                Buffer = nameBuf,
            };
            IntPtr unamePtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf<UNICODE_STRING_PROBE>());
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(uname, unamePtr, false);
                var attr = new OBJECT_ATTRIBUTES_PROBE
                {
                    Length = System.Runtime.InteropServices.Marshal.SizeOf<OBJECT_ATTRIBUTES_PROBE>(),
                    RootDirectory = IntPtr.Zero,
                    ObjectName = unamePtr,
                    Attributes = 0,
                    SecurityDescriptor = IntPtr.Zero,
                    SecurityQualityOfService = IntPtr.Zero,
                };
                int status = NtOpenDirectoryObject(out IntPtr handle, DIRECTORY_QUERY_PROBE | DIRECTORY_TRAVERSE_PROBE, ref attr);
                if (status == 0)
                {
                    // (leak handle deliberately -- process is short-lived probe)
                }
                return status;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(unamePtr);
            }
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(nameBuf);
        }
    }

    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--internal-createevent-probe")
        {
            IntPtr h = CreateEventW(IntPtr.Zero, false, false, @"Local\AIDLCSelfTestEvent");
            int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (h == IntPtr.Zero)
            {
                Console.WriteLine($"CreateEventW(Local\\AIDLCSelfTestEvent) FAILED, Win32 error {err}");
                return 1;
            }
            Console.WriteLine($"CreateEventW(Local\\AIDLCSelfTestEvent) SUCCEEDED (err={err}, 0=created new/183=already existed)");
            Console.Out.Flush();
            // Round-5: hold the handle open for a window so an external, unsandboxed probe can
            // locate exactly where this object landed in the object-manager namespace.
            System.Threading.Thread.Sleep(8000);
            return 0;
        }

        // Round-5 (per coordinator direction): non-interactive, no-GUI-tool location of where
        // CreateEventW's "Local\..." name actually landed for an AppContainer/LowBox token.
        // Launches the self-test child WITHOUT blocking, sleeps briefly while the child holds
        // the event open (see the 8s sleep above), probes candidate NT object-manager paths from
        // this (unsandboxed) process, then waits for the child to finish.
        if (args.Length == 1 && args[0] == "--self-test-locate")
        {
            var locateProfile = ProfileManager.FindOrCreateProfile();
            Console.WriteLine($"Profile SID: {locateProfile.SidString}");
            string? selfPath2 = Environment.ProcessPath;
            if (selfPath2 is null)
            {
                Console.Error.WriteLine("Could not determine Environment.ProcessPath for self-relaunch.");
                return 2;
            }

            Console.WriteLine("Launching self --internal-createevent-probe (non-blocking) under the AppContainer profile...");
            var (childHandle, childPid) = ProcessLauncher.LaunchArbitraryNonBlocking(locateProfile, selfPath2, "--internal-createevent-probe");
            Console.WriteLine($"Child PID={childPid}, waiting 3s for it to create+hold the event...");
            System.Threading.Thread.Sleep(3000);

            int sessionId2 = Process.GetCurrentProcess().SessionId;
            string sidStr2 = locateProfile.SidString;

            Console.WriteLine();
            Console.WriteLine("--- Probing candidate locations for 'AIDLCSelfTestEvent' ---");
            TokenDiag.TryLocateNamedObject($@"\Sessions\{sessionId2}\BaseNamedObjects\AIDLCSelfTestEvent", "plain session BaseNamedObjects");
            TokenDiag.TryLocateNamedObject($@"\Sessions\{sessionId2}\AppContainerNamedObjects\{sidStr2}\AIDLCSelfTestEvent", "AppContainer-scoped ACNO");
            TokenDiag.TryLocateNamedObject(@"\BaseNamedObjects\AIDLCSelfTestEvent", "global BaseNamedObjects (session 0)");

            Console.WriteLine();
            Console.WriteLine("--- Enumerating directory contents (looking for AIDLCSelfTestEvent) ---");
            TokenDiag.TryEnumerateDirectory($@"\Sessions\{sessionId2}\BaseNamedObjects", "plain session BaseNamedObjects");
            TokenDiag.TryEnumerateDirectory($@"\Sessions\{sessionId2}\AppContainerNamedObjects\{sidStr2}", "AppContainer-scoped ACNO");

            ProcessLauncher.WaitAndCloseNonBlocking(childHandle, 10000);
            return 0;
        }

        // Round-5 hypothesis test: a SINGLE Nt* call resolving a full multi-segment absolute path
        // string may benefit from "bypass traverse checking" on intermediate segments (a
        // well-known NT object-manager path-parsing optimization/exception, distinct from the
        // per-object access check our code's separate NtOpenDirectoryObject-per-component walk
        // incurs) -- unlike opening \Sessions and \Sessions\<n> as SEPARATE standalone handles,
        // which is what currently fails. This directly tests that theory using the exact path
        // Round 5's locate probe confirmed the real object lives at.
        if (args.Length == 2 && args[0] == "--internal-fullpath-probe")
        {
            string fullPath = args[1];
            int status = TestFullPathOpen(fullPath);
            Console.WriteLine($"NtOpenDirectoryObject(full path '{fullPath}', single call) -> NTSTATUS 0x{status:X8}" + (status == 0 ? " SUCCEEDED" : " FAILED"));
            return status == 0 ? 0 : 1;
        }

        if (args.Length == 1 && args[0] == "--internal-rtlgetnamedobjdir-probe")
        {
            int status = RtlGetNamedObjectDirectory(out IntPtr baseDirHandle);
            if (status < 0)
            {
                Console.WriteLine($"RtlGetNamedObjectDirectory FAILED: NTSTATUS 0x{status:X8}");
                return 1;
            }
            Console.WriteLine($"RtlGetNamedObjectDirectory SUCCEEDED, handle=0x{baseDirHandle:X}");

            string childName = "AIDLCProbeChild";
            IntPtr nameBuf = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(childName);
            try
            {
                var uname = new UNICODE_STRING_PROBE
                {
                    Length = (ushort)(childName.Length * 2),
                    MaximumLength = (ushort)((childName.Length + 1) * 2),
                    Buffer = nameBuf,
                };
                IntPtr unamePtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf<UNICODE_STRING_PROBE>());
                try
                {
                    System.Runtime.InteropServices.Marshal.StructureToPtr(uname, unamePtr, false);
                    var attr = new OBJECT_ATTRIBUTES_PROBE
                    {
                        Length = System.Runtime.InteropServices.Marshal.SizeOf<OBJECT_ATTRIBUTES_PROBE>(),
                        RootDirectory = baseDirHandle,
                        ObjectName = unamePtr,
                        Attributes = 0x80 /* OBJ_OPENIF */,
                        SecurityDescriptor = IntPtr.Zero,
                        SecurityQualityOfService = IntPtr.Zero,
                    };
                    int createStatus = NtCreateDirectoryObject(out IntPtr childHandle, 0x000F000F /* DIRECTORY_ALL_ACCESS */, ref attr);
                    if (createStatus < 0)
                    {
                        Console.WriteLine($"NtCreateDirectoryObject('{childName}' relative to RtlGetNamedObjectDirectory's handle) FAILED: NTSTATUS 0x{createStatus:X8}");
                        return 1;
                    }
                    Console.WriteLine($"NtCreateDirectoryObject('{childName}' relative to RtlGetNamedObjectDirectory's handle) SUCCEEDED");
                    return 0;
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(unamePtr);
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(nameBuf);
            }
        }

        if (args.Length == 1 && args[0] == "--self-test-fullpath")
        {
            var fpProfile = ProfileManager.FindOrCreateProfile();
            int fpSessionId = Process.GetCurrentProcess().SessionId;
            string fpPath = $@"\Sessions\{fpSessionId}\AppContainerNamedObjects\{fpProfile.SidString}";
            Console.WriteLine($"Profile SID: {fpProfile.SidString}");
            Console.WriteLine($"Testing single-call full-path open of: {fpPath}");
            string? fpSelfPath = Environment.ProcessPath;
            if (fpSelfPath is null)
            {
                Console.Error.WriteLine("Could not determine Environment.ProcessPath for self-relaunch.");
                return 2;
            }
            string fpCaptureDir = Path.Combine(ScratchRoot, "selftest-capture");
            var fpResult = ProcessLauncher.LaunchArbitrary(fpProfile, fpSelfPath, $"--internal-fullpath-probe \"{fpPath}\"", fpCaptureDir);
            Console.WriteLine($"ExitCode={fpResult.ExitCode} TimedOut={fpResult.TimedOut}");
            Console.WriteLine($"Stdout: {fpResult.Stdout}");
            Console.WriteLine($"Stderr: {fpResult.Stderr}");
            return 0;
        }

        if (args.Length == 1 && (args[0] == "--self-test-createevent" || args[0] == "--self-test-rtlgetnamedobjdir"))
        {
            string probeArg = args[0] == "--self-test-createevent" ? "--internal-createevent-probe" : "--internal-rtlgetnamedobjdir-probe";
            var probeProfile = ProfileManager.FindOrCreateProfile();
            Console.WriteLine($"Profile SID: {probeProfile.SidString}");
            string? selfPath = Environment.ProcessPath;
            if (selfPath is null)
            {
                Console.Error.WriteLine("Could not determine Environment.ProcessPath for self-relaunch.");
                return 2;
            }
            string captureDir = Path.Combine(ScratchRoot, "selftest-capture");
            Console.WriteLine($"Launching self ({selfPath}) {probeArg} under the AppContainer profile...");
            var result = ProcessLauncher.LaunchArbitrary(probeProfile, selfPath, probeArg, captureDir);
            Console.WriteLine($"ExitCode={result.ExitCode} TimedOut={result.TimedOut}");
            Console.WriteLine($"Stdout: {result.Stdout}");
            Console.WriteLine($"Stderr: {result.Stderr}");
            return 0;
        }

        string? target = null;
        string? scenarioName = null;
        ExpectMode? expect = null;
        bool verbose = false;
        bool cleanup = false;
        bool debugToken = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--target":
                    target = args[++i];
                    break;
                case "--scenario":
                    scenarioName = args[++i];
                    break;
                case "--expect":
                    expect = args[++i].ToLowerInvariant() switch
                    {
                        "failing" => ExpectMode.Failing,
                        "working" => ExpectMode.Working,
                        var other => throw new ArgumentException($"Unknown --expect value: {other}"),
                    };
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--cleanup":
                    cleanup = true;
                    break;
                case "--debug-token":
                    debugToken = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return 2;
            }
        }

        if (cleanup)
        {
            Console.WriteLine($"Deleting AppContainer profile '{ProfileManager.ProfileName}'...");
            ProfileManager.DeleteProfile();
            Console.WriteLine("Done.");
            return 0;
        }

        if (debugToken)
        {
            string repoRootDbg = FindRepoRoot();
            string toolchainBashDbg = Path.Combine(repoRootDbg, ".build-toolchain", "msys64", "usr", "bin", "bash.exe");
            if (!File.Exists(toolchainBashDbg))
            {
                Console.Error.WriteLine($"Toolchain bash.exe not found at: {toolchainBashDbg}");
                return 2;
            }

            Console.WriteLine("=== Round 3: TokenCapabilities + \\Sessions security investigation ===");
            var dbgProfile = ProfileManager.FindOrCreateProfile();
            Console.WriteLine($"Profile SID: {dbgProfile.SidString}");

            Console.WriteLine();
            Console.WriteLine("--- Harness's OWN (unsandboxed) process token, for contrast ---");
            TokenDiag.DumpProcessToken(Process.GetCurrentProcess().Handle, "harness-self");

            Console.WriteLine();
            Console.WriteLine("--- Sandboxed child process token ---");
            ProcessLauncher.DebugLaunchAndDumpToken(dbgProfile, toolchainBashDbg);

            Console.WriteLine();
            Console.WriteLine("--- \\Sessions and own-session security descriptors (as seen by the harness, unsandboxed) ---");
            try
            {
                ProfileManager.EnableSecurityPrivilege();
                Console.WriteLine("SeSecurityPrivilege enabled (harness is running elevated).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not enable SeSecurityPrivilege: {ex.Message} (SACL/integrity label will be unavailable below)");
            }
            int sessionId = Process.GetCurrentProcess().SessionId;
            TokenDiag.DumpObjectSecurity(@"\Sessions", "Sessions-top");
            TokenDiag.DumpObjectSecurity($@"\Sessions\{sessionId}", $"Sessions-{sessionId}");
            TokenDiag.DumpObjectSecurity($@"\Sessions\{sessionId}\AppContainerNamedObjects", $"Sessions-{sessionId}-ACNO");

            return 0;
        }

        if (target is null || expect is null)
        {
            Console.Error.WriteLine("Usage: appcontainer-harness --target <path-to-msys-2.0.dll> --expect failing|working [--scenario <name>] [--verbose]");
            Console.Error.WriteLine("       appcontainer-harness --cleanup");
            return 2;
        }

        if (!File.Exists(target))
        {
            Console.Error.WriteLine($"Target DLL not found: {target}");
            return 2;
        }

        string repoRoot = FindRepoRoot();
        string toolchainBinDir = Path.Combine(repoRoot, ".build-toolchain", "msys64", "usr", "bin");
        string toolchainBash = Path.Combine(toolchainBinDir, "bash.exe");
        if (!File.Exists(toolchainBash))
        {
            Console.Error.WriteLine($"Toolchain bash.exe not found at: {toolchainBash} (expected Unit 1a's isolated toolchain)");
            return 2;
        }

        string binFolder = Path.Combine(ScratchRoot, "bin");
        string dataFolder = Path.Combine(ScratchRoot, "data");
        string captureFolder = Path.Combine(ScratchRoot, "capture");
        string reportPath = Path.Combine(ScratchRoot, "last-report.json");

        Console.WriteLine("Finding or creating AppContainer profile...");
        var profile = ProfileManager.FindOrCreateProfile();
        Console.WriteLine($"Profile SID: {profile.SidString}");

        Console.WriteLine("Granting scratch folder access...");
        ProfileManager.GrantScratchFolderAccess(profile, binFolder, dataFolder);

        // TEMP DISABLED for access-mask-hypothesis re-test (see patch-summary.md addendum):
        // testing whether the sandboxed process can self-provision its own namespace path
        // using Windows' own default-granted AppContainer rights, once appcontainer.cc
        // requests a minimal access mask on intermediate/existing path components instead
        // of the full CYG_SHARED_DIR_ACCESS it was previously (over-)requesting everywhere.
        // Console.WriteLine("Ensuring AppContainer named-object namespace path...");
        // ProfileManager.EnsureAppContainerNamedObjectPath(profile);

        Console.WriteLine("Staging test bin...");
        ProcessLauncher.StageTestBin(toolchainBinDir, target, binFolder);
        string stagedBash = Path.Combine(binFolder, "bash.exe");

        IEnumerable<Scenario> scenarios;
        if (scenarioName is not null)
        {
            var s = ScenarioLibrary.GetScenario(scenarioName);
            if (s is null)
            {
                Console.Error.WriteLine($"Unknown scenario: {scenarioName}");
                return 2;
            }
            scenarios = new[] { s };
        }
        else
        {
            scenarios = ScenarioLibrary.GetAllScenarios();
        }

        Console.WriteLine("Running scenarios...");
        var report = ScenarioRunner.RunSuite(profile, stagedBash, dataFolder, target, expect.Value, scenarios);

        Reporter.PrintSummary(report, verbose);
        Reporter.WriteJsonReport(report, reportPath);
        Console.WriteLine($"JSON report written to: {reportPath}");

        return report.OverallPassed ? 0 : 1;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "aidlc-docs")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate repo root (no ancestor directory containing 'aidlc-docs' found).");
    }
}
