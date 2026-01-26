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
		/// </summary>
		/// <param name="email">Email address to check work items for.</param>
		/// <param name="areaPaths">Optional area paths to filter work items. If null or empty, all area paths are checked.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>List of SLA violations for the specified email.</returns>
		Task<List<WorkItemUpdateSlaViolation>> CheckViolationsForEmailAsync(
			string email,
			IEnumerable<string>? areaPaths = null,
			CancellationToken cancellationToken = default);
	}
}
