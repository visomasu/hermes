namespace Hermes.Evals.Core.Models.Scoring;

/// <summary>
/// Defines custom scoring weights for evaluation dimensions.
/// </summary>
public class ScoringWeights
{
    /// <summary>
    /// Weight for tool selection evaluation (default: 0.30).
    /// </summary>
    public double ToolSelection { get; set; } = 0.30;

    /// <summary>
    /// Weight for parameter extraction evaluation (default: 0.30).
    /// </summary>
    public double ParameterExtraction { get; set; } = 0.30;

    /// <summary>
    /// Weight for context retention evaluation (default: 0.25).
    /// </summary>
    public double ContextRetention { get; set; } = 0.25;

    /// <summary>
    /// Weight for response quality evaluation (default: 0.15).
    /// </summary>
    public double ResponseQuality { get; set; } = 0.15;

    /// <summary>
    /// Validates that weights sum to 1.0.
    /// </summary>
    public bool IsValid()
    {
        var total = ToolSelection + ParameterExtraction + ContextRetention + ResponseQuality;
        return Math.Abs(total - 1.0) < 0.001; // Allow small floating point errors
    }
}
