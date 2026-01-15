using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.AzureDevOps.Capabilities.Inputs
{
	/// <summary>
	/// Input model for GetFullHierarchy capability.
	/// </summary>
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public class GetFullHierarchyCapabilityInput : ToolCapabilityInputBase
	{
		/// <summary>
		/// The work item ID to retrieve full hierarchy for (parents + children).
		/// </summary>
		[JsonPropertyName("workItemId")]
		public int WorkItemId { get; set; }

		/// <summary>
		/// Maximum depth to traverse for child items.
		/// If not specified, defaults to 2.
		/// </summary>
		[JsonPropertyName("depth")]
		public int? Depth { get; set; }

		/// <summary>
		/// Optional list of fields to include in the response.
		/// If not specified, default fields will be returned.
		/// </summary>
		[JsonPropertyName("fields")]
		public IEnumerable<string>? Fields { get; set; }
	}
}
