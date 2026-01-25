using System.Text.Json;
using Hermes.Integrations.MicrosoftGraph;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;
using Hermes.Tools.UserManagement.Capabilities.Inputs;
using Microsoft.Extensions.Logging;

namespace Hermes.Tools.UserManagement.Capabilities
{
	/// <summary>
	/// Capability for registering users to receive work item update SLA notifications.
	/// Fetches user profile from Microsoft Graph and stores registration in UserConfiguration.
	/// </summary>
	public sealed class RegisterSlaNotificationsCapability
		: IAgentToolCapability<RegisterSlaNotificationsCapabilityInput>
	{
		private readonly IMicrosoftGraphClient _graphClient;
		private readonly IUserConfigurationRepository _userConfigRepo;
		private readonly ILogger<RegisterSlaNotificationsCapability> _logger;

		public RegisterSlaNotificationsCapability(
			IMicrosoftGraphClient graphClient,
			IUserConfigurationRepository userConfigRepo,
			ILogger<RegisterSlaNotificationsCapability> logger)
		{
			_graphClient = graphClient;
			_userConfigRepo = userConfigRepo;
			_logger = logger;
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

				// 3. Update SLA registration profile
				userConfig!.SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = profile.Email,
					DirectReportEmails = profile.DirectReportEmails,
					RegisteredAt = DateTime.UtcNow,
					DirectReportsLastRefreshedAt = DateTime.UtcNow
				};

				// 4. Enable SLA notifications in preferences
				userConfig.Notifications.SlaViolationNotifications = true;
				userConfig.UpdatedAt = DateTime.UtcNow;

				// 5. Save to storage
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

				// 6. Return success response
				var response = new
				{
					success = true,
					message = profile.IsManager
						? $"✅ Registered successfully! You'll receive daily SLA reports for your team ({profile.DirectReportEmails.Count} direct reports) and your own work items."
						: "✅ Registered successfully! You'll receive daily SLA reports for your work items.",
					email = profile.Email,
					isManager = profile.IsManager,
					directReportCount = profile.DirectReportEmails.Count
				};

				_logger.LogInformation(
					"Successfully registered user {TeamsUserId} for SLA notifications (IsManager: {IsManager}, DirectReports: {Count})",
					input.TeamsUserId,
					profile.IsManager,
					profile.DirectReportEmails.Count);

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
