using System.Runtime.InteropServices;
using AppContainerHarness.Models;
using AppContainerHarness.Profiles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;
using Windows.Win32.Storage.FileSystem;

namespace AppContainerHarness.Launching;

internal static class ProcessLauncher
{
    private const uint PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES = 0x00020009;
    private const uint GENERIC_WRITE = 0x40000000;
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(15);

    // Copies bash.exe + msys-2.0.dll (from --target) into binFolder, overwriting any previous staging.
    internal static void StageTestBin(string toolchainBinDir, string targetDllPath, string binFolder)
    {
        Directory.CreateDirectory(binFolder);
        string srcBash = Path.Combine(toolchainBinDir, "bash.exe");
        File.Copy(srcBash, Path.Combine(binFolder, "bash.exe"), overwrite: true);
        File.Copy(targetDllPath, Path.Combine(binFolder, "msys-2.0.dll"), overwrite: true);
    }

    private static unsafe HANDLE CreateInheritableFile(string path, SECURITY_ATTRIBUTES* sa)
    {
        fixed (char* pathPtr = path)
        {
            return PInvoke.CreateFile(
                new PCWSTR(pathPtr),
                GENERIC_WRITE,
                FILE_SHARE_MODE.FILE_SHARE_READ,
                sa,
                FILE_CREATION_DISPOSITION.CREATE_ALWAYS,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                default(HANDLE));
        }
    }

    // Builds a CreateProcess-style Unicode environment block (NAME=VALUE\0 ... \0\0), starting
    // from the current process's environment but overriding TMP/TEMP/TMPDIR to point at a
    // directory the sandboxed AppContainer SID actually has ACL access to. Without this, the
    // child inherits the unsandboxed harness's real TMP/TEMP (the user's normal %TEMP%, which the
    // AppContainer profile was never granted access to), and bash's own startup check for a
    // writable /tmp fails with "could not find /tmp, please create!" even though nothing about
    // shared-memory/pipe namespace resolution is involved.
    private static string BuildEnvironmentBlockWithTemp(string tempDir)
    {
        var vars = new System.Collections.Generic.SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
        {
            vars[(string)e.Key] = (string)(e.Value ?? string.Empty);
        }
        vars["TMP"] = tempDir;
        vars["TEMP"] = tempDir;
        vars["TMPDIR"] = tempDir;

        var sb = new System.Text.StringBuilder();
        foreach (var kv in vars)
        {
            sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\0');
        }
        sb.Append('\0');
        return sb.ToString();
    }

