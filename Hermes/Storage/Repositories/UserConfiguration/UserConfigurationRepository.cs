using Hermes.Storage.Core;
using Microsoft.Extensions.Logging;

namespace Hermes.Storage.Repositories.UserConfiguration
{
	/// <summary>
	/// Repository implementation for user configuration documents.
	/// </summary>
	public class UserConfigurationRepository
		: RepositoryBase<UserConfigurationDocument>,
		  IUserConfigurationRepository
	{
		private readonly ILogger<UserConfigurationRepository> _logger;

		/// <inheritdoc/>
		protected override string ObjectTypeCode => "user-config";

		public UserConfigurationRepository(
			IStorageClient<UserConfigurationDocument, string> storage,
			ILogger<UserConfigurationRepository> logger)
			: base(storage)
		{
			_logger = logger;
		}

		/// <inheritdoc/>
		public async Task<UserConfigurationDocument?> GetByTeamsUserIdAsync(
			string teamsUserId,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(teamsUserId))
			{
				return null;
			}

			// TeamsUserId is used as both Id and PartitionKey
			return await ReadAsync(teamsUserId, teamsUserId);
		}

		/// <inheritdoc/>
		public async Task<List<UserConfigurationDocument>> GetAllWithSlaRegistrationAsync(
			CancellationToken cancellationToken = default)
		{
			// TODO: Implement cross-partition query for SLA registered users
			// This requires CosmosDB cross-partition query with:
			// SELECT * FROM c WHERE c.SlaRegistration != null AND c.SlaRegistration.IsRegistered = true
			//
			// Current limitation: IStorageClient interface doesn't support cross-partition queries
			// Options to implement:
			// 1. Extend IStorageClient with QueryAsync method
			// 2. Cast _storage to CosmosDbStorageClient and access container directly
			// 3. Maintain separate partition for registered users (denormalization)
			//
			// For MVP Phase 2: Returning empty list until implemented in Phase 5
			// WorkItemUpdateSlaEvaluator will be refactored to use this method

			_logger.LogWarning(
				"GetAllWithSlaRegistrationAsync called but cross-partition query not yet implemented. " +
				"Returning empty list. This method will be implemented when refactoring WorkItemUpdateSlaEvaluator in Phase 5.");

			return await Task.FromResult(new List<UserConfigurationDocument>());
		}
	}
}
