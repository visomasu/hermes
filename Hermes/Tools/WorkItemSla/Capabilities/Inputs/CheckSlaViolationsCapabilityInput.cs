using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.WorkItemSla.Capabilities.Inputs
{
	/// <summary>
	/// Input model for checking work item update SLA violations for a user.
	/// </summary>
	public sealed class CheckSlaViolationsCapabilityInput : ToolCapabilityInputBase
	{
		/// <summary>
		/// The Teams user ID to check SLA violations for.
		/// </summary>
		[JsonPropertyName("teamsUserId")]
		public string TeamsUserId { get; init; } = string.Empty;
	}
}
