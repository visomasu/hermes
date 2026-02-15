namespace Hermes.Storage.Repositories.TeamConfiguration
{
	/// <summary>
	/// Repository for managing team configuration documents.
	/// </summary>
	public interface ITeamConfigurationRepository : IRepository<TeamConfigurationDocument>
	{
		/// <summary>
		/// Retrieves team configuration by team ID.
		/// </summary>
		/// <param name="teamId">The team ID (e.g., "contact-center-ai").</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>The team configuration document, or null if not found.</returns>
		Task<TeamConfigurationDocument?> GetByTeamIdAsync(
			string teamId,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Retrieves all team configurations (cross-partition query).
		/// IMPORTANT: This is an expensive operation in CosmosDB - use sparingly.
		/// Intended for seeding, admin operations, and newsletter team detection.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>List of all team configurations.</returns>
		Task<List<TeamConfigurationDocument>> GetAllTeamsAsync(
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Creates or updates a team configuration document.
		/// </summary>
		/// <param name="document">The team configuration document to upsert.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>The upserted team configuration document.</returns>
		Task<TeamConfigurationDocument> UpsertAsync(
			TeamConfigurationDocument document,
			CancellationToken cancellationToken = default);
	}
}
