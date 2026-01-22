using Hermes.Storage.Core.Models;

namespace Hermes.Storage.Repositories.UserNotificationState
{
	/// <summary>
	/// Aggregate document tracking a user's recent notification history.
	/// Maintains a sliding window of notifications for throttling calculations.
	/// One document per user (much more efficient than one document per notification).
	/// </summary>
	public class UserNotificationStateDocument : Document
	{
		/// <summary>
		/// The Teams user ID.
		/// Used as both Id and PartitionKey for single-document-per-user pattern.
		/// </summary>
		public string TeamsUserId { get; set; } = string.Empty;

		/// <summary>
		/// Recent notification events (last 24 hours).
		/// Automatically cleaned up on each update to remove old events.
		/// </summary>
		public List<NotificationEvent> RecentNotifications { get; set; } = new();

		/// <summary>
		/// Last time this document was updated.
		/// </summary>
		public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// Total notifications sent (all time counter).
		/// Useful for analytics without querying history.
		/// </summary>
		public int TotalNotificationsSent { get; set; } = 0;

		/// <summary>
		/// Override TTL to 30 days (2,592,000 seconds).
		/// Document auto-deletes if user hasn't received notifications for 30 days.
		/// </summary>
		public new int? TTL { get; set; } = 2592000;
	}
}
