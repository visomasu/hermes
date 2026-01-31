namespace Hermes.Evals.Core.Models.Expectations;

/// <summary>
/// Defines expectations for context retention across conversation turns.
/// </summary>
public class ContextRetentionExpectation
{
    /// <summary>
    /// Context items that should be remembered from this turn for future turns.
    /// Example: [{"key": "lastFeatureId", "value": "3097408"}]
    /// </summary>
    public List<ContextItem>? ShouldRemember { get; set; }

    /// <summary>
    /// Verification that context from previous turns was used in parameters.
    /// Example: [{"contextKey": "lastFeatureId", "usedInParameter": "workItemId"}]
    /// </summary>
    public List<ContextUsageCheck>? VerifyContextUsage { get; set; }

    /// <summary>
    /// Optional description explaining what context retention is being tested.
    /// </summary>
    public string? Description { get; set; }
}
