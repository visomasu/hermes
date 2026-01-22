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
		/// Retrieves the most recent active conversation reference for a Teams user.
		/// Useful for default proactive messaging when conversation context is not specified.
		/// </summary>
		/// <param name="teamsUserId">Teams user ID.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Most recent conversation reference document, or null if not found.</returns>
		Task<ConversationReferenceDocument?> GetByTeamsUserIdAsync(
			string teamsUserId,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Retrieves a specific conversation reference by conversation ID.
		/// </summary>
		/// <param name="conversationId">Teams conversation ID.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Conversation reference document, or null if not found.</returns>
		Task<ConversationReferenceDocument?> GetByConversationIdAsync(
			string conversationId,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Retrieves all conversation references for a Teams user.
		/// Returns all conversations (1:1 chats, group chats, channels) for the user.
		/// </summary>
		/// <param name="teamsUserId">Teams user ID.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>List of conversation references for the user.</returns>
		Task<List<ConversationReferenceDocument>> GetAllByTeamsUserIdAsync(
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
