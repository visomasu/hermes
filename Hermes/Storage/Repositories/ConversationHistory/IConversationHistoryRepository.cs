namespace Hermes.Storage.Repositories.ConversationHistory
{
    /// <summary>
    /// Abstraction for storing and retrieving conversation history for a given conversation identifier.
    /// Implementations can persist history in memory, databases, or other storage mechanisms.
    /// </summary>
    public interface IConversationHistoryRepository
    {
        /// <summary>
        /// Gets the stored conversation history for the given conversation identifier.
        /// </summary>
        /// <param name="conversationId">Unique identifier for the conversation (e.g., Teams conversation id or thread id).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The serialized conversation history, or null if no history is stored.</returns>
        Task<string?> GetConversationHistoryAsync(string conversationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Appends or writes conversation history entries for the given conversation identifier.
        /// Implementations may choose to overwrite or extend existing history.
        /// </summary>
        /// <param name="conversationId">Unique identifier for the conversation (e.g., Teams conversation id or thread id).</param>
        /// <param name="historyEntries">One or more conversation messages to store.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task WriteConversationHistoryAsync(string conversationId, List<ConversationMessage> historyEntries, CancellationToken cancellationToken = default);
    }
}
