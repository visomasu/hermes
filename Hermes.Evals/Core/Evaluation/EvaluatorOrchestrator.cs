using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Expectations;
using Hermes.Evals.Core.Models.Results;
using Hermes.Evals.Core.Models.Scoring;

namespace Hermes.Evals.Core.Evaluation;

/// <summary>
/// Orchestrates multiple evaluators and aggregates their results into a single turn result.
/// </summary>
public class EvaluatorOrchestrator
{
    private readonly List<IEvaluator> _evaluators;
    private readonly ScoringWeights _weights;

    /// <summary>
    /// Creates an evaluator orchestrator with the specified evaluators and weights.
    /// </summary>
    /// <param name="evaluators">List of evaluators to use.</param>
    /// <param name="weights">Optional custom scoring weights. If null, uses default weights from evaluators.</param>
    public EvaluatorOrchestrator(List<IEvaluator> evaluators, ScoringWeights? weights = null)
    {
        _evaluators = evaluators ?? throw new ArgumentNullException(nameof(evaluators));
        _weights = weights ?? new ScoringWeights();

        // Validate weights sum to 1.0
        if (weights != null && !weights.IsValid())
        {
            throw new ArgumentException("Scoring weights must sum to 1.0", nameof(weights));
        }
    }

    /// <summary>
    /// Evaluates a single turn using all evaluators and aggregates the results.
    /// </summary>
    /// <param name="turn">The conversation turn to evaluate.</param>
    /// <param name="expectation">Expected outcomes for this turn.</param>
    /// <param name="capturedMetadata">Metadata captured during execution.</param>
    /// <returns>Aggregated turn result with weighted score.</returns>
    public async Task<TurnResult> EvaluateAsync(
        ConversationTurn turn,
        TurnExpectation expectation,
        Dictionary<string, object> capturedMetadata)
    {
        var aggregatedResult = new TurnResult
        {
            TurnNumber = turn.TurnNumber,
            EvaluatorName = "Aggregated",
            CapturedMetadata = capturedMetadata
        };

        // Run all evaluators
        var evaluatorTasks = _evaluators.Select(evaluator =>
            evaluator.EvaluateAsync(turn, expectation, capturedMetadata));

        var evaluatorResults = await Task.WhenAll(evaluatorTasks);

        // Aggregate scores from all evaluators
        double totalWeightedScore = 0.0;
        double totalWeight = 0.0;

        foreach (var evaluatorResult in evaluatorResults)
        {
            // Get weight for this evaluator
            var weight = GetWeightForEvaluator(evaluatorResult.EvaluatorName);

            // Add weighted score
            totalWeightedScore += evaluatorResult.OverallScore * weight;
            totalWeight += weight;

            // Merge checks from this evaluator
            foreach (var check in evaluatorResult.Checks)
            {
                aggregatedResult.Checks[$"{evaluatorResult.EvaluatorName}_{check.Key}"] = check.Value;
            }

            // Copy dimension-specific scores
            if (evaluatorResult.Scores.ToolSelection.HasValue)
            {
                aggregatedResult.Scores.ToolSelection = evaluatorResult.Scores.ToolSelection;
            }

            if (evaluatorResult.Scores.ParameterExtraction.HasValue)
            {
                aggregatedResult.Scores.ParameterExtraction = evaluatorResult.Scores.ParameterExtraction;
            }

            if (evaluatorResult.Scores.ContextRetention.HasValue)
            {
                aggregatedResult.Scores.ContextRetention = evaluatorResult.Scores.ContextRetention;
            }

            if (evaluatorResult.Scores.ResponseQuality.HasValue)
            {
                aggregatedResult.Scores.ResponseQuality = evaluatorResult.Scores.ResponseQuality;
            }

            // If any evaluator failed critically, mark overall as failed
            if (!evaluatorResult.Success && evaluatorResult.OverallScore < 0.5)
            {
                aggregatedResult.Success = false;
            }
        }

        // Calculate overall weighted score
        aggregatedResult.OverallScore = totalWeight > 0 ? totalWeightedScore / totalWeight : 0.0;

        // Overall success if score >= 0.5 and no critical failures
        if (aggregatedResult.OverallScore >= 0.5 && aggregatedResult.Success)
        {
            aggregatedResult.Success = true;
        }
        else
        {
            aggregatedResult.Success = false;
        }

        return aggregatedResult;
    }

    /// <summary>
    /// Gets the weight for a specific evaluator by name.
    /// </summary>
    private double GetWeightForEvaluator(string evaluatorName)
    {
        return evaluatorName switch
        {
            "ToolSelection" => _weights.ToolSelection,
            "ParameterExtraction" => _weights.ParameterExtraction,
            "ContextRetention" => _weights.ContextRetention,
            "ResponseQuality" => _weights.ResponseQuality,
            _ => 0.0
        };
    }

    /// <summary>
    /// Resets stateful evaluators (like ContextRetentionEvaluator) for a new scenario.
    /// Call this when starting a new scenario to clear conversation context.
    /// </summary>
    public void ResetForNewScenario()
    {
        foreach (var evaluator in _evaluators)
        {
            if (evaluator is ContextRetentionEvaluator contextEvaluator)
            {
                contextEvaluator.Reset();
            }
        }
    }
}
