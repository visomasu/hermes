using System.Text.Json;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Tools.UserManagement.Capabilities.Inputs;
using Microsoft.Extensions.Logging;

namespace Hermes.Tools.UserManagement.Capabilities
{
	/// <summary>
	/// Capability for unregistering users from work item update SLA notifications.
	/// Sets IsRegistered flag to false while preserving registration data.
	/// </summary>
	public sealed class UnregisterSlaNotificationsCapability
		: IAgentToolCapability<UnregisterSlaNotificationsCapabilityInput>
	{
		private readonly IUserConfigurationRepository _userConfigRepo;
		private readonly ILogger<UnregisterSlaNotificationsCapability> _logger;

		public UnregisterSlaNotificationsCapability(
			IUserConfigurationRepository userConfigRepo,
			ILogger<UnregisterSlaNotificationsCapability> logger)
		{
			_userConfigRepo = userConfigRepo;
			_logger = logger;
		}

		/// <inheritdoc />
		public string Name => "UnregisterSlaNotifications";

		/// <inheritdoc />
		public string Description => "Unregister user from receiving daily work item update SLA violation notifications";

		/// <inheritdoc />
		public async Task<string> ExecuteAsync(UnregisterSlaNotificationsCapabilityInput input)
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
				_logger.LogInformation("Unregistering user {TeamsUserId} from SLA notifications", input.TeamsUserId);

				var userConfig = await _userConfigRepo.GetByTeamsUserIdAsync(input.TeamsUserId);

				if (userConfig?.SlaRegistration == null || !userConfig.SlaRegistration.IsRegistered)
				{
					_logger.LogInformation("User {TeamsUserId} is not currently registered for SLA notifications", input.TeamsUserId);
					return JsonSerializer.Serialize(new
					{
						success = false,
						message = "You are not currently registered for SLA notifications."
					});
				}

				userConfig.SlaRegistration.IsRegistered = false;
				userConfig.Notifications.SlaViolationNotifications = false;
				userConfig.UpdatedAt = DateTime.UtcNow;

				await _userConfigRepo.UpdateAsync(userConfig.Id, userConfig);

				_logger.LogInformation("Successfully unregistered user {TeamsUserId} from SLA notifications", input.TeamsUserId);

				return JsonSerializer.Serialize(new
				{
					success = true,
					message = "✅ Unregistered successfully. You will no longer receive SLA violation notifications."
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to unregister user {TeamsUserId} from SLA notifications", input.TeamsUserId);
				return JsonSerializer.Serialize(new
				{
					success = false,
					message = "An error occurred during unregistration. Please try again later."
				});
			}
		}
	}
}
