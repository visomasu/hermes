using Hermes.Storage.Core;

namespace Hermes.Storage.Repositories.UserConfiguration
{
	/// <summary>
	/// Repository implementation for user configuration documents.
	/// </summary>
	public class UserConfigurationRepository
		: RepositoryBase<UserConfigurationDocument>,
		  IUserConfigurationRepository
	{
		/// <inheritdoc/>
		protected override string ObjectTypeCode => "user-config";

		public UserConfigurationRepository(
			IStorageClient<UserConfigurationDocument, string> storage)
			: base(storage)
		{
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
	}
}
