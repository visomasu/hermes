using Hermes.Storage.Repositories.ConversationHistory;

namespace Hermes.Orchestrator.Context
{
	/// <summary>
	/// Interface for selecting relevant conversation messages for context window.
	/// </summary>
	public interface IConversationContextSelector
	{
		/// <summary>
		/// Selects relevant conversation messages for context window based on relevance to current query.
		/// </summary>
		/// <param name="currentQuery">Current user query to use for relevance comparison.</param>
		/// <param name="conversationHistory">Full conversation history to select from.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Selected messages in chronological order.</returns>
		Task<List<ConversationMessage>> SelectRelevantContextAsync(
			string currentQuery,
			List<ConversationMessage> conversationHistory,
			CancellationToken cancellationToken = default);
	}
}
