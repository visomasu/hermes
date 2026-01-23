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

			// TEMPORARY: Hardcoded test data for SLA notification testing
			// Real implementation pending cross-partition query solution
			var testDocument = new ConversationReferenceDocument
			{
				TeamsUserId = "user-id-0",
				ConversationId = "973d9a9b-db52-49f2-b0e8-5ab43e7fb81b",
				ConversationReferenceJson = "{\"ActivityId\":\"6e6ac86b-f897-4db0-bb40-d079064328bf\",\"user\":{\"Id\":\"user-id-0\",\"Name\":\"Alex Wilber\",\"AadObjectId\":\"00000000-0000-0000-0000-0000000000020\",\"Role\":null,\"AgenticUserId\":null,\"AgenticAppId\":null,\"TenantId\":null,\"email\":\"AlexW@M365x214355.onmicrosoft.com\",\"Properties\":{\"email\":\"AlexW@M365x214355.onmicrosoft.com\"}},\"bot\":{\"Id\":\"00000000-0000-0000-0000-00000000000011\",\"Name\":\"Test Bot\",\"AadObjectId\":null,\"Role\":null,\"AgenticUserId\":null,\"AgenticAppId\":null,\"TenantId\":null,\"Properties\":{}},\"Conversation\":{\"IsGroup\":null,\"ConversationType\":\"personal\",\"TenantId\":\"00000000-0000-0000-0000-0000000000001\",\"Id\":\"973d9a9b-db52-49f2-b0e8-5ab43e7fb81b\",\"Name\":null,\"AadObjectId\":null,\"Role\":null,\"Properties\":{}},\"ChannelId\":\"msteams\",\"ServiceUrl\":\"http://localhost:56150/_connector\",\"Locale\":\"en-US\",\"RequestId\":\"3f3e3640-fb60-4235-84bf-a4c5f370251b\",\"DeliveryMode\":null}",
				LastInteractionAt = DateTime.Parse("2026-01-20T01:45:22.2618037Z"),
				IsActive = true,
				ConsecutiveFailureCount = 0,
				TTL = 7776000,
				Id = "973d9a9b-db52-49f2-b0e8-5ab43e7fb81b",
				PartitionKey = "conv:user-id-0",
				Etag = "\"00000000-0000-0000-89ae-70b73c0501dc\""
			};

			return await Task.FromResult(new List<ConversationReferenceDocument> { testDocument });
		}
	}
}