    internal static unsafe LaunchResult Launch(AppContainerProfile profile, string bashExePath, string command, string captureDir)
    {
        Directory.CreateDirectory(captureDir);
        string outPath = Path.Combine(captureDir, $"stdout-{Guid.NewGuid():N}.txt");
        string errPath = Path.Combine(captureDir, $"stderr-{Guid.NewGuid():N}.txt");
        string envBlock = BuildEnvironmentBlockWithTemp(captureDir);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        SECURITY_ATTRIBUTES sa = new()
        {
            nLength = (uint)Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = true,
            lpSecurityDescriptor = null,
        };

        HANDLE outHandle = CreateInheritableFile(outPath, &sa);
        HANDLE errHandle = CreateInheritableFile(errPath, &sa);

        SECURITY_CAPABILITIES secCap = new()
        {
            AppContainerSid = profile.Sid,
            Capabilities = null,
            CapabilityCount = 0,
            Reserved = 0,
        };

        nuint attrListSize = 0;
        PInvoke.InitializeProcThreadAttributeList(default, 1, 0, &attrListSize);
        IntPtr attrListBuffer = Marshal.AllocHGlobal((int)attrListSize);
        var attrList = new LPPROC_THREAD_ATTRIBUTE_LIST((void*)attrListBuffer);

        int? exitCode = null;
        bool timedOut = false;
        PROCESS_INFORMATION pi = default;

        try
        {
            if (!PInvoke.InitializeProcThreadAttributeList(attrList, 1, 0, &attrListSize))
            {
                throw new InvalidOperationException("InitializeProcThreadAttributeList failed: " + Marshal.GetLastWin32Error());
            }

            if (!PInvoke.UpdateProcThreadAttribute(
                    attrList,
                    0,
                    PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES,
                    &secCap,
                    (nuint)Marshal.SizeOf<SECURITY_CAPABILITIES>(),
                    null,
                    (nuint*)null))
            {
                throw new InvalidOperationException("UpdateProcThreadAttribute failed: " + Marshal.GetLastWin32Error());
            }

            STARTUPINFOEXW siex = default;
            siex.StartupInfo.cb = (uint)Marshal.SizeOf<STARTUPINFOEXW>();
            siex.StartupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;
            siex.StartupInfo.hStdOutput = outHandle;
            siex.StartupInfo.hStdError = errHandle;
            siex.lpAttributeList = attrList;

            string quotedCommand = command.Replace("\"", "\\\"");
            string commandLine = $"\"{bashExePath}\" -lc \"{quotedCommand}\"";
            // Round 8 diagnostic: cwd = captureDir (writable) instead of the bin folder (read+execute
            // only), so that if the sandboxed process crashes and Cygwin's exception handler tries to
            // write a "<prog>.exe.stackdump" file to cwd, it can actually succeed instead of silently
            // failing to write it into the read-only bin folder.
            string workingDir = captureDir;

            bool created;
            fixed (char* cmdLinePtr = commandLine)
            fixed (char* appNamePtr = bashExePath)
            fixed (char* curDirPtr = workingDir)
            fixed (char* envPtr = envBlock)
            {
                created = PInvoke.CreateProcess(
                    new PCWSTR(appNamePtr),
                    new PWSTR(cmdLinePtr),
                    (SECURITY_ATTRIBUTES*)null,
                    (SECURITY_ATTRIBUTES*)null,
                    true,
                    PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT | PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT,
                    envPtr,
                    new PCWSTR(curDirPtr),
                    &siex.StartupInfo,
                    &pi);
            }

            if (!created)
            {
                int err = Marshal.GetLastWin32Error();
                return new LaunchResult
                {
                    ExitCode = null,
                    Stdout = string.Empty,
                    Stderr = $"CreateProcess failed with Win32 error {err}",
                    TimedOut = false,
                    Duration = sw.Elapsed,
                };
            }

            WAIT_EVENT waitResult = PInvoke.WaitForSingleObject(pi.hProcess, (uint)LaunchTimeout.TotalMilliseconds);
            if (waitResult == WAIT_EVENT.WAIT_TIMEOUT)
            {
                timedOut = true;
                PInvoke.TerminateProcess(pi.hProcess, 1);
                PInvoke.WaitForSingleObject(pi.hProcess, 2000);
            }
            else
            {
                uint code;
                _ = PInvoke.GetExitCodeProcess(pi.hProcess, &code);
                exitCode = unchecked((int)code);
            }
        }
        finally
        {
            PInvoke.DeleteProcThreadAttributeList(attrList);
            Marshal.FreeHGlobal(attrListBuffer);
            if (pi.hProcess != default) PInvoke.CloseHandle(pi.hProcess);
            if (pi.hThread != default) PInvoke.CloseHandle(pi.hThread);
            if (outHandle != default) PInvoke.CloseHandle(outHandle);
            if (errHandle != default) PInvoke.CloseHandle(errHandle);
        }

        sw.Stop();

        string stdout = ReadAndDelete(outPath);
        string stderr = ReadAndDelete(errPath);

        return new LaunchResult
        {
            ExitCode = exitCode,
            Stdout = stdout,
            Stderr = stderr,
            TimedOut = timedOut,
            Duration = sw.Elapsed,
        };
    }

