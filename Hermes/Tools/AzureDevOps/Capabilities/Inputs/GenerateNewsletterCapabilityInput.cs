using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.AzureDevOps.Capabilities.Inputs
{
	/// <summary>
	/// Input model for generating an executive newsletter from an Azure DevOps work item hierarchy.
	/// </summary>
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public sealed class GenerateNewsletterCapabilityInput : ToolCapabilityInputBase
	{
		/// <summary>
		/// Work item ID (typically a Feature or Epic) to generate the newsletter from.
		/// </summary>
		[JsonPropertyName("workItemId")]
		public int WorkItemId { get; init; }

		/// <summary>
		/// Optional: Newsletter format style. Defaults to "Executive".
		/// Future values: "Technical", "Brief"
		/// </summary>
		[JsonPropertyName("format")]
		public string? Format { get; init; } = "Executive";

		/// <summary>
		/// Optional: Maximum depth to traverse the work item hierarchy. Defaults to 3.
		/// </summary>
		[JsonPropertyName("depth")]
		public int Depth { get; init; } = 3;
	}
}
