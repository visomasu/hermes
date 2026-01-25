using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.UserManagement.Capabilities.Inputs
{
	/// <summary>
	/// Input model for registering a user for work item update SLA notifications.
	/// </summary>
	public sealed class RegisterSlaNotificationsCapabilityInput : ToolCapabilityInputBase
	{
		/// <summary>
		/// The Teams user ID to register for SLA notifications.
		/// </summary>
		[JsonPropertyName("teamsUserId")]
		public string TeamsUserId { get; init; } = string.Empty;
	}
}
