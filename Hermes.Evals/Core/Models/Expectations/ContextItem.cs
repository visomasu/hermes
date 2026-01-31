namespace Hermes.Evals.Core.Models.Expectations;

/// <summary>
/// Represents a context item to remember.
/// </summary>
public class ContextItem
{
    /// <summary>
    /// Context key identifier (e.g., "lastFeatureId", "lastOperation").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Context value to store (e.g., "3097408", "newsletter").
    /// </summary>
    public object Value { get; set; } = string.Empty;
}
