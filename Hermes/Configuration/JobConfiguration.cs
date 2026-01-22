namespace Hermes.Configuration
{
	/// <summary>
	/// Configuration for an individual scheduled job.
	/// </summary>
	public class JobConfiguration
	{
		/// <summary>
		/// Unique name for the job (used in Quartz identity).
		/// </summary>
		public string JobName { get; set; } = string.Empty;

		/// <summary>
		/// Job type identifier for resolving to C# type.
		/// Example: "SlaNotification" maps to SlaNotificationJob.
		/// </summary>
		public string JobType { get; set; } = string.Empty;

		/// <summary>
		/// Whether this job is enabled.
		/// </summary>
		public bool Enabled { get; set; } = true;

		/// <summary>
		/// Cron expression defining when the job runs.
		/// Example: "0 0 9 * * ?" = 9:00 AM daily
		/// </summary>
		public string CronExpression { get; set; } = string.Empty;

		/// <summary>
		/// Timezone ID for the cron schedule (IANA format).
		/// Example: "America/Los_Angeles", "UTC", "America/New_York"
		/// </summary>
		public string TimeZone { get; set; } = "America/Los_Angeles";

		/// <summary>
		/// Optional job-specific parameters.
		/// </summary>
		public Dictionary<string, string> Parameters { get; set; } = new();
	}
}
