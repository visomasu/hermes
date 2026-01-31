using Hermes.Evals.Core.Models.Scoring;

namespace Hermes.Evals.Core.Models.Results;

/// <summary>
/// Represents the evaluation result for a single conversation turn.
/// </summary>
public class TurnResult
{
    /// <summary>
    /// Turn number that was evaluated (1-indexed).
    /// </summary>
    public int TurnNumber { get; set; }

    /// <summary>
    /// Name of the evaluator that produced this result (for multi-evaluator scenarios).
    /// </summary>
    public string EvaluatorName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the turn passed all checks successfully.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Overall score for this turn (0.0 - 1.0).
    /// Calculated as weighted average of all evaluation dimensions.
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// Individual scores for each evaluation dimension.
    /// </summary>
    public EvaluationScores Scores { get; set; } = new();

    /// <summary>
    /// Individual checks performed with pass/fail status and details.
    /// Example: {"CorrectToolSelected": {"Passed": true, "Details": "Expected: AzureDevOpsTool, Actual: AzureDevOpsTool"}}
    /// </summary>
    public Dictionary<string, CheckResult> Checks { get; set; } = new();

    /// <summary>
    /// Error message if execution or evaluation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Captured metadata (tool called, parameters, response text, etc.).
    /// </summary>
    public Dictionary<string, object> CapturedMetadata { get; set; } = new();

    /// <summary>
    /// Adds a check result to this turn.
    /// </summary>
    public void AddCheck(string checkName, bool passed, string details)
    {
        Checks[checkName] = new CheckResult
        {
            Passed = passed,
            Details = details
        };
    }
}
