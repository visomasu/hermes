using Hermes.Notifications.WorkItemSla;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Diagnostics;

namespace Hermes.Scheduling.Jobs
{
	/// <summary>
	/// Quartz.NET job that evaluates work items for update frequency SLA violations
	/// and sends digest notifications to users.
	/// </summary>
	[DisallowConcurrentExecution]
	public class WorkItemUpdateSlaJob : IJob
	{
		private readonly IWorkItemUpdateSlaEvaluator _slaEvaluator;
		private readonly ILogger<WorkItemUpdateSlaJob> _logger;

		public WorkItemUpdateSlaJob(
			IWorkItemUpdateSlaEvaluator slaEvaluator,
			ILogger<WorkItemUpdateSlaJob> logger)
		{
			_slaEvaluator = slaEvaluator;
			_logger = logger;
		}

		public async Task Execute(IJobExecutionContext context)
		{
			var stopwatch = Stopwatch.StartNew();
			_logger.LogInformation("Work item update SLA notification job started at {Time} UTC", DateTime.UtcNow);

			try
			{
				var summary = await _slaEvaluator.EvaluateAndNotifyAsync(context.CancellationToken);

				_logger.LogInformation(
					"Work item update SLA notification job completed. " +
					"Processed: {Users}, Violations: {Violations}, " +
					"Sent: {Sent}, Blocked: {Blocked}, Errors: {Errors}, Duration: {Duration}",
					summary.UsersProcessed,
					summary.ViolationsDetected,
					summary.NotificationsSent,
					summary.NotificationsBlocked,
					summary.Errors,
					summary.Duration);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Work item update SLA notification job failed");
				throw; // Let Quartz handle retry
			}
			finally
			{
				stopwatch.Stop();
				_logger.LogInformation("Work item update SLA notification job total duration: {Duration}", stopwatch.Elapsed);
			}
		}
	}
}
