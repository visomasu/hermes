using System.Text.Json;
using Hermes.Integrations.MicrosoftGraph;
using Hermes.Storage.Repositories.TeamConfiguration;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;
using Hermes.Tools.UserManagement.Capabilities.Inputs;
using Microsoft.Extensions.Logging;

namespace Hermes.Tools.UserManagement.Capabilities
{
	/// <summary>
	/// Capability for registering users to receive work item update SLA notifications.
	/// Fetches user profile from Microsoft Graph and stores registration in UserConfiguration.
	/// Supports multi-team subscriptions where each team has its own SLA rules and iteration windows.
	/// </summary>
	public sealed class RegisterSlaNotificationsCapability
		: IAgentToolCapability<RegisterSlaNotificationsCapabilityInput>
	{
		private readonly IMicrosoftGraphClient _graphClient;
		private readonly IUserConfigurationRepository _userConfigRepo;
		private readonly ITeamConfigurationRepository _teamConfigRepo;
		private readonly ILogger<RegisterSlaNotificationsCapability> _logger;

		public RegisterSlaNotificationsCapability(
			ILogger<RegisterSlaNotificationsCapability> logger,
			IMicrosoftGraphClient graphClient,
			IUserConfigurationRepository userConfigRepo,
			ITeamConfigurationRepository teamConfigRepo)
		{
			_logger = logger;
			_graphClient = graphClient;
			_userConfigRepo = userConfigRepo;
			_teamConfigRepo = teamConfigRepo;
		}

		/// <inheritdoc />
		public string Name => "RegisterSlaNotifications";

		/// <inheritdoc />
		public string Description => "Register user to receive daily work item update SLA violation notifications for their work items and direct reports";

		/// <inheritdoc />
		public async Task<string> ExecuteAsync(RegisterSlaNotificationsCapabilityInput input)
		{
			if (string.IsNullOrWhiteSpace(input.TeamsUserId))
			{
				return JsonSerializer.Serialize(new
				{
					success = false,
					message = "TeamsUserId is required"
				});
			}

			try
			{
				_logger.LogInformation("Registering user {TeamsUserId} for SLA notifications", input.TeamsUserId);

				// 1. Get or create user configuration
				var userConfig = await _userConfigRepo.GetByTeamsUserIdAsync(input.TeamsUserId);
				var isNewUser = userConfig == null;

				if (isNewUser)
				{
					userConfig = CreateNewUserConfig(input.TeamsUserId);
				}

				// 2. Fetch profile from Microsoft Graph
				var profile = await _graphClient.GetUserProfileWithDirectReportsAsync(input.TeamsUserId);

				if (string.IsNullOrWhiteSpace(profile.Email))
				{
					_logger.LogWarning("Could not retrieve email for user {TeamsUserId} from Microsoft Graph", input.TeamsUserId);
					return JsonSerializer.Serialize(new
					{
						success = false,
						message = "Could not retrieve your email from Azure AD. Please contact support."
					});
				}

				// 3. Determine team subscriptions
				var subscribedTeamIds = await _DetermineTeamSubscriptionsAsync(input, userConfig);

				if (subscribedTeamIds.Count == 0)
				{
					_logger.LogWarning("No teams found for user {TeamsUserId}", input.TeamsUserId);
					return JsonSerializer.Serialize(new
					{
						success = false,
						message = "No teams were specified or found. Please specify which teams you'd like to subscribe to."
					});
				}

				// 4. Validate team IDs exist
				var allTeams = await _teamConfigRepo.GetAllTeamsAsync();
				var validTeamIds = subscribedTeamIds
					.Where(teamId => allTeams.Any(t => t.TeamId == teamId))
					.ToList();

				if (validTeamIds.Count == 0)
				{
					var availableTeams = string.Join(", ", allTeams.Select(t => t.TeamId));
					_logger.LogWarning("No valid team IDs found for user {TeamsUserId}. Available teams: {Teams}", input.TeamsUserId, availableTeams);
					return JsonSerializer.Serialize(new
					{
						success = false,
						message = $"None of the specified teams were found. Available teams: {availableTeams}"
					});
				}

				// 5. Get team names for response
				var subscribedTeams = allTeams
					.Where(t => validTeamIds.Contains(t.TeamId))
					.Select(t => new { t.TeamId, t.TeamName })
					.ToList();

				// 6. Update SLA registration profile
				userConfig!.SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = profile.Email,
					DirectReportEmails = profile.DirectReportEmails,
					SubscribedTeamIds = validTeamIds,
					RegisteredAt = DateTime.UtcNow,
					DirectReportsLastRefreshedAt = DateTime.UtcNow
				};

				// 7. Enable SLA notifications in preferences
				userConfig.Notifications.SlaViolationNotifications = true;
				userConfig.UpdatedAt = DateTime.UtcNow;

				// 8. Save to storage
				if (isNewUser)
				{
					await _userConfigRepo.CreateAsync(userConfig);
					_logger.LogInformation("Created new user configuration for {TeamsUserId}", input.TeamsUserId);
				}
				else
				{
					await _userConfigRepo.UpdateAsync(userConfig.Id, userConfig);
					_logger.LogInformation("Updated user configuration for {TeamsUserId}", input.TeamsUserId);
				}

				// 9. Return success response
				var teamMessage = subscribedTeams.Count > 0
					? $" Subscribed to {subscribedTeams.Count} team(s): {string.Join(", ", subscribedTeams.Select(t => t.TeamName))}."
					: "";

				var baseMessage = profile.IsManager
					? $"✅ Registered successfully! You'll receive daily SLA reports for your team ({profile.DirectReportEmails.Count} direct reports) and your own work items."
					: "✅ Registered successfully! You'll receive daily SLA reports for your work items.";

				var response = new
				{
					success = true,
					message = baseMessage + teamMessage,
					email = profile.Email,
					isManager = profile.IsManager,
					directReportCount = profile.DirectReportEmails.Count,
					teams = subscribedTeams
				};

				_logger.LogInformation(
					"Successfully registered user {TeamsUserId} for SLA notifications (IsManager: {IsManager}, DirectReports: {Count}, Teams: {Teams})",
					input.TeamsUserId,
					profile.IsManager,
					profile.DirectReportEmails.Count,
					string.Join(", ", validTeamIds));

				return JsonSerializer.Serialize(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to register user {TeamsUserId} for SLA notifications", input.TeamsUserId);
				return JsonSerializer.Serialize(new
				{
					success = false,
					message = "An error occurred during registration. Please try again later."
				});
			}
		}

		/// <summary>
		/// Determines which teams the user should be subscribed to.
		/// Priority: input.TeamIds > migration from existing AreaPaths > input.AreaPaths (legacy).
		/// </summary>
		private async Task<List<string>> _DetermineTeamSubscriptionsAsync(
			RegisterSlaNotificationsCapabilityInput input,
			UserConfigurationDocument? userConfig)
		{
			// Priority 1: TeamIds from input
			if (input.TeamIds?.Any() == true)
			{
				return input.TeamIds.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
			}

			// Priority 2: Migrate existing user's AreaPaths to teams (best-effort)
			if (userConfig?.SlaRegistration?.AreaPaths?.Any() == true)
			{
				_logger.LogInformation(
					"Migrating existing AreaPaths to team subscriptions for user {TeamsUserId}",
					userConfig.TeamsUserId);

				var allTeams = await _teamConfigRepo.GetAllTeamsAsync();
				var migratedTeamIds = allTeams
					.Where(t => userConfig.SlaRegistration.AreaPaths.Any(ap =>
						t.AreaPaths.Any(tap => ap.StartsWith(tap, StringComparison.OrdinalIgnoreCase))))
					.Select(t => t.TeamId)
					.ToList();

				if (migratedTeamIds.Any())
				{
					_logger.LogInformation(
						"Migrated {Count} area paths to {TeamCount} teams for user {TeamsUserId}",
						userConfig.SlaRegistration.AreaPaths.Count,
						migratedTeamIds.Count,
						userConfig.TeamsUserId);

					return migratedTeamIds;
				}
			}

#pragma warning disable CS0618 // Type or member is obsolete
			// Priority 3: Legacy AreaPaths from input (convert to teams, best-effort)
			if (input.AreaPaths?.Any() == true)
			{
				_logger.LogInformation("Converting legacy AreaPaths input to team subscriptions");

				var allTeams = await _teamConfigRepo.GetAllTeamsAsync();
				var convertedTeamIds = allTeams
					.Where(t => input.AreaPaths.Any(ap =>
						t.AreaPaths.Any(tap => ap.StartsWith(tap, StringComparison.OrdinalIgnoreCase))))
					.Select(t => t.TeamId)
					.ToList();

				return convertedTeamIds;
			}
#pragma warning restore CS0618 // Type or member is obsolete

			return new List<string>();
		}

		private UserConfigurationDocument CreateNewUserConfig(string teamsUserId)
		{
			return new UserConfigurationDocument
			{
				Id = teamsUserId,
				PartitionKey = teamsUserId,
				TeamsUserId = teamsUserId,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};
		}
	}
}
