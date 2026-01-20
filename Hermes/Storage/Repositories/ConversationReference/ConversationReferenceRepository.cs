using Hermes.Storage.Core;
using Hermes.Storage.Repositories;

namespace Hermes.Storage.Repositories.ConversationReference
{
	/// <summary>
	/// Repository for conversation references.
	/// </summary>
	public class ConversationReferenceRepository
		: RepositoryBase<ConversationReferenceDocument>,
		  IConversationReferenceRepository
	{
		/// <inheritdoc/>
		protected override string ObjectTypeCode => "conv";

		public ConversationReferenceRepository(
			IStorageClient<ConversationReferenceDocument, string> storage)
			: base(storage)
		{
		}

		public async Task<ConversationReferenceDocument?> GetByTeamsUserIdAsync(
			string teamsUserId,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(teamsUserId))
			{
				return null;
			}

			// Get all conversations for this user and return the most recent one
			var results = await ReadAllByPartitionKeyAsync(teamsUserId);

			if (results == null || results.Count == 0)
				return null;

			// Return the most recent active conversation
			return results
				.Where(r => r.IsActive)
				.OrderByDescending(r => r.LastInteractionAt)
				.FirstOrDefault();
		}

		public async Task<ConversationReferenceDocument?> GetByConversationIdAsync(
			string conversationId,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(conversationId))
			{
				return null;
			}

			// ConversationId is used as the document Id
			// We need to scan all partitions or use a different approach
			// For now, this requires the caller to know the TeamsUserId
			// Better approach: Use ReadAsync with conversationId as Id if we can determine partition key
			// This is a limitation of the current design - cross-partition queries are expensive

			// Placeholder: This would require cross-partition query which is expensive
			// Alternative: Require TeamsUserId as parameter, or use conversationId as Id with synthetic partition key
			throw new NotImplementedException("GetByConversationIdAsync requires cross-partition query. Use GetByTeamsUserIdAsync or GetAllByTeamsUserIdAsync instead.");
		}

		public async Task<List<ConversationReferenceDocument>> GetAllByTeamsUserIdAsync(
			string teamsUserId,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(teamsUserId))
			{
				return new List<ConversationReferenceDocument>();
			}

			// PartitionKey will be automatically prefixed by RepositoryBase
			var results = await ReadAllByPartitionKeyAsync(teamsUserId);
			return results?.ToList() ?? new List<ConversationReferenceDocument>();
		}

		public async Task<List<ConversationReferenceDocument>> GetActiveReferencesAsync(
			CancellationToken cancellationToken = default)
		{
			// TODO: This requires cross-partition query with WHERE IsActive = true
			// CosmosDB cross-partition query is expensive
			// For MVP, would need to implement custom query in storage client
			// Alternative: Maintain separate partition for active references

			// Placeholder: Return empty list (needs real implementation)
			return new List<ConversationReferenceDocument>();
		}
	}
}