    // Round-3 diagnostic (per user request): launch a trivial long-lived command under the
    // profile's token and dump its TokenGroups/TokenCapabilities immediately after CreateProcess
    // succeeds (the token is fixed at process-creation time, so this is valid even though the
    // sandboxed bash.exe itself will likely abort quickly on the current \Sessions bug --
    // "sleep 5" gives ample margin regardless).
    internal static unsafe void DebugLaunchAndDumpToken(AppContainerProfile profile, string bashExePath)
    {
        SECURITY_CAPABILITIES secCap = new()
        {
            AppContainerSid = profile.Sid,
            Capabilities = null,
            CapabilityCount = 0,
            Reserved = 0,
        };

        nuint attrListSize = 0;
        PInvoke.InitializeProcThreadAttributeList(default, 1, 0, &attrListSize);
        IntPtr attrListBuffer = Marshal.AllocHGlobal((int)attrListSize);
        var attrList = new LPPROC_THREAD_ATTRIBUTE_LIST((void*)attrListBuffer);
        PROCESS_INFORMATION pi = default;

        try
        {
            if (!PInvoke.InitializeProcThreadAttributeList(attrList, 1, 0, &attrListSize))
                throw new InvalidOperationException("InitializeProcThreadAttributeList failed: " + Marshal.GetLastWin32Error());

            if (!PInvoke.UpdateProcThreadAttribute(attrList, 0, PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES,
                    &secCap, (nuint)Marshal.SizeOf<SECURITY_CAPABILITIES>(), null, (nuint*)null))
                throw new InvalidOperationException("UpdateProcThreadAttribute failed: " + Marshal.GetLastWin32Error());

            STARTUPINFOEXW siex = default;
            siex.StartupInfo.cb = (uint)Marshal.SizeOf<STARTUPINFOEXW>();
            siex.lpAttributeList = attrList;

            string commandLine = $"\"{bashExePath}\" -lc \"sleep 5\"";
            string workingDir = Path.GetDirectoryName(bashExePath) ?? ".";

            bool created;
            fixed (char* cmdLinePtr = commandLine)
            fixed (char* appNamePtr = bashExePath)
            fixed (char* curDirPtr = workingDir)
            {
                created = PInvoke.CreateProcess(
                    new PCWSTR(appNamePtr), new PWSTR(cmdLinePtr),
                    (SECURITY_ATTRIBUTES*)null, (SECURITY_ATTRIBUTES*)null, false,
                    PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT,
                    (void*)null, new PCWSTR(curDirPtr), &siex.StartupInfo, &pi);
            }

            if (!created)
            {
                Console.WriteLine($"[debug-token] CreateProcess failed: {Marshal.GetLastWin32Error()}");
                return;
            }

            AppContainerHarness.Diagnostics.TokenDiag.DumpProcessToken(pi.hProcess, "sandboxed-child");
            PInvoke.WaitForSingleObject(pi.hProcess, 6000);
        }
        finally
        {
            PInvoke.DeleteProcThreadAttributeList(attrList);
            Marshal.FreeHGlobal(attrListBuffer);
            if (pi.hProcess != default) PInvoke.CloseHandle(pi.hProcess);
            if (pi.hThread != default) PInvoke.CloseHandle(pi.hThread);
        }
    }

