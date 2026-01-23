using Hermes.Scheduling.Jobs;
using Quartz;

namespace Hermes.Scheduling
{
	/// <summary>
	/// Resolves job type strings to actual C# types for dynamic job registration.
	/// </summary>
	public class JobTypeResolver
	{
		private readonly Dictionary<string, Type> _jobTypes;

		public JobTypeResolver()
		{
			_jobTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
			{
				{ "Sample", typeof(SampleJob) },
				{ "WorkItemUpdateSla", typeof(WorkItemUpdateSlaJob) }
			};
		}

		/// <summary>
		/// Gets the job type for the specified job type name.
		/// </summary>
		/// <param name="jobTypeName">The job type name from configuration (e.g., "SlaNotification").</param>
		/// <returns>The corresponding C# type.</returns>
		/// <exception cref="ArgumentException">Thrown when job type is not found.</exception>
		public Type GetJobType(string jobTypeName)
		{
			if (string.IsNullOrWhiteSpace(jobTypeName))
			{
				throw new ArgumentException("Job type name cannot be null or empty.", nameof(jobTypeName));
			}

			if (!_jobTypes.TryGetValue(jobTypeName, out var jobType))
			{
				throw new ArgumentException(
					$"Unknown job type: '{jobTypeName}'. " +
					$"Available job types: {string.Join(", ", _jobTypes.Keys)}",
					nameof(jobTypeName));
			}

			return jobType;
		}

		/// <summary>
		/// Registers a new job type.
		/// </summary>
		/// <param name="jobTypeName">The job type name (e.g., "SlaNotification").</param>
		/// <param name="jobType">The job type (must implement IJob).</param>
		public void RegisterJobType(string jobTypeName, Type jobType)
		{
			if (string.IsNullOrWhiteSpace(jobTypeName))
			{
				throw new ArgumentException("Job type name cannot be null or empty.", nameof(jobTypeName));
			}

			if (jobType == null)
			{
				throw new ArgumentNullException(nameof(jobType));
			}

			if (!typeof(IJob).IsAssignableFrom(jobType))
			{
				throw new ArgumentException(
					$"Job type '{jobType.FullName}' must implement IJob interface.",
					nameof(jobType));
			}

			_jobTypes[jobTypeName] = jobType;
		}

		/// <summary>
		/// Gets all registered job type names.
		/// </summary>
		public IEnumerable<string> GetRegisteredJobTypes() => _jobTypes.Keys;
	}
}
