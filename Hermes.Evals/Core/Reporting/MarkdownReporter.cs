using System.Text;
using Hermes.Evals.Core.Models.Metrics;
using Microsoft.Extensions.Logging;

namespace Hermes.Evals.Core.Reporting;

/// <summary>
/// Generates human-readable Markdown report with detailed analysis.
/// Similar to INTEGRATION-TEST-REPORT.md format with executive summary and recommendations.
/// </summary>
public class MarkdownReporter : IReporter
{
    private readonly ILogger<MarkdownReporter> _logger;

    public MarkdownReporter(ILogger<MarkdownReporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a Markdown report.
    /// </summary>
    public async Task GenerateReportAsync(EvaluationMetrics metrics, string outputPath)
    {
        _logger.LogInformation("Generating Markdown report: {OutputPath}", outputPath);

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var markdown = new StringBuilder();

        // Header
        markdown.AppendLine("# Hermes Evaluation Report");
        markdown.AppendLine();
        markdown.AppendLine($"**Generated:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        markdown.AppendLine();

        // Executive Summary
        _AppendExecutiveSummary(markdown, metrics);
        markdown.AppendLine();

        // Metrics Table
        _AppendMetricsTable(markdown, metrics);
        markdown.AppendLine();

        // Performance Metrics
        _AppendPerformanceSection(markdown, metrics);
        markdown.AppendLine();

        // Detailed Results
        _AppendDetailedResults(markdown, metrics);
        markdown.AppendLine();

        // Recommendations
        _AppendRecommendations(markdown, metrics);
        markdown.AppendLine();

        await File.WriteAllTextAsync(outputPath, markdown.ToString());

        _logger.LogInformation("Markdown report saved: {OutputPath} ({Size} bytes)",
            outputPath, markdown.Length);
    }

    private void _AppendExecutiveSummary(StringBuilder md, EvaluationMetrics metrics)
    {
        md.AppendLine("## Executive Summary");
        md.AppendLine();

        var status = metrics.Summary.SuccessRate >= 0.8 ? "✅ **PASSING**" : "❌ **FAILING**";
        md.AppendLine($"**Status:** {status}");
        md.AppendLine();

        md.AppendLine($"- **Scenarios Passed:** {metrics.Summary.PassedScenarios}/{metrics.Summary.TotalScenarios} ({metrics.Summary.SuccessRate:P1})");
        md.AppendLine($"- **Overall Score:** {metrics.Summary.OverallScore:F3}");
        md.AppendLine($"- **Total Turns Executed:** {metrics.Summary.TotalTurns}");
        md.AppendLine($"- **Average Execution Time:** {metrics.Performance.AverageExecutionTimeMs:F0}ms per turn");
    }

    private void _AppendMetricsTable(StringBuilder md, EvaluationMetrics metrics)
    {
        md.AppendLine("## Evaluation Metrics");
        md.AppendLine();
        md.AppendLine("| Dimension | Score | Grade | Target |");
        md.AppendLine("|-----------|-------|-------|--------|");

        _AppendMetricRow(md, "Tool Selection", metrics.Metrics.ToolSelectionAccuracy, 0.95);
        _AppendMetricRow(md, "Parameter Extraction", metrics.Metrics.ParameterExtractionAccuracy, 0.98);
        _AppendMetricRow(md, "Context Retention", metrics.Metrics.ContextRetentionScore, 0.80);
        _AppendMetricRow(md, "Response Quality", metrics.Metrics.ResponseQualityScore, 0.75);
    }

    private void _AppendMetricRow(StringBuilder md, string dimension, double score, double target)
    {
        var grade = _GetScoreGrade(score);
        var status = score >= target ? "✅" : "⚠️";
        md.AppendLine($"| {dimension} | {score:F3} | {grade} {status} | {target:F2} |");
    }

    private void _AppendPerformanceSection(StringBuilder md, EvaluationMetrics metrics)
    {
        md.AppendLine("## Performance Metrics");
        md.AppendLine();
        md.AppendLine("| Metric | Value | Target |");
        md.AppendLine("|--------|-------|--------|");
        md.AppendLine($"| Average Execution Time | {metrics.Performance.AverageExecutionTimeMs:F0}ms | <1500ms |");
        md.AppendLine($"| P95 Execution Time | {metrics.Performance.P95ExecutionTimeMs}ms | <2500ms |");
        md.AppendLine($"| P99 Execution Time | {metrics.Performance.P99ExecutionTimeMs}ms | <3000ms |");
    }

    private void _AppendDetailedResults(StringBuilder md, EvaluationMetrics metrics)
    {
        md.AppendLine("## Detailed Scenario Results");
        md.AppendLine();

        foreach (var scenario in metrics.ScenarioResults)
        {
            var statusEmoji = scenario.Passed ? "✅" : "❌";
            md.AppendLine($"### {statusEmoji} {scenario.ScenarioName}");
            md.AppendLine();

            md.AppendLine($"- **Status:** {(scenario.Passed ? "PASSED" : "FAILED")}");
            md.AppendLine($"- **Overall Score:** {scenario.OverallScore:F3}");
            md.AppendLine($"- **Execution Mode:** {scenario.ExecutionMode}");
            md.AppendLine($"- **Data Mode:** {scenario.DataMode}");
            md.AppendLine($"- **Execution Time:** {scenario.ExecutionTimeMs}ms");
            md.AppendLine($"- **Turns:** {scenario.TurnResults.Count} ({scenario.Metrics.PassedTurns} passed, {scenario.Metrics.FailedTurns} failed)");
            md.AppendLine();

            // Dimension scores for this scenario
            md.AppendLine("**Dimension Scores:**");
            md.AppendLine();
            if (scenario.Metrics.ToolSelectionAccuracy.HasValue)
                md.AppendLine($"- Tool Selection: {scenario.Metrics.ToolSelectionAccuracy.Value:F3}");
            if (scenario.Metrics.ParameterExtractionAccuracy.HasValue)
                md.AppendLine($"- Parameter Extraction: {scenario.Metrics.ParameterExtractionAccuracy.Value:F3}");
            if (scenario.Metrics.ContextRetentionScore.HasValue)
                md.AppendLine($"- Context Retention: {scenario.Metrics.ContextRetentionScore.Value:F3}");
            if (scenario.Metrics.ResponseQualityScore.HasValue)
                md.AppendLine($"- Response Quality: {scenario.Metrics.ResponseQualityScore.Value:F3}");
            md.AppendLine();

            // Failed turn details
            var failedTurns = scenario.TurnResults.Where(t => !t.Success).ToList();
            if (failedTurns.Any())
            {
                md.AppendLine("**Failed Turns:**");
                md.AppendLine();
                foreach (var turn in failedTurns)
                {
                    md.AppendLine($"- **Turn {turn.TurnNumber}:** Score {turn.OverallScore:F3}");
                    var failedChecks = turn.Checks.Where(c => !c.Value.Passed).ToList();
                    if (failedChecks.Any())
                    {
                        foreach (var check in failedChecks)
                        {
                            md.AppendLine($"  - ❌ {check.Key}: {check.Value.Details}");
                        }
                    }
                }
                md.AppendLine();
            }
        }
    }

    private void _AppendRecommendations(StringBuilder md, EvaluationMetrics metrics)
    {
        md.AppendLine("## Recommendations");
        md.AppendLine();

        var recommendations = new List<string>();

        // Check each dimension
        if (metrics.Metrics.ToolSelectionAccuracy < 0.95)
        {
            recommendations.Add("🔧 **Tool Selection:** Review capability routing logic and instruction files. Tool selection accuracy is below target (95%).");
        }

        if (metrics.Metrics.ParameterExtractionAccuracy < 0.98)
        {
            recommendations.Add("🔧 **Parameter Extraction:** Improve parameter extraction prompts or add more examples to capability instructions.");
        }

        if (metrics.Metrics.ContextRetentionScore < 0.80)
        {
            recommendations.Add("🔧 **Context Retention:** Review conversation history handling and context management in multi-turn scenarios.");
        }

        if (metrics.Metrics.ResponseQualityScore < 0.75)
        {
            recommendations.Add("🔧 **Response Quality:** Improve response formatting and completeness in capability implementations.");
        }

        if (metrics.Performance.AverageExecutionTimeMs > 1500)
        {
            recommendations.Add("⚡ **Performance:** Average execution time exceeds target. Consider optimization or caching strategies.");
        }

        if (metrics.Summary.SuccessRate < 0.8)
        {
            recommendations.Add("⚠️ **Overall Success Rate:** Less than 80% of scenarios passing. Investigate failed scenarios for systemic issues.");
        }

        if (recommendations.Any())
        {
            foreach (var recommendation in recommendations)
            {
                md.AppendLine($"- {recommendation}");
            }
        }
        else
        {
            md.AppendLine("✅ All metrics meet or exceed targets. No immediate action required.");
        }

        md.AppendLine();
        md.AppendLine("---");
        md.AppendLine();
        md.AppendLine("🤖 *Generated with [Hermes.Evals](https://github.com/your-org/hermes)*");
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
