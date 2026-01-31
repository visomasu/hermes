namespace Hermes.Evals.Core.Models.Metrics;

/// <summary>
/// Summary statistics for evaluation run.
/// </summary>
public class EvaluationSummary
{
    /// <summary>
    /// Total number of scenarios executed.
    /// </summary>
    public int TotalScenarios { get; set; }

    /// <summary>
    /// Number of scenarios that passed.
    /// </summary>
    public int PassedScenarios { get; set; }

    /// <summary>
    /// Number of scenarios that failed.
    /// </summary>
    public int FailedScenarios { get; set; }

    /// <summary>
    /// Total number of turns executed across all scenarios.
    /// </summary>
    public int TotalTurns { get; set; }

    /// <summary>
    /// Success rate: PassedScenarios / TotalScenarios (0.0 - 1.0).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Overall score across all scenarios (0.0 - 1.0).
    /// </summary>
    public double OverallScore { get; set; }
}
