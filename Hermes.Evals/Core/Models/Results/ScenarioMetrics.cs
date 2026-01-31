namespace Hermes.Evals.Core.Models.Results;

/// <summary>
/// Aggregated metrics for a scenario.
/// </summary>
public class ScenarioMetrics
{
    /// <summary>
    /// Average tool selection accuracy across all turns (0.0 - 1.0). Null if not evaluated.
    /// </summary>
    public double? ToolSelectionAccuracy { get; set; }

    /// <summary>
    /// Average parameter extraction accuracy across all turns (0.0 - 1.0). Null if not evaluated.
    /// </summary>
    public double? ParameterExtractionAccuracy { get; set; }

    /// <summary>
    /// Average context retention score across all turns (0.0 - 1.0). Null if not evaluated.
    /// </summary>
    public double? ContextRetentionScore { get; set; }

    /// <summary>
    /// Average response quality score across all turns (0.0 - 1.0). Null if not evaluated.
    /// </summary>
    public double? ResponseQualityScore { get; set; }

    /// <summary>
    /// Average execution time per turn in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// Total number of turns executed.
    /// </summary>
    public int TotalTurns { get; set; }

    /// <summary>
    /// Number of turns that passed.
    /// </summary>
    public int PassedTurns { get; set; }

    /// <summary>
    /// Number of turns that failed.
    /// </summary>
    public int FailedTurns { get; set; }
}
