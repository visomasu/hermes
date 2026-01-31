namespace Hermes.Evals.Core.Models.Scoring;

/// <summary>
/// Scores for each evaluation dimension.
/// </summary>
public class EvaluationScores
{
    /// <summary>
    /// Tool selection score (0.0 - 1.0). Null if not evaluated.
    /// </summary>
    public double? ToolSelection { get; set; }

    /// <summary>
    /// Parameter extraction score (0.0 - 1.0). Null if not evaluated.
    /// </summary>
    public double? ParameterExtraction { get; set; }

    /// <summary>
    /// Context retention score (0.0 - 1.0). Null if not evaluated.
    /// </summary>
    public double? ContextRetention { get; set; }

    /// <summary>
    /// Response quality score (0.0 - 1.0). Null if not evaluated.
    /// </summary>
    public double? ResponseQuality { get; set; }
}
