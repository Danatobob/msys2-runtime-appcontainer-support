namespace AppContainerHarness.Models;

public enum ExpectMode
{
    Failing,
    Working
}

public sealed record ExpectedOutcome(
    int? ExpectedExitCode,
    string[] RequiredOutputPatterns,
    bool RequireCleanExit);

public sealed record Scenario(
    string Name,
    string Description,
    string Command,
    bool IsStartupProbe,
    ExpectedOutcome WorkingModeExpectation,
    ExpectedOutcome? FailingModeExpectation);

public sealed class LaunchResult
{
    public int? ExitCode { get; init; }
    public string Stdout { get; init; } = string.Empty;
    public string Stderr { get; init; } = string.Empty;
    public bool TimedOut { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed class ScenarioResult
{
    public required Scenario Scenario { get; init; }
    public required ExpectMode Mode { get; init; }
    public LaunchResult? Actual { get; init; }
    public bool Passed { get; init; }
    public string? FailureReason { get; init; }
}

public sealed class SuiteReport
{
    public DateTime RunTimestamp { get; init; } = DateTime.UtcNow;
    public string TargetDllPath { get; init; } = string.Empty;
    public ExpectMode Mode { get; init; }
    public List<ScenarioResult> Results { get; init; } = new();
    public bool OverallPassed => Results.Count > 0 && Results.All(r => r.Passed);
}
