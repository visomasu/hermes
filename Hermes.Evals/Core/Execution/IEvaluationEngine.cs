using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Results;

namespace Hermes.Evals.Core.Execution;

/// <summary>
/// Main execution interface for the evaluation framework.
/// Orchestrates scenario execution, aggregates results, and provides progress callbacks.
/// </summary>
public interface IEvaluationEngine
{
    /// <summary>
    /// Runs multiple evaluation scenarios sequentially or in parallel.
    /// </summary>
    /// <param name="scenarios">The scenarios to execute.</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>List of evaluation results, one per scenario.</returns>
    Task<List<EvaluationResult>> RunScenariosAsync(
        IEnumerable<EvaluationScenario> scenarios,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a single evaluation scenario.
    /// </summary>
    /// <param name="scenario">The scenario to execute.</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>Evaluation result for the scenario.</returns>
    Task<EvaluationResult> RunScenarioAsync(
        EvaluationScenario scenario,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when a turn completes evaluation.
    /// </summary>
    event EventHandler<TurnCompletedEventArgs>? TurnCompleted;

    /// <summary>
    /// Event raised when a scenario completes evaluation.
    /// </summary>
    event EventHandler<ScenarioCompletedEventArgs>? ScenarioCompleted;
}

/// <summary>
/// Event arguments for turn completion.
/// </summary>
public class TurnCompletedEventArgs : EventArgs
{
    public string ScenarioName { get; init; } = string.Empty;
    public int TurnNumber { get; init; }
    public TurnResult Result { get; init; } = new();
}

/// <summary>
/// Event arguments for scenario completion.
/// </summary>
public class ScenarioCompletedEventArgs : EventArgs
{
    public EvaluationResult Result { get; init; } = new();
}
