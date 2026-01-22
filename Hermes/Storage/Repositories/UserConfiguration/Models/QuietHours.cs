namespace Hermes.Storage.Repositories.UserConfiguration.Models
{
	/// <summary>
	/// Defines a time window during which notifications should be suppressed.
	/// Times are specified in the user's local timezone (see NotificationPreferences.TimeZoneId).
	/// </summary>
	public class QuietHours
	{
		/// <summary>
		/// Start time for quiet hours in user's local timezone.
		/// Example: 22:00 means 10 PM in the user's timezone.
		/// </summary>
		public TimeOnly StartTime { get; set; } = new(22, 0); // 10 PM local time

		/// <summary>
		/// End time for quiet hours in user's local timezone.
		/// Example: 08:00 means 8 AM in the user's timezone.
		/// </summary>
		public TimeOnly EndTime { get; set; } = new(8, 0); // 8 AM local time

		/// <summary>
		/// Whether quiet hours are enabled.
		/// </summary>
		public bool Enabled { get; set; } = false;
	}
}
