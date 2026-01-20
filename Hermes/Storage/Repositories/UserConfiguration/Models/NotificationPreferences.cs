namespace Hermes.Storage.Repositories.UserConfiguration.Models
{
	/// <summary>
	/// User preferences for notifications.
	/// </summary>
	public class NotificationPreferences
	{
		/// <summary>
		/// Whether to receive SLA violation notifications.
		/// </summary>
		public bool SlaViolationNotifications { get; set; } = true;

		/// <summary>
		/// Whether to receive work item update notifications.
		/// </summary>
		public bool WorkItemUpdateNotifications { get; set; } = false;

		/// <summary>
		/// Maximum number of notifications allowed per hour.
		/// </summary>
		public int MaxNotificationsPerHour { get; set; } = 5;

		/// <summary>
		/// Maximum number of notifications allowed per day.
		/// </summary>
		public int MaxNotificationsPerDay { get; set; } = 20;

		/// <summary>
		/// User's timezone identifier (IANA timezone, e.g., "America/Los_Angeles", "Europe/London").
		/// Used to convert quiet hours from user's local time to UTC for evaluation.
		/// Defaults to UTC if not specified.
		/// </summary>
		public string TimeZoneId { get; set; } = "UTC";

		/// <summary>
		/// Optional quiet hours during which notifications are suppressed.
		/// Times are specified in the user's local timezone (TimeZoneId).
		/// </summary>
		public QuietHours? QuietHours { get; set; }
	}
}
