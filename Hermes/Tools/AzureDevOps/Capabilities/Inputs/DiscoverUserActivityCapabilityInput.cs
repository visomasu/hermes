using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.AzureDevOps.Capabilities.Inputs
{
	/// <summary>
	/// Input model for discovering user activity in Azure DevOps and integrated services.
	/// </summary>
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public sealed class DiscoverUserActivityCapabilityInput : ToolCapabilityInputBase
	{
		/// <summary>
		/// The email address of the user to discover activity for.
		/// </summary>
		[JsonPropertyName("userEmail")]
		public string UserEmail { get; init; } = string.Empty;

		/// <summary>
		/// Number of days to look back for activity. Default is 7.
		/// </summary>
		[JsonPropertyName("daysBack")]
		public int DaysBack { get; init; } = 7;

		/// <summary>
		/// Types of activity to include. Default is all work item activity.
		/// Can be combined using flags (e.g., WorkItemsAssigned | WorkItemsChanged).
		/// </summary>
		[JsonPropertyName("activityTypes")]
		public UserActivityType ActivityTypes { get; init; } = UserActivityType.AllWorkItems;

		/// <summary>
		/// Options for filtering work item activity queries.
		/// Only applies when work item activity types are requested.
		/// </summary>
		[JsonPropertyName("workItemOptions")]
		public WorkItemActivityOptions? WorkItemOptions { get; init; }

		// Future activity options can be added here:
		// public PullRequestActivityOptions? PullRequestOptions { get; init; }
		// public CodeActivityOptions? CodeOptions { get; init; }
		// public DocumentActivityOptions? DocumentOptions { get; init; }
	}
}
