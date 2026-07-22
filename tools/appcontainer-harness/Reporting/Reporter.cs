using System.Text.Json;
using AppContainerHarness.Models;

namespace AppContainerHarness.Reporting;

public static class Reporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static void PrintSummary(SuiteReport report, bool verbose)
    {
        Console.WriteLine($"AppContainer Harness Suite Report - {report.RunTimestamp:u}");
        Console.WriteLine($"Target DLL: {report.TargetDllPath}");
        Console.WriteLine($"Mode: {report.Mode}");
        Console.WriteLine(new string('-', 60));

        foreach (var r in report.Results)
        {
            string status = r.Passed ? "PASS" : "FAIL";
            Console.WriteLine($"[{status}] {r.Scenario.Name} - {r.Scenario.Description}");
            if (!r.Passed && r.FailureReason is not null)
            {
                Console.WriteLine($"       reason: {r.FailureReason}");
            }
            if (verbose && r.Actual is not null)
            {
                Console.WriteLine($"       exitCode={r.Actual.ExitCode?.ToString() ?? "null"} timedOut={r.Actual.TimedOut} duration={r.Actual.Duration}");
                Console.WriteLine($"       stdout: {Truncate(r.Actual.Stdout)}");
                Console.WriteLine($"       stderr: {Truncate(r.Actual.Stderr)}");
            }
        }

        Console.WriteLine(new string('-', 60));
        Console.WriteLine(report.OverallPassed ? "OVERALL: PASS" : "OVERALL: FAIL");
    }

    public static void WriteJsonReport(SuiteReport report, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string json = JsonSerializer.Serialize(report, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string Truncate(string s) => s.Length > 500 ? s[..500] + "... [truncated]" : s;
}
