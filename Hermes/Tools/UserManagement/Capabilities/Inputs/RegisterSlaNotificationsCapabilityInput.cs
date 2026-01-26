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
		/// Optional area paths to filter work items for SLA violation checks.
		/// If null or empty, all area paths are checked.
		/// Supports multiple area paths for users working across multiple teams/projects.
		/// Example: ["Project\\Team1", "Project\\Team2"]
		/// </summary>
		[JsonPropertyName("areaPaths")]
		public List<string>? AreaPaths { get; init; }
	}
}
