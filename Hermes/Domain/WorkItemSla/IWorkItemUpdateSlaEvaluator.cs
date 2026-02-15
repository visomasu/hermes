using Hermes.Domain.WorkItemSla.Models;
using Hermes.Notifications.WorkItemSla.Models;

namespace Hermes.Domain.WorkItemSla
{
	/// <summary>
	/// Evaluates work items against SLA thresholds and sends notifications.
	/// </summary>
	public interface IWorkItemUpdateSlaEvaluator
	{
		/// <summary>
		/// Checks all users' work items for SLA violations and sends digest notifications.
		/// Used by scheduled job (WorkItemUpdateSlaJob).
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Summary of the evaluation run.</returns>
		Task<SlaNotificationRunSummary> EvaluateAndNotifyAsync(
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Checks work items assigned to a specific email address for SLA violations.
		/// Shared method used by both scheduled job and on-demand capability.
		/// DEPRECATED: Use CheckViolationsForTeamsAsync for multi-team support.
		/// </summary>
		/// <param name="email">Email address to check work items for.</param>
		/// <param name="areaPaths">Optional area paths to filter work items. If null or empty, all area paths are checked.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>List of SLA violations for the specified email.</returns>
		[Obsolete("Use CheckViolationsForTeamsAsync for multi-team support")]
		Task<List<WorkItemUpdateSlaViolation>> CheckViolationsForEmailAsync(
			string email,
			IEnumerable<string>? areaPaths = null,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Checks work items assigned to a specific email address for SLA violations across multiple teams.
		/// Each team can have its own SLA rules and area path filters.
		/// Used by CheckSlaViolationsCapability for multi-team on-demand checks.
		/// </summary>
		/// <param name="email">Email address to check work items for.</param>
		/// <param name="teamIds">Team IDs to check violations for. If null or empty, returns empty list.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>List of SLA violations grouped by team for the specified email.</returns>
		Task<List<WorkItemUpdateSlaViolation>> CheckViolationsForTeamsAsync(
			string email,
			IEnumerable<string> teamIds,
			CancellationToken cancellationToken = default);
	}
}
