namespace Hermes.Evals.Core.Models.Expectations;

/// <summary>
/// Represents a check for context usage in parameters.
/// </summary>
public class ContextUsageCheck
{
    /// <summary>
    /// Context key that should have been used (e.g., "lastFeatureId").
    /// </summary>
    public string ContextKey { get; set; } = string.Empty;

    /// <summary>
    /// Parameter name where the context value should appear (e.g., "workItemId").
    /// </summary>
    public string UsedInParameter { get; set; } = string.Empty;
}
