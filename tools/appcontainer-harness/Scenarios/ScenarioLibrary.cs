using AppContainerHarness.Models;

namespace AppContainerHarness.Scenarios;

public static class ScenarioLibrary
{
    // BR-1: only the startup probe carries a real FailingModeExpectation - every other
    // scenario would fail identically (for the same underlying reason) if startup itself
    // fails, so a separate "failing" expectation for them would not be independently meaningful.
    public static IReadOnlyList<Scenario> GetAllScenarios() => new[]
    {
        new Scenario(
            Name: "startup",
            Description: "Bare process startup - the actual known AppContainer bug site",
            Command: "true",
            IsStartupProbe: true,
            WorkingModeExpectation: new ExpectedOutcome(ExpectedExitCode: 0, RequiredOutputPatterns: Array.Empty<string>(), RequireCleanExit: true),
            FailingModeExpectation: new ExpectedOutcome(ExpectedExitCode: null, RequiredOutputPatterns: new[] { "NtCreateDirectoryObject", "0xC0000022" }, RequireCleanExit: false)),

        new Scenario(
            Name: "echo",
            Description: "Basic command execution",
            Command: "echo hello-from-scenario",
            IsStartupProbe: false,
            WorkingModeExpectation: new ExpectedOutcome(0, new[] { "hello-from-scenario" }, true),
            FailingModeExpectation: null),

        new Scenario(
            Name: "control-flow-script",
            Description: "Shell script with a loop and conditional",
            Command: "for i in 1 2 3; do if [ $i -eq 2 ]; then echo control-flow-ok; fi; done",
            IsStartupProbe: false,
            WorkingModeExpectation: new ExpectedOutcome(0, new[] { "control-flow-ok" }, true),
            FailingModeExpectation: null),

        new Scenario(
            Name: "fork-subshell",
            Description: "Subshell spawn (fork/exec emulation)",
            Command: "(echo child-subshell-ok)",
            IsStartupProbe: false,
            WorkingModeExpectation: new ExpectedOutcome(0, new[] { "child-subshell-ok" }, true),
            FailingModeExpectation: null),

        new Scenario(
            Name: "file-io",
            Description: "Write/read/delete a file in the granted scratch data folder",
            // Uses a path relative to cwd (ProcessLauncher.Launch() sets the child's working
            // directory to the granted data folder itself) rather than an absolute /e/... MSYS
            // path, since this minimally-staged bash instance (just bash.exe + msys-2.0.dll
            // copied into a flat folder, no /etc/fstab or normal mount-table init) may not have
            // Cygwin's usual drive-letter auto-mount ("/e" -> "E:\") established correctly.
            Command: "echo file-io-ok > harness-test.txt && cat harness-test.txt && rm harness-test.txt",
            IsStartupProbe: false,
            WorkingModeExpectation: new ExpectedOutcome(0, new[] { "file-io-ok" }, true),
            FailingModeExpectation: null),

        new Scenario(
            Name: "env-vars",
            Description: "Environment variable set/read",
            Command: "export HARNESS_VAR=env-var-ok && echo $HARNESS_VAR",
            IsStartupProbe: false,
            WorkingModeExpectation: new ExpectedOutcome(0, new[] { "env-var-ok" }, true),
            FailingModeExpectation: null),

        new Scenario(
            Name: "pipes",
            Description: "Pipe/redirection between two commands",
            Command: "echo foo | tr a-z A-Z",
            IsStartupProbe: false,
            WorkingModeExpectation: new ExpectedOutcome(0, new[] { "FOO" }, true),
            FailingModeExpectation: null),
    };

    public static Scenario? GetScenario(string name) =>
        GetAllScenarios().FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
