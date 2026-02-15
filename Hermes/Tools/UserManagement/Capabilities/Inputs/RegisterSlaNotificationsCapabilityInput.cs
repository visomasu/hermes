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

		/// <summary>
		/// Team IDs to subscribe to for SLA notifications.
		/// If null or empty, user will be prompted to select teams.
		/// Example: ["contact-center-ai", "auth-antifraud"]
		/// </summary>
		[JsonPropertyName("teamIds")]
		public List<string>? TeamIds { get; init; }

		/// <summary>
		/// Legacy area paths property (deprecated).
		/// Kept for backwards compatibility.
		/// Use TeamIds instead - area paths are now configured per team.
		/// </summary>
		[Obsolete("Use TeamIds instead. Area paths are now configured per team.")]
		[JsonPropertyName("areaPaths")]
		public List<string>? AreaPaths { get; init; }
	}
}
