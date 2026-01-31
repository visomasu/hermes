namespace Hermes.Evals.Core.Models.Enums;

/// <summary>
/// Defines how the evaluation engine executes scenarios.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Execute via REST API HTTP calls to http://localhost:3978/api/hermes/v1.0/chat.
    /// Tests the full stack including HTTP layer. More realistic but slower.
    /// </summary>
    RestApi,

    /// <summary>
    /// Execute by calling HermesOrchestrator directly.
    /// Faster, easier to debug, but doesn't test REST layer.
    /// </summary>
    DirectOrchestrator
}
