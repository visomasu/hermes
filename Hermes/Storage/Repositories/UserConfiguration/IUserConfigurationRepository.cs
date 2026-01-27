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

		/// <summary>
		/// Retrieves all users with SLA registration (cross-partition query).
		/// Returns only users where SlaRegistration is not null and IsRegistered is true.
		/// IMPORTANT: This is an expensive operation in CosmosDB - use sparingly.
		/// Intended for scheduled SLA notification job that runs once per day.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>List of user configurations with active SLA registration.</returns>
		Task<List<UserConfigurationDocument>> GetAllWithSlaRegistrationAsync(
			CancellationToken cancellationToken = default);
	}
}
