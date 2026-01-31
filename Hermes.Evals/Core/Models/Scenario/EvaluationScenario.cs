using Hermes.Evals.Core.Models.Enums;
using Hermes.Evals.Core.Models.Scoring;

namespace Hermes.Evals.Core.Models.Scenario;

/// <summary>
/// Represents a complete evaluation scenario with multiple conversation turns.
/// This is the root model that defines an entire test case.
/// </summary>
public class EvaluationScenario
{
    /// <summary>
    /// Unique name for the scenario (e.g., "Newsletter Generation with Follow-up Hierarchy Validation").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what this scenario tests.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tags for categorization and filtering (e.g., ["newsletter", "hierarchy", "context-retention"]).
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Execution mode: RestApi or DirectOrchestrator.
    /// </summary>
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.RestApi;

    /// <summary>
    /// Data mode: Mock or Real.
    /// </summary>
    public DataMode DataMode { get; set; } = DataMode.Mock;

    /// <summary>
    /// Setup configuration including userId, sessionId, and mock data.
    /// </summary>
    public ScenarioSetup Setup { get; set; } = new();

    /// <summary>
    /// Ordered list of conversation turns to execute.
    /// </summary>
    public List<ConversationTurn> Turns { get; set; } = new();

    /// <summary>
    /// Weighted scoring configuration for this scenario.
    /// If not specified, uses default weights (Tool:30%, Params:30%, Context:25%, Quality:15%).
    /// </summary>
    public ScoringWeights? Scoring { get; set; }

    /// <summary>
    /// If true, stop executing remaining turns when a turn fails critically (score < 0.5).
    /// Default: false (continue all turns even if some fail).
    /// </summary>
    public bool StopOnFailure { get; set; } = false;
}
