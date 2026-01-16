using Hermes.Storage.Repositories.ConversationHistory;

namespace Hermes.Orchestrator.Context
{
	/// <summary>
	/// Simple time-based context selector that returns the most recent N messages.
	/// Used as fallback when semantic filtering is disabled or fails.
	/// </summary>
	public class TimeBasedConversationContextSelector : IConversationContextSelector
	{
		private readonly int _maxTurns;

		/// <summary>
		/// Initializes a new instance of the TimeBasedConversationContextSelector.
		/// </summary>
		/// <param name="maxTurns">Maximum number of recent turns to include.</param>
		public TimeBasedConversationContextSelector(int maxTurns)
		{
			_maxTurns = maxTurns;
		}

		/// <inheritdoc/>
		public Task<List<ConversationMessage>> SelectRelevantContextAsync(
			string currentQuery,
			List<ConversationMessage> conversationHistory,
			CancellationToken cancellationToken = default)
		{
			if (conversationHistory == null || conversationHistory.Count == 0)
			{
				return Task.FromResult(new List<ConversationMessage>());
			}

			// Simple time-based selection: take last N messages
			var selected = conversationHistory
				.OrderBy(m => m.Timestamp)
				.Skip(Math.Max(0, conversationHistory.Count - _maxTurns))
				.ToList();

			return Task.FromResult(selected);
		}
	}
}
