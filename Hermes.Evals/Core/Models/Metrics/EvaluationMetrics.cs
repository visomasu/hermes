using Hermes.Evals.Core.Models.Results;

namespace Hermes.Evals.Core.Models.Metrics;

/// <summary>
/// Aggregated evaluation metrics across multiple scenarios.
/// Used for overall reporting and baseline comparison.
/// </summary>
public class EvaluationMetrics
{
    /// <summary>
    /// Summary statistics.
    /// </summary>
    public EvaluationSummary Summary { get; set; } = new();

    /// <summary>
    /// Aggregated dimension-specific metrics.
    /// </summary>
    public DimensionMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Performance metrics.
    /// </summary>
    public PerformanceMetrics Performance { get; set; } = new();

    /// <summary>
    /// List of all scenario results.
    /// </summary>
    public List<EvaluationResult> ScenarioResults { get; set; } = new();

    /// <summary>
    /// Calculates aggregated metrics from scenario results.
    /// </summary>
    public void CalculateFromScenarios(List<EvaluationResult> scenarios)
    {
        ScenarioResults = scenarios;

        if (scenarios.Count == 0)
        {
            return;
        }

        // Calculate summary
        Summary.TotalScenarios = scenarios.Count;
        Summary.PassedScenarios = scenarios.Count(s => s.Passed);
        Summary.FailedScenarios = scenarios.Count(s => !s.Passed);
        Summary.TotalTurns = scenarios.Sum(s => s.TurnResults.Count);
        Summary.SuccessRate = (double)Summary.PassedScenarios / Summary.TotalScenarios;
        Summary.OverallScore = scenarios.Average(s => s.OverallScore);

        // Calculate dimension metrics (average across all scenarios)
        var toolSelectionScores = scenarios
            .Where(s => s.Metrics.ToolSelectionAccuracy.HasValue)
            .Select(s => s.Metrics.ToolSelectionAccuracy!.Value)
            .ToList();

        var parameterExtractionScores = scenarios
            .Where(s => s.Metrics.ParameterExtractionAccuracy.HasValue)
            .Select(s => s.Metrics.ParameterExtractionAccuracy!.Value)
            .ToList();

        var contextRetentionScores = scenarios
            .Where(s => s.Metrics.ContextRetentionScore.HasValue)
            .Select(s => s.Metrics.ContextRetentionScore!.Value)
            .ToList();

        var responseQualityScores = scenarios
            .Where(s => s.Metrics.ResponseQualityScore.HasValue)
            .Select(s => s.Metrics.ResponseQualityScore!.Value)
            .ToList();

        Metrics.ToolSelectionAccuracy = toolSelectionScores.Any() ? toolSelectionScores.Average() : 0.0;
        Metrics.ParameterExtractionAccuracy = parameterExtractionScores.Any() ? parameterExtractionScores.Average() : 0.0;
        Metrics.ContextRetentionScore = contextRetentionScores.Any() ? contextRetentionScores.Average() : 0.0;
        Metrics.ResponseQualityScore = responseQualityScores.Any() ? responseQualityScores.Average() : 0.0;

        // Calculate performance metrics
        var executionTimes = scenarios.SelectMany(s => s.TurnResults.Select(t => t.ExecutionTimeMs)).ToList();
        if (executionTimes.Any())
        {
            Performance.AverageExecutionTimeMs = executionTimes.Average();
            Performance.P95ExecutionTimeMs = CalculatePercentile(executionTimes, 0.95);
            Performance.P99ExecutionTimeMs = CalculatePercentile(executionTimes, 0.99);
        }
    }

    /// <summary>
    /// Calculates the specified percentile from a list of values.
    /// </summary>
    private static long CalculatePercentile(List<long> values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        index = Math.Max(0, Math.Min(index, sorted.Count - 1));
        return sorted[index];
    }
}
