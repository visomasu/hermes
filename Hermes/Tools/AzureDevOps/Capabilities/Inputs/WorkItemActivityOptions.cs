using System.Text.Json.Serialization;

namespace Hermes.Tools.AzureDevOps.Capabilities.Inputs
{
	/// <summary>
	/// Options for filtering work item activity queries.
	/// </summary>
	public sealed class WorkItemActivityOptions
	{
		/// <summary>
		/// Optional collection of work item states to filter on (e.g., 'Active', 'New').
		/// If null, all states are included.
		/// </summary>
		[JsonPropertyName("states")]
		public string[]? States { get; init; }

		/// <summary>
		/// Optional collection of work item types to filter on (e.g., 'Bug', 'Task', 'User Story').
		/// If null, all types are included.
		/// </summary>
		[JsonPropertyName("workItemTypes")]
		public string[]? WorkItemTypes { get; init; }

		/// <summary>
		/// Optional list of field reference names to include in the response
		/// (e.g., 'System.Id', 'System.Title', 'System.State').
		/// If null, default fields are included.
		/// </summary>
		[JsonPropertyName("fields")]
		public string[]? Fields { get; init; }
	}
}
