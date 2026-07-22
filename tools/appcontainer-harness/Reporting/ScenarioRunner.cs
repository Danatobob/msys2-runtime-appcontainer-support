using AppContainerHarness.Launching;
using AppContainerHarness.Models;
using AppContainerHarness.Profiles;

namespace AppContainerHarness.Reporting;

internal static class ScenarioRunner
{
    public static SuiteReport RunSuite(
        AppContainerProfile profile,
        string bashExePath,
        string dataFolder,
        string targetDllPath,
        ExpectMode mode,
        IEnumerable<Scenario> scenarios)
    {
        var report = new SuiteReport
        {
            TargetDllPath = targetDllPath,
            Mode = mode,
        };

        foreach (var scenario in scenarios)
        {
            report.Results.Add(RunOne(profile, bashExePath, dataFolder, scenario, mode));
        }

        return report;
    }

    private static ScenarioResult RunOne(AppContainerProfile profile, string bashExePath, string dataFolder, Scenario scenario, ExpectMode mode)
    {
        // BR-1: in Failing mode, only the startup probe is meaningful.
        if (mode == ExpectMode.Failing && !scenario.IsStartupProbe)
        {
            return new ScenarioResult
            {
                Scenario = scenario,
                Mode = mode,
                Actual = null,
                Passed = false,
                FailureReason = "not applicable in failing mode",
            };
        }

        ExpectedOutcome? expectation = mode == ExpectMode.Failing ? scenario.FailingModeExpectation : scenario.WorkingModeExpectation;
        if (expectation is null)
        {
            return new ScenarioResult
            {
                Scenario = scenario,
                Mode = mode,
                Actual = null,
                Passed = false,
                FailureReason = "no expectation defined for this mode (Scenario Library authoring defect - BR-2)",
            };
        }

        LaunchResult actual;
        try
        {
            actual = ProcessLauncher.Launch(profile, bashExePath, scenario.Command, dataFolder);
        }
        catch (Exception ex)
        {
            // Reliability pattern: report always written, even on unexpected exception (nfr-design-patterns.md)
            return new ScenarioResult
            {
                Scenario = scenario,
                Mode = mode,
                Actual = null,
                Passed = false,
                FailureReason = $"unexpected exception: {ex.Message}",
            };
        }

        var (passed, reason) = Evaluate(actual, expectation);
        return new ScenarioResult
        {
            Scenario = scenario,
            Mode = mode,
            Actual = actual,
            Passed = passed,
            FailureReason = reason,
        };
    }

    // BR-3: exit code + output pattern matching (not exact string equality).
    private static (bool Passed, string? Reason) Evaluate(LaunchResult actual, ExpectedOutcome expected)
    {
        if (actual.TimedOut)
        {
            return (false, "timeout"); // BR-4
        }

        if (expected.RequireCleanExit && expected.ExpectedExitCode.HasValue && actual.ExitCode != expected.ExpectedExitCode)
        {
            return (false, $"exit code mismatch: expected {expected.ExpectedExitCode}, got {actual.ExitCode?.ToString() ?? "null"}");
        }

        string combinedOutput = actual.Stdout + "\n" + actual.Stderr;
        foreach (var pattern in expected.RequiredOutputPatterns)
        {
            if (!combinedOutput.Contains(pattern, StringComparison.Ordinal))
            {
                return (false, $"missing required pattern: {pattern}");
            }
        }

        return (true, null);
    }
}
