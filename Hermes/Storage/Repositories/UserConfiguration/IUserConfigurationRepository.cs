namespace Hermes.Storage.Repositories.UserConfiguration
{
	/// <summary>
	/// Repository for managing user configuration documents.
	/// </summary>
	public interface IUserConfigurationRepository : IRepository<UserConfigurationDocument>
	{
		/// <summary>
		/// Retrieves user configuration by Teams user ID.
		/// </summary>
		/// <param name="teamsUserId">The Teams user ID.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>The user configuration document, or null if not found.</returns>
		Task<UserConfigurationDocument?> GetByTeamsUserIdAsync(
			string teamsUserId,
			CancellationToken cancellationToken = default);
	}
}
