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
		/// <param name="query">The input query from the user.</param>
		/// <returns>Response as a string.</returns>
		Task<string> OrchestrateAsync(string query);
	}
}
