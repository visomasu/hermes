namespace Hermes.Evals.Core.Models.Expectations;

/// <summary>
/// Defines expectations for response quality.
/// </summary>
public class ResponseQualityExpectation
{
    /// <summary>
    /// Content that MUST be present in the response.
    /// Example: ["Feature #3097408", "Progress", "Timeline"]
    /// </summary>
    public List<string>? MustContain { get; set; }

    /// <summary>
    /// Content that MUST NOT be present in the response.
    /// Example: ["error", "failed"]
    /// </summary>
    public List<string>? MustNotContain { get; set; }

    /// <summary>
    /// Minimum response length in characters.
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Expected sections or structure elements (e.g., ["Summary", "Key Updates"]).
    /// </summary>
    public List<string>? Structure { get; set; }
}
