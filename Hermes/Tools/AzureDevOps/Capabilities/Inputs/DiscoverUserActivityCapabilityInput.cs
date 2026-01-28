using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.AzureDevOps.Capabilities.Inputs
{
	/// <summary>
	/// Input model for discovering user pull request activity in Azure DevOps.
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
	}
}
