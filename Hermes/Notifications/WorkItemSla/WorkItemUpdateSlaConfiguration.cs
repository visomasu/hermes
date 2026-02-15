namespace Hermes.Notifications.WorkItemSla
{
	/// <summary>
	/// Configuration for work item update SLA notifications.
	/// Defines SLA thresholds by work item type and notification behavior.
	/// </summary>
	public class WorkItemUpdateSlaConfiguration
	{
		/// <summary>
		/// Gets or sets a value indicating whether SLA notifications are enabled.
		/// </summary>
		public bool Enabled { get; set; } = true;

		/// <summary>
		/// Gets or sets the base URL for Azure DevOps work items.
		/// Example: "https://dev.azure.com/dynamicscrm/OneCRM/_workitems/edit"
		/// </summary>
		public string AzureDevOpsBaseUrl { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the SLA rules by work item type.
		/// Key: Work item type (e.g., "Bug", "Task")
		/// Value: Days before SLA violation
		/// DEPRECATED: Use GlobalSlaDefaults instead. Kept for backwards compatibility.
		/// </summary>
		[Obsolete("Use GlobalSlaDefaults instead")]
		public Dictionary<string, int> SlaRules { get; set; } = new();

		/// <summary>
		/// Gets or sets the global default SLA rules by work item type.
		/// These are used as fallback values when team-specific SLA overrides are not defined.
		/// Key: Work item type (e.g., "Bug", "Task")
		/// Value: Days before SLA violation
		/// </summary>
		public Dictionary<string, int> GlobalSlaDefaults { get; set; } = new();

		/// <summary>
		/// Gets or sets the team name for querying current iteration.
		/// Required to dynamically determine the current iteration path based on dates.
		/// Example: "OneCRM Team", "Platform Team"
		/// If null or empty, iteration filtering is skipped.
		/// DEPRECATED: Use per-team configuration in TeamConfigurationDocument instead.
		/// </summary>
		[Obsolete("Use per-team configuration in TeamConfigurationDocument instead")]
		public string? TeamName { get; set; }

		/// <summary>
		/// Gets or sets the iteration path filter (deprecated - use TeamName for automatic current iteration).
		/// If set, only work items under this iteration path will be checked.
		/// Specify a path like "Project\\Sprint 1".
		/// If null or empty, all iterations are checked (or current iteration if TeamName is set).
		/// DEPRECATED: Use per-team configuration in TeamConfigurationDocument instead.
		/// </summary>
		[Obsolete("Use per-team configuration in TeamConfigurationDocument instead")]
		public string? IterationPath { get; set; }

		/// <summary>
		/// Gets or sets the batch size for querying users.
		/// </summary>
		public int QueryBatchSize { get; set; } = 10;

		/// <summary>
		/// Gets or sets the maximum number of notifications to send per job run.
		/// Prevents overwhelming users or hitting API limits.
		/// </summary>
		public int MaxNotificationsPerRun { get; set; } = 100;

		/// <summary>
		/// Gets or sets the deduplication window in hours.
		/// Prevents notifying about the same violation within this window.
		/// </summary>
		public int DeduplicationWindowHours { get; set; } = 24;

		/// <summary>
		/// Gets or sets a value indicating whether to bypass notification gates.
		/// When true, throttling, quiet hours, and deduplication are skipped.
		/// Useful for development/testing. Should be false in production.
		/// </summary>
		public bool BypassGates { get; set; } = false;

		/// <summary>
		/// Gets or sets the batch size for parallel user processing.
		/// Higher values process more users concurrently but may increase load.
		/// Set to 1 to disable batch processing (sequential).
		/// </summary>
		public int UserProcessingBatchSize { get; set; } = 5;
	}
}