    // Round-4 diagnostic: launch an arbitrary exe+args (not bash -lc) under the profile's
    // token, capturing stdout/stderr the same way Launch() does. Used to test whether a plain
    // Win32 named-object API (CreateEventW) succeeds under this exact token/profile, as a
    // control against the raw Nt* \Sessions walk that winsup/cygwin's patch performs.
    internal static unsafe LaunchResult LaunchArbitrary(AppContainerProfile profile, string exePath, string args, string captureDir)
    {
        Directory.CreateDirectory(captureDir);
        string outPath = Path.Combine(captureDir, $"stdout-{Guid.NewGuid():N}.txt");
        string errPath = Path.Combine(captureDir, $"stderr-{Guid.NewGuid():N}.txt");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        SECURITY_ATTRIBUTES sa = new()
        {
            nLength = (uint)Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = true,
            lpSecurityDescriptor = null,
        };
        HANDLE outHandle = CreateInheritableFile(outPath, &sa);
        HANDLE errHandle = CreateInheritableFile(errPath, &sa);

        SECURITY_CAPABILITIES secCap = new()
        {
            AppContainerSid = profile.Sid,
            Capabilities = null,
            CapabilityCount = 0,
            Reserved = 0,
        };

        nuint attrListSize = 0;
        PInvoke.InitializeProcThreadAttributeList(default, 1, 0, &attrListSize);
        IntPtr attrListBuffer = Marshal.AllocHGlobal((int)attrListSize);
        var attrList = new LPPROC_THREAD_ATTRIBUTE_LIST((void*)attrListBuffer);

        int? exitCode = null;
        bool timedOut = false;
        PROCESS_INFORMATION pi = default;

        try
        {
            if (!PInvoke.InitializeProcThreadAttributeList(attrList, 1, 0, &attrListSize))
                throw new InvalidOperationException("InitializeProcThreadAttributeList failed: " + Marshal.GetLastWin32Error());

            if (!PInvoke.UpdateProcThreadAttribute(attrList, 0, PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES,
                    &secCap, (nuint)Marshal.SizeOf<SECURITY_CAPABILITIES>(), null, (nuint*)null))
                throw new InvalidOperationException("UpdateProcThreadAttribute failed: " + Marshal.GetLastWin32Error());

            STARTUPINFOEXW siex = default;
            siex.StartupInfo.cb = (uint)Marshal.SizeOf<STARTUPINFOEXW>();
            siex.StartupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;
            siex.StartupInfo.hStdOutput = outHandle;
            siex.StartupInfo.hStdError = errHandle;
            siex.lpAttributeList = attrList;

            string commandLine = $"\"{exePath}\" {args}";
            string workingDir = Path.GetDirectoryName(exePath) ?? ".";

            bool created;
            fixed (char* cmdLinePtr = commandLine)
            fixed (char* appNamePtr = exePath)
            fixed (char* curDirPtr = workingDir)
            {
                created = PInvoke.CreateProcess(
                    new PCWSTR(appNamePtr), new PWSTR(cmdLinePtr),
                    (SECURITY_ATTRIBUTES*)null, (SECURITY_ATTRIBUTES*)null, true,
                    PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT,
                    (void*)null, new PCWSTR(curDirPtr), &siex.StartupInfo, &pi);
            }

            if (!created)
            {
                int err = Marshal.GetLastWin32Error();
                return new LaunchResult { ExitCode = null, Stdout = string.Empty, Stderr = $"CreateProcess failed: {err}", TimedOut = false, Duration = sw.Elapsed };
            }

            WAIT_EVENT waitResult = PInvoke.WaitForSingleObject(pi.hProcess, (uint)LaunchTimeout.TotalMilliseconds);
            if (waitResult == WAIT_EVENT.WAIT_TIMEOUT)
            {
                timedOut = true;
                PInvoke.TerminateProcess(pi.hProcess, 1);
                PInvoke.WaitForSingleObject(pi.hProcess, 2000);
            }
            else
            {
                uint code;
                _ = PInvoke.GetExitCodeProcess(pi.hProcess, &code);
                exitCode = unchecked((int)code);
            }
        }
        finally
        {
            PInvoke.DeleteProcThreadAttributeList(attrList);
            Marshal.FreeHGlobal(attrListBuffer);
            if (pi.hProcess != default) PInvoke.CloseHandle(pi.hProcess);
            if (pi.hThread != default) PInvoke.CloseHandle(pi.hThread);
            if (outHandle != default) PInvoke.CloseHandle(outHandle);
            if (errHandle != default) PInvoke.CloseHandle(errHandle);
        }

        sw.Stop();
        return new LaunchResult
        {
            ExitCode = exitCode,
            Stdout = ReadAndDelete(outPath),
            Stderr = ReadAndDelete(errPath),
            TimedOut = timedOut,
            Duration = sw.Elapsed,
        };
    }

