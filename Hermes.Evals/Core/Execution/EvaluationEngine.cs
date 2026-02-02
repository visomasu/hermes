using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Results;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Hermes.Evals.Core.Execution;

/// <summary>
/// Main evaluation engine that orchestrates scenario execution.
/// Supports sequential and parallel execution with progress callbacks.
/// </summary>
public class EvaluationEngine : IEvaluationEngine
{
    private readonly ConversationRunner _conversationRunner;
    private readonly ILogger<EvaluationEngine> _logger;

    /// <summary>
    /// Event raised when a turn completes evaluation.
    /// </summary>
    public event EventHandler<TurnCompletedEventArgs>? TurnCompleted;

    /// <summary>
    /// Event raised when a scenario completes evaluation.
    /// </summary>
    public event EventHandler<ScenarioCompletedEventArgs>? ScenarioCompleted;

    public EvaluationEngine(
        ConversationRunner conversationRunner,
        ILogger<EvaluationEngine> logger)
    {
        _conversationRunner = conversationRunner;
        _logger = logger;
    }

    /// <summary>
    /// Runs multiple evaluation scenarios sequentially.
    /// </summary>
    /// <param name="scenarios">The scenarios to execute.</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>List of evaluation results, one per scenario.</returns>
    public async Task<List<EvaluationResult>> RunScenariosAsync(
        IEnumerable<EvaluationScenario> scenarios,
        CancellationToken cancellationToken = default)
    {
        var scenarioList = scenarios.ToList();
        _logger.LogInformation("Starting evaluation of {ScenarioCount} scenarios", scenarioList.Count);

        var stopwatch = Stopwatch.StartNew();
        var results = new List<EvaluationResult>();

        foreach (var scenario in scenarioList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Scenario execution cancelled after {CompletedCount}/{TotalCount} scenarios",
                    results.Count, scenarioList.Count);
                break;
            }

            try
            {
                var result = await RunScenarioAsync(scenario, cancellationToken);
                results.Add(result);

                // Add delay between scenarios to avoid Azure OpenAI rate limiting
                // Skip delay after last scenario
                if (results.Count < scenarioList.Count)
                {
                    var delayMs = 2000; // 2 second delay between scenarios
                    _logger.LogDebug("Waiting {DelayMs}ms before next scenario to avoid rate limiting", delayMs);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute scenario: {ScenarioName}", scenario.Name);

                // Create failed result
                var failedResult = new EvaluationResult
                {
                    ScenarioName = scenario.Name,
                    ExecutionMode = scenario.ExecutionMode,
                    DataMode = scenario.DataMode,
                    Passed = false,
                    OverallScore = 0.0
                };

                // Add error details
                var errorTurnResult = new TurnResult
                {
                    TurnNumber = 0,
                    EvaluatorName = "EvaluationEngine",
                    Success = false,
                    OverallScore = 0.0,
                    CapturedMetadata = new Dictionary<string, object> { ["error"] = ex.Message }
                };
                errorTurnResult.AddCheck("ScenarioExecution", false, $"Scenario failed: {ex.Message}");
                failedResult.TurnResults.Add(errorTurnResult);

                results.Add(failedResult);
            }
        }

        stopwatch.Stop();
        _logger.LogInformation("Completed evaluation of {ScenarioCount} scenarios in {ElapsedMs}ms",
            results.Count, stopwatch.ElapsedMilliseconds);

        return results;
    }

    /// <summary>
    /// Runs a single evaluation scenario.
    /// </summary>
    /// <param name="scenario">The scenario to execute.</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>Evaluation result for the scenario.</returns>
    public async Task<EvaluationResult> RunScenarioAsync(
        EvaluationScenario scenario,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running scenario: {ScenarioName}", scenario.Name);

        // Subscribe to conversation runner events if we have listeners
        if (TurnCompleted != null)
        {
            // Note: ConversationRunner doesn't expose events yet
            // We could enhance it to raise events per turn
            // For now, we'll raise events after scenario completes
        }

        var result = await _conversationRunner.RunScenarioAsync(scenario, cancellationToken);

        // Raise scenario completed event
        ScenarioCompleted?.Invoke(this, new ScenarioCompletedEventArgs { Result = result });

        // Raise turn completed events for each turn (retroactive)
        if (TurnCompleted != null)
        {
            foreach (var turnResult in result.TurnResults)
            {
                TurnCompleted.Invoke(this, new TurnCompletedEventArgs
                {
                    ScenarioName = scenario.Name,
                    TurnNumber = turnResult.TurnNumber,
                    Result = turnResult
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Runs multiple evaluation scenarios in parallel (experimental).
    /// Use with caution as parallel execution may cause resource contention.
    /// </summary>
    /// <param name="scenarios">The scenarios to execute.</param>
    /// <param name="maxParallelism">Maximum number of parallel executions.</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>List of evaluation results, one per scenario.</returns>
    public async Task<List<EvaluationResult>> RunScenariosInParallelAsync(
        IEnumerable<EvaluationScenario> scenarios,
        int maxParallelism = 3,
        CancellationToken cancellationToken = default)
    {
        var scenarioList = scenarios.ToList();
        _logger.LogInformation("Starting parallel evaluation of {ScenarioCount} scenarios (MaxParallelism: {MaxParallelism})",
            scenarioList.Count, maxParallelism);

        var stopwatch = Stopwatch.StartNew();

        // Use SemaphoreSlim to limit parallelism
        using var semaphore = new SemaphoreSlim(maxParallelism);
        var tasks = scenarioList.Select(async scenario =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await RunScenarioAsync(scenario, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute scenario: {ScenarioName}", scenario.Name);

                // Create failed result
                return new EvaluationResult
                {
                    ScenarioName = scenario.Name,
                    ExecutionMode = scenario.ExecutionMode,
                    DataMode = scenario.DataMode,
                    Passed = false,
                    OverallScore = 0.0,
                    TurnResults = new List<TurnResult>
                    {
                        new TurnResult
                        {
                            TurnNumber = 0,
                            EvaluatorName = "EvaluationEngine",
                            Success = false,
                            OverallScore = 0.0,
                            CapturedMetadata = new Dictionary<string, object> { ["error"] = ex.Message }
                        }
                    }
                };
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();
        _logger.LogInformation("Completed parallel evaluation of {ScenarioCount} scenarios in {ElapsedMs}ms",
            results.Length, stopwatch.ElapsedMilliseconds);

        return results.ToList();
    }
}
