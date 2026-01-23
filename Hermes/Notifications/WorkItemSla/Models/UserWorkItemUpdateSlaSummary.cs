namespace Hermes.Notifications.WorkItemSla.Models
{
	/// <summary>
	/// Groups all SLA violations for a single user.
	/// Used to compose digest notifications (one message per user with all their violations).
	/// </summary>
	public class UserWorkItemUpdateSlaSummary
	{
		/// <summary>
		/// Gets or sets the Teams user ID.
		/// </summary>
		public string TeamsUserId { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the user's email address.
		/// </summary>
		public string UserEmail { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the list of SLA violations for this user.
		/// </summary>
		public List<WorkItemUpdateSlaViolation> Violations { get; set; } = new();
	}
}
