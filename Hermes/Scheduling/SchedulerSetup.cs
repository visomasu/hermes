using Hermes.Configuration;
using Quartz;
using Quartz.Spi;

namespace Hermes.Scheduling
{
	/// <summary>
	/// Hosted service that sets up and configures the Quartz.NET scheduler.
	/// Dynamically registers jobs based on appsettings.json configuration.
	/// </summary>
	public class SchedulerSetup : IHostedService
	{
		private readonly ISchedulerFactory _schedulerFactory;
		private readonly SchedulingConfiguration _schedulingConfig;
		private readonly JobTypeResolver _jobTypeResolver;
		private readonly ILogger<SchedulerSetup> _logger;

		public SchedulerSetup(
			ISchedulerFactory schedulerFactory,
			SchedulingConfiguration schedulingConfig,
			JobTypeResolver jobTypeResolver,
			ILogger<SchedulerSetup> logger)
		{
			_schedulerFactory = schedulerFactory;
			_schedulingConfig = schedulingConfig;
			_jobTypeResolver = jobTypeResolver;
			_logger = logger;
		}

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			if (!_schedulingConfig.EnableScheduler)
			{
				_logger.LogInformation("Scheduler is disabled in configuration. No jobs will be scheduled.");
				return;
			}

			_logger.LogInformation("Starting Quartz.NET scheduler...");

			var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

			var enabledJobs = _schedulingConfig.Jobs.Where(j => j.Enabled).ToList();

			if (enabledJobs.Count == 0)
			{
				_logger.LogWarning("No enabled jobs found in configuration.");
				return;
			}

			_logger.LogInformation("Found {Count} enabled jobs to schedule", enabledJobs.Count);

			foreach (var jobConfig in enabledJobs)
			{
				try
				{
					await _ScheduleJobAsync(scheduler, jobConfig, cancellationToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(
						ex,
						"Failed to schedule job '{JobName}' of type '{JobType}'",
						jobConfig.JobName,
						jobConfig.JobType);
					throw;
				}
			}

			_logger.LogInformation("Quartz.NET scheduler started successfully");
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Quartz.NET scheduler stopping...");
			return Task.CompletedTask;
		}

		private async Task _ScheduleJobAsync(
			IScheduler scheduler,
			JobConfiguration jobConfig,
			CancellationToken cancellationToken)
		{
			// Resolve job type from string
			var jobType = _jobTypeResolver.GetJobType(jobConfig.JobType);

			// Create job identity
			var job = JobBuilder.Create(jobType)
				.WithIdentity(jobConfig.JobName, "HermesJobs")
				.WithDescription($"Job Type: {jobConfig.JobType}")
				.Build();

			// Parse timezone
			TimeZoneInfo timeZone;
			try
			{
				timeZone = TimeZoneInfo.FindSystemTimeZoneById(jobConfig.TimeZone);
			}
			catch (TimeZoneNotFoundException ex)
			{
				_logger.LogWarning(
					ex,
					"Timezone '{TimeZone}' not found for job '{JobName}'. Using UTC.",
					jobConfig.TimeZone,
					jobConfig.JobName);
				timeZone = TimeZoneInfo.Utc;
			}

			// Create trigger with cron expression
			var trigger = TriggerBuilder.Create()
				.WithIdentity($"{jobConfig.JobName}Trigger", "HermesJobs")
				.WithDescription($"Cron: {jobConfig.CronExpression} ({timeZone.Id})")
				.WithCronSchedule(jobConfig.CronExpression, builder =>
				{
					builder.InTimeZone(timeZone);
				})
				.Build();

			// Schedule the job
			await scheduler.ScheduleJob(job, trigger, cancellationToken);

			_logger.LogInformation(
				"Scheduled job '{JobName}' (Type: {JobType}) with cron expression '{Cron}' in timezone '{TimeZone}'",
				jobConfig.JobName,
				jobConfig.JobType,
				jobConfig.CronExpression,
				timeZone.Id);
		}
	}
}
