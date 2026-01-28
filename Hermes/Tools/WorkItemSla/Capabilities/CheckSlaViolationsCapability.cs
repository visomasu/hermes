using System.Text.Json;
using Hermes.Domain.WorkItemSla;
using Hermes.Domain.WorkItemSla.Models;
using Hermes.Integrations.MicrosoftGraph;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Tools.WorkItemSla.Capabilities.Inputs;
using Microsoft.Extensions.Logging;

namespace Hermes.Tools.WorkItemSla.Capabilities
{
	/// <summary>
	/// Capability for checking work item update SLA violations on-demand.
	/// Works for both registered and unregistered users (fetches from Graph if needed).
	/// </summary>
	public sealed class CheckSlaViolationsCapability
		: IAgentToolCapability<CheckSlaViolationsCapabilityInput>
	{
		private readonly IWorkItemUpdateSlaEvaluator _slaEvaluator;
		private readonly IMicrosoftGraphClient _graphClient;
		private readonly IUserConfigurationRepository _userConfigRepo;
		private readonly ILogger<CheckSlaViolationsCapability> _logger;

		public CheckSlaViolationsCapability(
			IWorkItemUpdateSlaEvaluator slaEvaluator,
			IMicrosoftGraphClient graphClient,
			IUserConfigurationRepository userConfigRepo,
			ILogger<CheckSlaViolationsCapability> logger)
		{
			_slaEvaluator = slaEvaluator;
			_graphClient = graphClient;
			_userConfigRepo = userConfigRepo;
			_logger = logger;
		}

		/// <inheritdoc />
		public string Name => "CheckSlaViolations";

		/// <inheritdoc />
		public string Description => "Check work item update SLA violations for user and their direct reports (if manager)";

		/// <inheritdoc />
		public async Task<string> ExecuteAsync(CheckSlaViolationsCapabilityInput input)
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
				_logger.LogInformation("Checking SLA violations for user {TeamsUserId}", input.TeamsUserId);

				// 1. Get user profile (try storage first, fallback to Graph)
				var userProfile = await GetOrFetchUserProfileAsync(input.TeamsUserId);

				if (string.IsNullOrWhiteSpace(userProfile.Email))
				{
					_logger.LogWarning("Could not retrieve email for user {TeamsUserId}", input.TeamsUserId);
					return JsonSerializer.Serialize(new
					{
						success = false,
						message = "Could not retrieve your email. Please try again."
					});
				}

				// 2. Determine emails to check (user + directs if manager)
				var emailsToCheck = new List<string> { userProfile.Email };
				if (userProfile.IsManager)
				{
					emailsToCheck.AddRange(userProfile.DirectReportEmails);
				}

				_logger.LogInformation(
					"Checking SLA violations for {Count} email(s) (IsManager: {IsManager})",
					emailsToCheck.Count,
					userProfile.IsManager);

				// 3. Check violations for all emails in parallel
				var violationTasks = emailsToCheck.Select(async email =>
				{
					var violations = await _slaEvaluator.CheckViolationsForEmailAsync(email, userProfile.AreaPaths);
					return new { Email = email, Violations = violations };
				}).ToArray();

				var results = await Task.WhenAll(violationTasks);

				// Aggregate results
				var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>();
				foreach (var result in results)
				{
					if (result.Violations.Count > 0)
					{
						violationsByOwner[result.Email] = result.Violations;
					}
				}

				// 4. Compose response
				if (violationsByOwner.Count == 0)
				{
					_logger.LogInformation("No SLA violations found for user {TeamsUserId}", input.TeamsUserId);
					return JsonSerializer.Serialize(new
					{
						success = true,
						message = userProfile.IsManager
							? "✅ No SLA violations found for you or your direct reports!"
							: "✅ No SLA violations found for your work items!",
						isManager = userProfile.IsManager,
						directReportCount = userProfile.DirectReportEmails.Count,
						violations = new Dictionary<string, List<WorkItemUpdateSlaViolation>>()
					});
				}

				var totalViolations = violationsByOwner.Sum(kvp => kvp.Value.Count);
				var message = userProfile.IsManager
					? $"⚠️ Found {totalViolations} SLA violation(s) across your team"
					: $"⚠️ Found {totalViolations} SLA violation(s) for your work items";

				_logger.LogInformation(
					"Found {TotalCount} SLA violations for user {TeamsUserId} ({OwnerCount} owners)",
					totalViolations,
					input.TeamsUserId,
					violationsByOwner.Count);

				return JsonSerializer.Serialize(new
				{
					success = true,
					message,
					isManager = userProfile.IsManager,
					directReportCount = userProfile.DirectReportEmails.Count,
					violations = violationsByOwner
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to check SLA violations for user {TeamsUserId}", input.TeamsUserId);
				return JsonSerializer.Serialize(new
				{
					success = false,
					message = "An error occurred while checking violations. Please try again."
				});
			}
		}

		/// <summary>
		/// Gets user profile from storage if registered, otherwise fetches from Microsoft Graph.
		/// </summary>
		private async Task<UserProfileResult> GetOrFetchUserProfileAsync(string teamsUserId)
		{
			// Try storage first (if user is registered)
			var userConfig = await _userConfigRepo.GetByTeamsUserIdAsync(teamsUserId);

			if (userConfig?.SlaRegistration != null && userConfig.SlaRegistration.IsRegistered)
			{
				_logger.LogDebug("Using cached profile from UserConfiguration for {TeamsUserId}", teamsUserId);
				return new UserProfileResult
				{
					Email = userConfig.SlaRegistration.AzureDevOpsEmail,
					DirectReportEmails = userConfig.SlaRegistration.DirectReportEmails,
					AreaPaths = userConfig.SlaRegistration.AreaPaths
				};
			}

			// Fallback: Fetch from Graph for one-time check
			_logger.LogDebug("Fetching profile from Microsoft Graph for {TeamsUserId} (not registered)", teamsUserId);
			return await _graphClient.GetUserProfileWithDirectReportsAsync(teamsUserId);
		}
	}
}
