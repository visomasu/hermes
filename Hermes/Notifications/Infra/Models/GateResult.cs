namespace Hermes.Notifications.Infra.Models
{
	/// <summary>
	/// Result of evaluating whether a notification can be sent through the gate.
	/// </summary>
	public class GateResult
	{
		/// <summary>
		/// Whether the notification can be sent.
		/// </summary>
		public bool CanSend { get; set; }

		/// <summary>
		/// Reason why the notification was blocked, if CanSend is false.
		/// Examples: "Hourly limit exceeded", "Quiet hours", "Duplicate notification"
		/// </summary>
		public string? BlockedReason { get; set; }

		/// <summary>
		/// Number of notifications sent in the last hour.
		/// </summary>
		public int NotificationsSentInLastHour { get; set; }

		/// <summary>
		/// Number of notifications sent in the last day.
		/// </summary>
		public int NotificationsSentInLastDay { get; set; }
	}
}
