using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Expectations;
using Hermes.Evals.Core.Models.Results;

namespace Hermes.Evals.Core.Evaluation;

/// <summary>
/// Base interface for all evaluators.
/// Each evaluator assesses a specific dimension of LLM performance (tool selection, parameters, context, quality).
/// </summary>
public interface IEvaluator
{
    /// <summary>
    /// Name of this evaluator (e.g., "ToolSelection", "ParameterExtraction").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Weight of this evaluator in the overall score calculation (0.0 - 1.0).
    /// Default weights: Tool=0.30, Params=0.30, Context=0.25, Quality=0.15.
    /// </summary>
    double Weight { get; }

    /// <summary>
    /// Evaluates a single conversation turn against its expectations.
    /// </summary>
    /// <param name="turn">The conversation turn being evaluated.</param>
    /// <param name="expectation">Expected outcomes for this turn.</param>
    /// <param name="capturedMetadata">Metadata captured during execution (tool, params, response, etc.).</param>
    /// <returns>Turn result with score and detailed checks.</returns>
    Task<TurnResult> EvaluateAsync(
        ConversationTurn turn,
        TurnExpectation expectation,
        Dictionary<string, object> capturedMetadata);
}
