namespace Hermes.Orchestrator
{
	/// <summary>
	/// Interface for agent orchestrators that handle user queries and orchestrate tool operations.
	/// </summary>
	public interface IAgentOrchestrator
	{
		/// <summary>
		/// Orchestrates operations based on the user's query and returns the response.
		/// </summary>
		/// <param name="sessionId">Logical session or conversation identifier used to scope history.</param>
		/// <param name="query">The input query from the user.</param>
		/// <returns>Response as a string.</returns>
		Task<string> OrchestrateAsync(string sessionId, string query);

		/// <summary>
		/// Orchestrates operations based on the user's query and returns the response, with optional progress callback.
		/// </summary>
		/// <param name="sessionId">Logical session or conversation identifier used to scope history.</param>
		/// <param name="query">The input query from the user.</param>
		/// <param name="progressCallback">Optional callback invoked with status updates during orchestration.</param>
		/// <returns>Response as a string.</returns>
		Task<string> OrchestrateAsync(string sessionId, string query, Action<string>? progressCallback = null);
	}
}
