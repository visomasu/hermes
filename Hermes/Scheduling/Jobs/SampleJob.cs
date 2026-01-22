using Quartz;

namespace Hermes.Scheduling.Jobs
{
	/// <summary>
	/// Sample job for testing the Quartz.NET scheduling infrastructure.
	/// Logs a simple message and exits.
	/// </summary>
	public class SampleJob : IJob
	{
		private readonly ILogger<SampleJob> _logger;

		public SampleJob(ILogger<SampleJob> logger)
		{
			_logger = logger;
		}

		public Task Execute(IJobExecutionContext context)
		{
			_logger.LogInformation("SampleJob executed at {Time} UTC", DateTime.UtcNow);
			return Task.CompletedTask;
		}
	}
}
