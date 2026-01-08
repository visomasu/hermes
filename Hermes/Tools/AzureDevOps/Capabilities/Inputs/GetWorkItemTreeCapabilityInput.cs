using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.AzureDevOps.Capabilities.Inputs
{
	/// <summary>
	/// Input model for retrieving an Azure DevOps work item tree.
	/// </summary>
	public sealed class GetWorkItemTreeCapabilityInput : ToolCapabilityInputBase
	{
		/// <summary>
		/// Work item ID to start the tree from.
		/// </summary>
		[JsonPropertyName("workItemId")]
		public int WorkItemId { get; init; }

		/// <summary>
		/// Maximum depth to traverse from the root.
		/// </summary>
		[JsonPropertyName("depth")]
		public int Depth { get; init; }
	}
}
