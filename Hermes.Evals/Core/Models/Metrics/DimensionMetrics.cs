namespace Hermes.Evals.Core.Models.Metrics;

/// <summary>
/// Aggregated metrics for each evaluation dimension.
/// </summary>
public class DimensionMetrics
{
    /// <summary>
    /// Tool selection accuracy: percentage of turns where correct tool/capability was selected (0.0 - 1.0).
    /// </summary>
    public double ToolSelectionAccuracy { get; set; }

    /// <summary>
    /// Parameter extraction accuracy: percentage of parameters correctly extracted (0.0 - 1.0).
    /// </summary>
    public double ParameterExtractionAccuracy { get; set; }

    /// <summary>
    /// Context retention score: how well information is retained across turns (0.0 - 1.0).
    /// </summary>
    public double ContextRetentionScore { get; set; }

    /// <summary>
    /// Response quality score: completeness and structure of responses (0.0 - 1.0).
    /// </summary>
    public double ResponseQualityScore { get; set; }
}
