using Hermes.Storage.Core;
using Hermes.Storage.Core.CosmosDB;
using Microsoft.Azure.Cosmos;
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
			try
			{
				// Try to cast storage to CosmosDbStorageClient to access QueryAsync method
				// This is necessary because IStorageClient interface doesn't expose cross-partition queries
				var cosmosStorage = _storage as CosmosDbStorageClient<UserConfigurationDocument>;

				if (cosmosStorage == null)
				{
					// Storage might be HierarchicalStorageClient, try to access L2
					if (_storage is HierarchicalStorageClient<UserConfigurationDocument> hierarchical)
					{
						// Use reflection to access _l2 field
						var l2Field = typeof(HierarchicalStorageClient<UserConfigurationDocument>)
							.GetField("_l2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

						if (l2Field != null)
						{
							cosmosStorage = l2Field.GetValue(hierarchical) as CosmosDbStorageClient<UserConfigurationDocument>;
						}
					}
				}

				if (cosmosStorage == null)
				{
					_logger.LogWarning(
						"Unable to access CosmosDbStorageClient for cross-partition query. " +
						"Storage type: {StorageType}. Returning empty list.",
						_storage.GetType().Name);
					return new List<UserConfigurationDocument>();
				}

				// Execute cross-partition query
				var query = new QueryDefinition(
					"SELECT * FROM c WHERE c.SlaRegistration != null AND c.SlaRegistration.IsRegistered = true");

				var results = await cosmosStorage.QueryAsync(query, cancellationToken);

				_logger.LogInformation(
					"GetAllWithSlaRegistrationAsync found {Count} registered users",
					results.Count);

				return results;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error executing cross-partition query for SLA registered users");
				return new List<UserConfigurationDocument>();
			}
		}
	}
}