    // Round-5: launch an arbitrary exe+args under the profile's token WITHOUT waiting for exit --
    // returns immediately after CreateProcess succeeds, so the caller can do other work (e.g.
    // probe the object-manager namespace) while the child is still running/holding a handle open.
    internal static unsafe (IntPtr processHandle, int pid) LaunchArbitraryNonBlocking(AppContainerProfile profile, string exePath, string args)
    {
        SECURITY_CAPABILITIES secCap = new()
        {
            AppContainerSid = profile.Sid,
            Capabilities = null,
            CapabilityCount = 0,
            Reserved = 0,
        };

        nuint attrListSize = 0;
        PInvoke.InitializeProcThreadAttributeList(default, 1, 0, &attrListSize);
        IntPtr attrListBuffer = Marshal.AllocHGlobal((int)attrListSize);
        var attrList = new LPPROC_THREAD_ATTRIBUTE_LIST((void*)attrListBuffer);
        PROCESS_INFORMATION pi = default;

        try
        {
            if (!PInvoke.InitializeProcThreadAttributeList(attrList, 1, 0, &attrListSize))
                throw new InvalidOperationException("InitializeProcThreadAttributeList failed: " + Marshal.GetLastWin32Error());

            if (!PInvoke.UpdateProcThreadAttribute(attrList, 0, PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES,
                    &secCap, (nuint)Marshal.SizeOf<SECURITY_CAPABILITIES>(), null, (nuint*)null))
                throw new InvalidOperationException("UpdateProcThreadAttribute failed: " + Marshal.GetLastWin32Error());

            STARTUPINFOEXW siex = default;
            siex.StartupInfo.cb = (uint)Marshal.SizeOf<STARTUPINFOEXW>();
            siex.lpAttributeList = attrList;

            string commandLine = $"\"{exePath}\" {args}";
            string workingDir = Path.GetDirectoryName(exePath) ?? ".";

            bool created;
            fixed (char* cmdLinePtr = commandLine)
            fixed (char* appNamePtr = exePath)
            fixed (char* curDirPtr = workingDir)
            {
                created = PInvoke.CreateProcess(
                    new PCWSTR(appNamePtr), new PWSTR(cmdLinePtr),
                    (SECURITY_ATTRIBUTES*)null, (SECURITY_ATTRIBUTES*)null, false,
                    PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT,
                    (void*)null, new PCWSTR(curDirPtr), &siex.StartupInfo, &pi);
            }

            if (!created)
            {
                Console.WriteLine($"[launch-nonblocking] CreateProcess failed: {Marshal.GetLastWin32Error()}");
                return (IntPtr.Zero, -1);
            }

            if (pi.hThread != default) PInvoke.CloseHandle(pi.hThread);
            return (pi.hProcess, (int)pi.dwProcessId);
        }
        finally
        {
            PInvoke.DeleteProcThreadAttributeList(attrList);
            Marshal.FreeHGlobal(attrListBuffer);
        }
    }

    internal static unsafe void WaitAndCloseNonBlocking(IntPtr processHandle, uint timeoutMs)
    {
        if (processHandle == IntPtr.Zero) return;
        var h = new HANDLE(processHandle);
        PInvoke.WaitForSingleObject(h, timeoutMs);
        PInvoke.CloseHandle(h);
    }

    private static string ReadAndDelete(string path)
    {
        try
        {
            if (!File.Exists(path)) return string.Empty;
            string content = File.ReadAllText(path);
            File.Delete(path);
            return content;
        }
        catch
        {
            return string.Empty;
        }
    }
}
