using Hermes.Storage.Core.Models;
using Hermes.Storage.Repositories;

namespace Hermes.Storage.Repositories.ConversationReference
{
	/// <summary>
	/// Repository for managing conversation references for proactive messaging.
	/// </summary>
	public interface IConversationReferenceRepository : IRepository<ConversationReferenceDocument>
	{
		/// <summary>
		/// Retrieves a conversation reference by Teams user ID.
		/// </summary>
		/// <param name="teamsUserId">Teams user ID.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Conversation reference document, or null if not found.</returns>
		Task<ConversationReferenceDocument?> GetByTeamsUserIdAsync(
			string teamsUserId,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Retrieves all active conversation references.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>List of active conversation references.</returns>
		Task<List<ConversationReferenceDocument>> GetActiveReferencesAsync(
			CancellationToken cancellationToken = default);
	}
}
