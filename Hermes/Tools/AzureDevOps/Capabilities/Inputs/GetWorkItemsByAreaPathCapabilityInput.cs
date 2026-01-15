using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.AzureDevOps.Capabilities.Inputs
{
	/// <summary>
	/// Input model for retrieving Azure DevOps work items by area path.
	/// </summary>
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public sealed class GetWorkItemsByAreaPathCapabilityInput : ToolCapabilityInputBase
	{
		[JsonPropertyName("areaPath")]
		public string AreaPath { get; init; } = string.Empty;

		[JsonPropertyName("workItemTypes")]
		public string[]? WorkItemTypes { get; init; }
		
		[JsonPropertyName("fields")]
		public string[]? Fields { get; init; }

		[JsonPropertyName("pageNumber")]
		public int? PageNumber { get; init; }

		[JsonPropertyName("pageSize")]
		public int? PageSize { get; init; }
	}
}
