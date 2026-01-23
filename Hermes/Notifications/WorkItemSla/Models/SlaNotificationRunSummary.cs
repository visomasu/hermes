namespace Hermes.Notifications.WorkItemSla.Models
{
	/// <summary>
	/// Statistics for a complete SLA notification job execution.
	/// Used for logging and monitoring.
	/// </summary>
	public class SlaNotificationRunSummary
	{
		/// <summary>
		/// Gets or sets the number of users processed in this run.
		/// </summary>
		public int UsersProcessed { get; set; }

		/// <summary>
		/// Gets or sets the total number of SLA violations detected across all users.
		/// </summary>
		public int ViolationsDetected { get; set; }

		/// <summary>
		/// Gets or sets the number of notifications successfully sent.
		/// </summary>
		public int NotificationsSent { get; set; }

		/// <summary>
		/// Gets or sets the number of notifications blocked by NotificationGate.
		/// </summary>
		public int NotificationsBlocked { get; set; }

		/// <summary>
		/// Gets or sets the number of errors encountered during processing.
		/// </summary>
		public int Errors { get; set; }

		/// <summary>
		/// Gets or sets the total duration of the job execution.
		/// </summary>
		public TimeSpan Duration { get; set; }
	}
}
