using Hermes.Evals.Core.Models.Expectations;

namespace Hermes.Evals.Core.Models.Scenario;

/// <summary>
/// Represents a single turn in a multi-turn conversation evaluation scenario.
/// </summary>
public class ConversationTurn
{
    /// <summary>
    /// Turn number within the scenario (1-indexed).
    /// </summary>
    public int TurnNumber { get; set; }

    /// <summary>
    /// User input text for this turn (e.g., "generate a newsletter for feature 3097408").
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Expected outcomes for this turn across all evaluation dimensions.
    /// </summary>
    public TurnExpectation Expectations { get; set; } = new();

    /// <summary>
    /// Metadata captured during execution (tool called, parameters, response time, etc.).
    /// Populated by the execution engine during test run.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
