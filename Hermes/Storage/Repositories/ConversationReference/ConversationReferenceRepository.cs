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
				throw new ArgumentException("TeamsUserId cannot be null or empty.", nameof(teamsUserId));

			// PartitionKey is TeamsUserId, so we can use ReadAllByPartitionKeyAsync
			var results = await ReadAllByPartitionKeyAsync(teamsUserId);
			return results?.FirstOrDefault();
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
