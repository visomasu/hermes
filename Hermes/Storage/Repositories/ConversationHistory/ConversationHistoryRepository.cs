using System.Text.Json;
using Hermes.Storage.Core;
using Hermes.Storage.Repositories;

namespace Hermes.Storage.Repositories.ConversationHistory
{
    /// <summary>
    /// Repository implementation for storing conversation history using the generic storage abstraction.
    /// </summary>
    public class ConversationHistoryRepository : RepositoryBase<ConversationHistoryDocument>, IConversationHistoryRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationHistoryRepository"/> class.
        /// </summary>
        /// <param name="storage">The storage client used for persisting conversation history documents.</param>
        public ConversationHistoryRepository(IStorageClient<ConversationHistoryDocument, string> storage)
            : base(storage)
        {
        }

        /// <inheritdoc />
        public async Task<string?> GetConversationHistoryAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                throw new ArgumentException("Conversation id cannot be null or empty.", nameof(conversationId));
            }

            // Use the same value for id and partition key so all history for a conversation is grouped.
            var document = await ReadAsync(conversationId, conversationId).ConfigureAwait(false);

            // Serialize the list of conversation messages to a JSON string for the consumer.
            if (document == null || document.History == null || document.History.Count == 0)
            {
                return null;
            }

            return System.Text.Json.JsonSerializer.Serialize(document.History);
        }

        /// <inheritdoc />
        public async Task WriteConversationHistoryAsync(string conversationId, List<ConversationMessage> historyEntries, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                throw new ArgumentException("Conversation id cannot be null or empty.", nameof(conversationId));
            }

            if (historyEntries == null || historyEntries.Count == 0)
            {
                return; // nothing to write
            }

            // Read existing document if any
            var existing = await ReadAsync(conversationId, conversationId).ConfigureAwait(false);

            if (existing == null)
            {
                var conversationHistory = new ConversationHistoryDocument
                {
                    Id = conversationId,
                    PartitionKey = conversationId,
                    History = new List<ConversationMessage>(historyEntries)
                };

                await CreateAsync(conversationHistory).ConfigureAwait(false);
            }
            else
            {
                if (existing.History == null)
                {
                    existing.History = new List<ConversationMessage>();
                }

                existing.History.AddRange(historyEntries);

                await UpdateAsync(existing.Id, existing).ConfigureAwait(false);
            }
        }
    }
}
