namespace Hermes.Storage.Repositories.UserNotificationState
{
	/// <summary>
	/// Represents a single notification event in the user's recent notification history.
	/// Used for throttling calculations and basic audit trail.
	/// </summary>
	public class NotificationEvent
	{
		/// <summary>
		/// When the notification was sent (UTC).
		/// </summary>
		public DateTime SentAt { get; set; }

		/// <summary>
		/// Type of notification (e.g., "SlaViolation", "WorkItemUpdate").
		/// </summary>
		public string NotificationType { get; set; } = string.Empty;

		/// <summary>
		/// Optional deduplication key for auditing purposes.
		/// Format: "{NotificationType}_{Identifier}" (e.g., "SlaViolation_12345").
		/// </summary>
		public string? DeduplicationKey { get; set; }

		/// <summary>
		/// Optional work item ID if this is a work item notification.
		/// </summary>
		public int? WorkItemId { get; set; }

		/// <summary>
		/// Optional area path if this is a work item notification.
		/// </summary>
		public string? AreaPath { get; set; }
	}
}
