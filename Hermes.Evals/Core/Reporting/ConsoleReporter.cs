using Hermes.Evals.Core.Models.Metrics;
using Microsoft.Extensions.Logging;

namespace Hermes.Evals.Core.Reporting;

/// <summary>
/// Generates console output with xUnit-style pass/fail summary.
/// Provides real-time progress updates during evaluation execution.
/// </summary>
public class ConsoleReporter : IReporter
{
    private readonly ILogger<ConsoleReporter> _logger;

    public ConsoleReporter(ILogger<ConsoleReporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a console summary report.
    /// </summary>
    public Task GenerateReportAsync(EvaluationMetrics metrics, string outputPath)
    {
        _logger.LogInformation("Generating console report");

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  HERMES EVALUATION RESULTS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();

        // Summary
        _PrintSummary(metrics);
        Console.WriteLine();

        // Dimension Metrics
        _PrintDimensionMetrics(metrics);
        Console.WriteLine();

        // Performance Metrics
        _PrintPerformanceMetrics(metrics);
        Console.WriteLine();

        // Scenario Results
        _PrintScenarioResults(metrics);
        Console.WriteLine();

        // Final Status
        _PrintFinalStatus(metrics);
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();

        return Task.CompletedTask;
    }

    private void _PrintSummary(EvaluationMetrics metrics)
    {
        Console.WriteLine("SUMMARY");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        Console.WriteLine($"  Total Scenarios:    {metrics.Summary.TotalScenarios}");
        Console.WriteLine($"  Passed:             {metrics.Summary.PassedScenarios} ({metrics.Summary.SuccessRate:P1})");
        Console.WriteLine($"  Failed:             {metrics.Summary.FailedScenarios}");
        Console.WriteLine($"  Total Turns:        {metrics.Summary.TotalTurns}");
        Console.WriteLine($"  Overall Score:      {metrics.Summary.OverallScore:F3} ({_GetScoreGrade(metrics.Summary.OverallScore)})");
    }

    private void _PrintDimensionMetrics(EvaluationMetrics metrics)
    {
        Console.WriteLine("DIMENSION METRICS");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        Console.WriteLine($"  Tool Selection:     {metrics.Metrics.ToolSelectionAccuracy:F3} ({_GetScoreGrade(metrics.Metrics.ToolSelectionAccuracy)})");
        Console.WriteLine($"  Parameter Extract:  {metrics.Metrics.ParameterExtractionAccuracy:F3} ({_GetScoreGrade(metrics.Metrics.ParameterExtractionAccuracy)})");
        Console.WriteLine($"  Context Retention:  {metrics.Metrics.ContextRetentionScore:F3} ({_GetScoreGrade(metrics.Metrics.ContextRetentionScore)})");
        Console.WriteLine($"  Response Quality:   {metrics.Metrics.ResponseQualityScore:F3} ({_GetScoreGrade(metrics.Metrics.ResponseQualityScore)})");
    }

    private void _PrintPerformanceMetrics(EvaluationMetrics metrics)
    {
        Console.WriteLine("PERFORMANCE");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        Console.WriteLine($"  Average Time:       {metrics.Performance.AverageExecutionTimeMs:F0}ms");
        Console.WriteLine($"  P95 Time:           {metrics.Performance.P95ExecutionTimeMs}ms");
        Console.WriteLine($"  P99 Time:           {metrics.Performance.P99ExecutionTimeMs}ms");
    }

    private void _PrintScenarioResults(EvaluationMetrics metrics)
    {
        Console.WriteLine("SCENARIO RESULTS");
        Console.WriteLine("───────────────────────────────────────────────────────────────");

        foreach (var scenario in metrics.ScenarioResults)
        {
            var status = scenario.Passed ? "PASS" : "FAIL";
            var statusColor = scenario.Passed ? "\u001b[32m" : "\u001b[31m"; // Green or Red
            var resetColor = "\u001b[0m";

            Console.WriteLine($"  [{statusColor}{status}{resetColor}] {scenario.ScenarioName}");
            Console.WriteLine($"        Score: {scenario.OverallScore:F3} | Turns: {scenario.TurnResults.Count} | Time: {scenario.ExecutionTimeMs}ms");

            // Show failed turn details
            if (!scenario.Passed)
            {
                var failedTurns = scenario.TurnResults.Where(t => !t.Success).ToList();
                if (failedTurns.Any())
                {
                    Console.WriteLine($"        Failed Turns: {string.Join(", ", failedTurns.Select(t => $"#{t.TurnNumber}"))}");
                }
            }
        }
    }

    private void _PrintFinalStatus(EvaluationMetrics metrics)
    {
        var allPassed = metrics.Summary.PassedScenarios == metrics.Summary.TotalScenarios;
        var status = allPassed ? "✓ ALL TESTS PASSED" : "✗ SOME TESTS FAILED";
        var statusColor = allPassed ? "\u001b[32m" : "\u001b[31m"; // Green or Red
        var resetColor = "\u001b[0m";

        Console.WriteLine($"{statusColor}{status}{resetColor}");
    }

    private string _GetScoreGrade(double score)
    {
        return score switch
        {
            >= 0.95 => "Excellent",
            >= 0.85 => "Good",
            >= 0.70 => "Fair",
            >= 0.50 => "Poor",
            _ => "Failing"
        };
    }
}
