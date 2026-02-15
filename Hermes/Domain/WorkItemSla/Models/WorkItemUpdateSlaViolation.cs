namespace Hermes.Domain.WorkItemSla.Models
{
	/// <summary>
	/// Represents a single work item that violates its update frequency SLA.
	/// </summary>
	public class WorkItemUpdateSlaViolation
	{
		/// <summary>
		/// Gets or sets the work item ID.
		/// </summary>
		public int WorkItemId { get; set; }

		/// <summary>
		/// Gets or sets the work item title.
		/// </summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the work item type (e.g., Bug, Task, User Story, Feature).
		/// </summary>
		public string WorkItemType { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the number of days since the work item was last updated.
		/// </summary>
		public int DaysSinceUpdate { get; set; }

		/// <summary>
		/// Gets or sets the SLA threshold in days for this work item type.
		/// </summary>
		public int SlaThresholdDays { get; set; }

		/// <summary>
		/// Gets or sets the URL to view the work item in Azure DevOps.
		/// </summary>
		public string Url { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the team ID this violation belongs to.
		/// Used for team-separated reporting in multi-team scenarios.
		/// </summary>
		public string TeamId { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the team name this violation belongs to.
		/// Used for display in manager reports.
		/// </summary>
		public string TeamName { get; set; } = string.Empty;
	}
}
