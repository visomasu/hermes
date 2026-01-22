namespace Hermes.Configuration
{
	/// <summary>
	/// Configuration for the Quartz.NET job scheduling system.
	/// </summary>
	public class SchedulingConfiguration
	{
		/// <summary>
		/// Whether the scheduler is enabled.
		/// </summary>
		public bool EnableScheduler { get; set; } = true;

		/// <summary>
		/// List of job configurations.
		/// </summary>
		public List<JobConfiguration> Jobs { get; set; } = new();
	}
}
