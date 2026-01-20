using Hermes.Storage.Core.Models;

namespace Hermes.Storage.Repositories.ConversationReference
{
	/// <summary>
	/// Document storing Teams conversation reference for proactive messaging.
	/// Enables the bot to send messages to users outside of an active conversation context.
	/// </summary>
	public class ConversationReferenceDocument : Document
	{
		/// <summary>
		/// Teams user ID (e.g., "29:1qKvhECjhXYX...").
		/// Used as part of PartitionKey for efficient lookups.
		/// </summary>
		public string TeamsUserId { get; set; } = string.Empty;

		/// <summary>
		/// Teams conversation ID (e.g., "19:meeting_xyz..." for group chats, "a:1-1_xyz..." for 1:1).
		/// Unique identifier for the conversation context.
		/// </summary>
		public string ConversationId { get; set; } = string.Empty;

		/// <summary>
		/// Serialized ConversationReference object from Microsoft.Agents SDK.
		/// Contains all routing information needed for proactive messaging (user info, tenant, service URL, etc.).
		/// </summary>
		public string ConversationReferenceJson { get; set; } = string.Empty;

		/// <summary>
		/// Timestamp of the last interaction in this conversation.
		/// Used to determine the most recent/active conversation for proactive messaging.
		/// </summary>
		public DateTime LastInteractionAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// Flag indicating if this reference is still valid.
		/// Set to false after repeated proactive messaging failures (circuit breaker).
		/// </summary>
		public bool IsActive { get; set; } = true;

		/// <summary>
		/// Number of consecutive failures when attempting proactive messaging.
		/// Reset to 0 on successful message delivery. Deactivates after 5 failures.
		/// </summary>
		public int ConsecutiveFailureCount { get; set; } = 0;

		/// <summary>
		/// Override TTL: Conversation references persist for 90 days (7776000 seconds).
		/// Default 8 hours is too short for proactive messaging scenarios.
		/// </summary>
		public new int? TTL { get; set; } = 7776000;
	}
}
