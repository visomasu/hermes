using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.UserManagement.Capabilities.Inputs
{
	/// <summary>
	/// Input model for unregistering a user from work item update SLA notifications.
	/// </summary>
	public sealed class UnregisterSlaNotificationsCapabilityInput : ToolCapabilityInputBase
	{
		/// <summary>
		/// The Teams user ID to unregister from SLA notifications.
		/// </summary>
		[JsonPropertyName("teamsUserId")]
		public string TeamsUserId { get; init; } = string.Empty;
	}
}
