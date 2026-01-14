using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.AzureDevOps.Capabilities.Inputs
{
	/// <summary>
	/// Input model for GetParentHierarchy capability.
	/// </summary>
	public class GetParentHierarchyCapabilityInput : ToolCapabilityInputBase
	{
		/// <summary>
		/// The work item ID to retrieve parent hierarchy for.
		/// </summary>
		[JsonPropertyName("workItemId")]
		public int WorkItemId { get; set; }

		/// <summary>
		/// Optional list of fields to include in the response.
		/// If not specified, default fields will be returned.
		/// </summary>
		[JsonPropertyName("fields")]
		public IEnumerable<string>? Fields { get; set; }
	}
}
