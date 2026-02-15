using Hermes.Storage.Core;
using Hermes.Storage.Core.CosmosDB;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Hermes.Storage.Repositories.TeamConfiguration
{
	/// <summary>
	/// Repository implementation for team configuration documents.
	/// </summary>
	public class TeamConfigurationRepository
		: RepositoryBase<TeamConfigurationDocument>,
		  ITeamConfigurationRepository
	{
		private readonly ILogger<TeamConfigurationRepository> _logger;

		/// <inheritdoc/>
		protected override string ObjectTypeCode => "team-config";

		public TeamConfigurationRepository(
			ILogger<TeamConfigurationRepository> logger,
			IStorageClient<TeamConfigurationDocument, string> storage)
			: base(storage)
		{
			_logger = logger;
		}

		/// <inheritdoc/>
		public async Task<TeamConfigurationDocument?> GetByTeamIdAsync(
			string teamId,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(teamId))
			{
				return null;
			}

			// TeamId is used as both Id and PartitionKey
			return await ReadAsync(teamId, teamId);
		}

		/// <inheritdoc/>
		public async Task<List<TeamConfigurationDocument>> GetAllTeamsAsync(
			CancellationToken cancellationToken = default)
		{
			try
			{
				// Try to cast storage to CosmosDbStorageClient to access QueryAsync method
				// This is necessary because IStorageClient interface doesn't expose cross-partition queries
				var cosmosStorage = _storage as CosmosDbStorageClient<TeamConfigurationDocument>;

				if (cosmosStorage == null)
				{
					// Storage might be HierarchicalStorageClient, try to access L2
					if (_storage is HierarchicalStorageClient<TeamConfigurationDocument> hierarchical)
					{
						// Use reflection to access _l2 field
						var l2Field = typeof(HierarchicalStorageClient<TeamConfigurationDocument>)
							.GetField("_l2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

						if (l2Field != null)
						{
							cosmosStorage = l2Field.GetValue(hierarchical) as CosmosDbStorageClient<TeamConfigurationDocument>;
						}
					}
				}

				if (cosmosStorage == null)
				{
					_logger.LogWarning(
						"Unable to access CosmosDbStorageClient for cross-partition query. " +
						"Storage type: {StorageType}. Returning empty list.",
						_storage.GetType().Name);
					return new List<TeamConfigurationDocument>();
				}

				// Execute cross-partition query
				var query = new QueryDefinition("SELECT * FROM c");

				var results = await cosmosStorage.QueryAsync(query, cancellationToken);

				_logger.LogInformation(
					"GetAllTeamsAsync found {Count} team configurations",
					results.Count);

				return results;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error executing cross-partition query for team configurations");
				return new List<TeamConfigurationDocument>();
			}
		}

		/// <inheritdoc/>
		public async Task<TeamConfigurationDocument> UpsertAsync(
			TeamConfigurationDocument document,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(document.TeamId))
			{
				throw new ArgumentException("TeamId cannot be null or empty", nameof(document));
			}

			// Set Id and PartitionKey to TeamId
			document.Id = document.TeamId;
			document.PartitionKey = document.TeamId;

			// Update timestamp
			if (document.CreatedAt == default)
			{
				document.CreatedAt = DateTime.UtcNow;
			}
			document.UpdatedAt = DateTime.UtcNow;

			// Use base UpdateAsync method (which uses CosmosDB UpsertItemAsync under the hood)
			await UpdateAsync(document.TeamId, document);

			_logger.LogInformation(
				"Upserted team configuration: TeamId={TeamId}, TeamName={TeamName}",
				document.TeamId,
				document.TeamName);

			return document;
		}
	}
}
