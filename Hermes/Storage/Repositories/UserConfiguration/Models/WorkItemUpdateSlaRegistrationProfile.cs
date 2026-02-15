namespace Hermes.Storage.Repositories.UserConfiguration.Models
{
	/// <summary>
	/// User's work item update SLA notification registration profile.
	/// Stores registration status, Azure DevOps email, and organizational hierarchy information
	/// for manager-driven work item update SLA violation notifications.
	/// </summary>
	public class WorkItemUpdateSlaRegistrationProfile
	{
		/// <summary>
		/// Whether user is registered for work item update SLA notifications.
		/// Explicit opt-in model - users must actively register to receive SLA violation reports.
		/// </summary>
		public bool IsRegistered { get; set; } = false;

		/// <summary>
		/// User's Azure DevOps email address (from Azure AD).
		/// Used to query Azure DevOps for work items assigned to this user.
		/// Retrieved from Microsoft Graph API during registration.
		/// </summary>
		public string AzureDevOpsEmail { get; set; } = string.Empty;

		/// <summary>
		/// Emails of user's direct reports (empty for Individual Contributors).
		/// Cached from Microsoft Graph API to avoid expensive queries on every SLA check.
		/// Managers receive aggregated SLA reports covering all direct reports.
		/// </summary>
		public List<string> DirectReportEmails { get; set; } = new();

		/// <summary>
		/// Derived property: user is a manager if they have direct reports.
		/// Not stored separately to ensure single source of truth and prevent inconsistent state.
		/// </summary>
		public bool IsManager => DirectReportEmails.Count > 0;

		/// <summary>
		/// Team IDs user is subscribed to for SLA notifications.
		/// Each team has its own iteration path, area paths, and SLA rule overrides.
		/// Example: ["contact-center-ai", "auth-antifraud"]
		/// </summary>
		public List<string> SubscribedTeamIds { get; set; } = new();

		/// <summary>
		/// Legacy area paths property (deprecated).
		/// Kept for backwards compatibility during migration.
		/// Use SubscribedTeamIds instead - area paths are now team-specific.
		/// </summary>
		[Obsolete("Use SubscribedTeamIds instead. Area paths are now configured per team.")]
		public List<string> AreaPaths { get; set; } = new();

		/// <summary>
		/// When user registered for work item update SLA notifications (UTC).
		/// </summary>
		public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// When direct reports list was last refreshed from Microsoft Graph API (UTC).
		/// Nullable to distinguish between "never refreshed" and "refreshed at specific time".
		/// Enables future enhancement: refresh stale data (e.g., if older than 7 days).
		/// </summary>
		public DateTime? DirectReportsLastRefreshedAt { get; set; }
	}
}
