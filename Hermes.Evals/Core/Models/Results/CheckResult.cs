namespace Hermes.Evals.Core.Models.Results;

/// <summary>
/// Result of an individual check within a turn evaluation.
/// </summary>
public class CheckResult
{
    /// <summary>
    /// Whether the check passed.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Details about the check result (e.g., "Expected: X, Actual: Y").
    /// </summary>
    public string Details { get; set; } = string.Empty;
}
