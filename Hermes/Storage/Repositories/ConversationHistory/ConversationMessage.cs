namespace Hermes.Storage.Repositories.ConversationHistory
{
    /// <summary>
    /// Represents a single message in a conversation for history storage.
    /// </summary>
    public class ConversationMessage
    {
        /// <summary>
        /// Role of the message sender (e.g., "user", "assistant", "system").
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Message content as plain text or serialized payload.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Optional timestamp in UTC.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Embedding vector for semantic similarity comparison.
        /// Nullable for backward compatibility with existing messages.
        /// </summary>
        public float[]? Embedding { get; set; } = null;

        /// <summary>
        /// Flag indicating whether embedding generation has been attempted.
        /// Differentiates between "not yet generated" and "generation failed/skipped".
        /// </summary>
        public bool EmbeddingGenerated { get; set; } = false;
    }
}
