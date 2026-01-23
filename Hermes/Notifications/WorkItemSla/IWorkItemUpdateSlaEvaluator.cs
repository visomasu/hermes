using Hermes.Notifications.WorkItemSla.Models;

namespace Hermes.Notifications.WorkItemSla
{
	/// <summary>
	/// Evaluates work items against SLA thresholds and sends notifications.
	/// </summary>
	public interface IWorkItemUpdateSlaEvaluator
	{
		/// <summary>
		/// Checks all users' work items for SLA violations and sends digest notifications.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Summary of the evaluation run.</returns>
		Task<SlaNotificationRunSummary> EvaluateAndNotifyAsync(
			CancellationToken cancellationToken = default);
	}
}
