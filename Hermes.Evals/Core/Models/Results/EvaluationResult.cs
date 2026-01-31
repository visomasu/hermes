using Hermes.Evals.Core.Models.Enums;

namespace Hermes.Evals.Core.Models.Results;

/// <summary>
/// Represents the complete evaluation result for a scenario.
/// </summary>
public class EvaluationResult
{
    /// <summary>
    /// Name of the scenario that was evaluated.
    /// </summary>
    public string ScenarioName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the scenario passed overall (all turns passed).
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Overall score for the scenario (0.0 - 1.0).
    /// Calculated as average of all turn scores.
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// Execution mode used for this scenario (RestApi or DirectOrchestrator).
    /// </summary>
    public ExecutionMode ExecutionMode { get; set; }

    /// <summary>
    /// Data mode used for this scenario (Mock or Real).
    /// </summary>
    public DataMode DataMode { get; set; }

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Scenario start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Scenario end time.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Total execution duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Results for each turn in the scenario.
    /// </summary>
    public List<TurnResult> TurnResults { get; set; } = new();

    /// <summary>
    /// Aggregated metrics across all turns.
    /// </summary>
    public ScenarioMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Calculates overall metrics from turn results.
    /// </summary>
    public void CalculateOverallMetrics()
    {
        if (TurnResults.Count == 0)
        {
            Passed = false;
            OverallScore = 0.0;
            return;
        }

        // Calculate overall score as average of turn scores
        OverallScore = TurnResults.Average(t => t.OverallScore);

        // Scenario passes if all turns pass and average score > 0.5
        Passed = TurnResults.All(t => t.Success) && OverallScore >= 0.5;

        // Calculate dimension-specific metrics
        var toolSelectionScores = TurnResults
            .Where(t => t.Scores.ToolSelection.HasValue)
            .Select(t => t.Scores.ToolSelection!.Value)
            .ToList();

        var parameterExtractionScores = TurnResults
            .Where(t => t.Scores.ParameterExtraction.HasValue)
            .Select(t => t.Scores.ParameterExtraction!.Value)
            .ToList();

        var contextRetentionScores = TurnResults
            .Where(t => t.Scores.ContextRetention.HasValue)
            .Select(t => t.Scores.ContextRetention!.Value)
            .ToList();

        var responseQualityScores = TurnResults
            .Where(t => t.Scores.ResponseQuality.HasValue)
            .Select(t => t.Scores.ResponseQuality!.Value)
            .ToList();

        Metrics.ToolSelectionAccuracy = toolSelectionScores.Any() ? toolSelectionScores.Average() : null;
        Metrics.ParameterExtractionAccuracy = parameterExtractionScores.Any() ? parameterExtractionScores.Average() : null;
        Metrics.ContextRetentionScore = contextRetentionScores.Any() ? contextRetentionScores.Average() : null;
        Metrics.ResponseQualityScore = responseQualityScores.Any() ? responseQualityScores.Average() : null;

        Metrics.AverageExecutionTimeMs = TurnResults.Average(t => t.ExecutionTimeMs);
        Metrics.TotalTurns = TurnResults.Count;
        Metrics.PassedTurns = TurnResults.Count(t => t.Success);
        Metrics.FailedTurns = TurnResults.Count(t => !t.Success);
    }
}
