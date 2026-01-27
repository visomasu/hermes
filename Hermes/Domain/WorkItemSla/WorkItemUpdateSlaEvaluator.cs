using Hermes.Domain.WorkItemSla.Models;
using Hermes.Notifications.Infra;
using Hermes.Notifications.WorkItemSla;
using Hermes.Notifications.WorkItemSla.Models;
using Hermes.Storage.Repositories.UserConfiguration;
using Integrations.AzureDevOps;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Hermes.Domain.WorkItemSla
{
	/// <summary>
	/// Evaluates work items against update frequency SLA thresholds and sends notifications.
	/// </summary>
	public class WorkItemUpdateSlaEvaluator : IWorkItemUpdateSlaEvaluator
	{
		private readonly IUserConfigurationRepository _userConfigRepo;
		private readonly IAzureDevOpsWorkItemClient _azureDevOpsClient;
		private readonly INotificationGate _notificationGate;
		private readonly IProactiveMessenger _proactiveMessenger;
		private readonly WorkItemUpdateSlaConfiguration _configuration;
		private readonly WorkItemUpdateSlaMessageComposer _messageComposer;
		private readonly ILogger<WorkItemUpdateSlaEvaluator> _logger;

		public WorkItemUpdateSlaEvaluator(
			IUserConfigurationRepository userConfigRepo,
			IAzureDevOpsWorkItemClient azureDevOpsClient,
			INotificationGate notificationGate,
			IProactiveMessenger proactiveMessenger,
			WorkItemUpdateSlaConfiguration configuration,
			WorkItemUpdateSlaMessageComposer messageComposer,
			ILogger<WorkItemUpdateSlaEvaluator> logger)
		{
			_userConfigRepo = userConfigRepo;
			_azureDevOpsClient = azureDevOpsClient;
			_notificationGate = notificationGate;
			_proactiveMessenger = proactiveMessenger;
			_configuration = configuration;
			_messageComposer = messageComposer;
			_logger = logger;
		}

		/// <inheritdoc/>
		public async Task<SlaNotificationRunSummary> EvaluateAndNotifyAsync(
			CancellationToken cancellationToken = default)
		{
			var stopwatch = Stopwatch.StartNew();
			var summary = new SlaNotificationRunSummary();

			try
			{
				// Check if SLA notifications are enabled
				if (!_configuration.Enabled)
				{
					_logger.LogInformation("SLA notifications are disabled in configuration");
					return summary;
				}

				// Get all registered users from UserConfiguration
				var registeredUsers = await _userConfigRepo.GetAllWithSlaRegistrationAsync(cancellationToken);

				if (registeredUsers == null || registeredUsers.Count == 0)
				{
					_logger.LogInformation("No registered users found for SLA notifications");
					return summary;
				}

				_logger.LogInformation("Processing {Count} registered users for SLA violations", registeredUsers.Count);

				// Process each user
				foreach (var userConfig in registeredUsers)
				{
					// Check if we've hit the max notifications limit
					if (summary.NotificationsSent >= _configuration.MaxNotificationsPerRun)
					{
						_logger.LogWarning("Reached max notifications per run ({Max}), stopping evaluation", _configuration.MaxNotificationsPerRun);
						break;
					}

					summary.UsersProcessed++;

					try
					{
						await _ProcessUserAsync(userConfig, summary, cancellationToken);
					}
					catch (Exception ex)
					{
						summary.Errors++;
						_logger.LogError(ex, "Error processing user {TeamsUserId}", userConfig.TeamsUserId);
					}
				}
			}
			finally
			{
				stopwatch.Stop();
				summary.Duration = stopwatch.Elapsed;
			}

			return summary;
		}

		/// <inheritdoc/>
		public async Task<List<WorkItemUpdateSlaViolation>> CheckViolationsForEmailAsync(
			string email,
			IEnumerable<string>? areaPaths = null,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(email))
			{
				_logger.LogWarning("CheckViolationsForEmailAsync called with empty email");
				return new List<WorkItemUpdateSlaViolation>();
			}

			_logger.LogDebug("Checking SLA violations for email {Email} with area paths {AreaPaths}",
				email,
				areaPaths != null && areaPaths.Any() ? string.Join(", ", areaPaths) : "all");

			// Get work item types we care about from SLA rules
			var workItemTypes = _configuration.SlaRules.Keys.ToList();

			// Determine iteration path (dynamic current iteration or static path)
			string? iterationPath = _configuration.IterationPath;
			if (!string.IsNullOrWhiteSpace(_configuration.TeamName))
			{
				try
				{
					iterationPath = await _azureDevOpsClient.GetCurrentIterationPathAsync(_configuration.TeamName, cancellationToken);
					_logger.LogDebug("Dynamically determined current iteration: {IterationPath}", iterationPath ?? "none");
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Failed to get current iteration for team {TeamName}, using configured path: {IterationPath}",
						_configuration.TeamName, _configuration.IterationPath);
					// Fall back to configured iteration path
				}
			}

			// Query Azure DevOps for assigned work items
			var workItemsJson = await _azureDevOpsClient.GetWorkItemsByAssignedUserAsync(
				email,
				new[] { "Active", "New" },
				new[] { "System.Id", "System.Title", "System.WorkItemType", "System.ChangedDate" },
				iterationPath,
				areaPaths,
				workItemTypes,
				cancellationToken);

			if (string.IsNullOrWhiteSpace(workItemsJson))
			{
				_logger.LogDebug("No work items found for email {Email}", email);
				return new List<WorkItemUpdateSlaViolation>();
			}

			// Parse work items and calculate violations
			var violations = _CalculateViolations(workItemsJson);

			_logger.LogDebug("Found {Count} SLA violations for email {Email}", violations.Count, email);

			return violations;
		}

		/// <summary>
		/// Processes a single user for SLA violations.
		/// </summary>
		private async Task _ProcessUserAsync(
			UserConfigurationDocument userConfig,
			SlaNotificationRunSummary summary,
			CancellationToken cancellationToken)
		{
			// Validate user configuration
			if (userConfig.SlaRegistration == null || !userConfig.SlaRegistration.IsRegistered)
			{
				_logger.LogWarning("User {TeamsUserId} has null or unregistered SlaRegistration, skipping", userConfig.TeamsUserId);
				return;
			}

			var profile = userConfig.SlaRegistration;

			if (string.IsNullOrWhiteSpace(profile.AzureDevOpsEmail))
			{
				_logger.LogWarning("User {TeamsUserId} has empty AzureDevOpsEmail, skipping", userConfig.TeamsUserId);
				return;
			}

			_logger.LogDebug("Processing user {Email} (Teams user {TeamsUserId}, IsManager: {IsManager})",
				profile.AzureDevOpsEmail, userConfig.TeamsUserId, profile.IsManager);

			// Determine emails to check (user + directs if manager)
			var emailsToCheck = new List<string> { profile.AzureDevOpsEmail };
			if (profile.IsManager)
			{
				emailsToCheck.AddRange(profile.DirectReportEmails);
			}

			_logger.LogInformation("Checking SLA violations for {Count} email(s) (user: {Email}, IsManager: {IsManager})",
				emailsToCheck.Count, profile.AzureDevOpsEmail, profile.IsManager);

			// Check violations for all emails in parallel
			var violationTasks = emailsToCheck.Select(async email =>
			{
				var violations = await CheckViolationsForEmailAsync(email, profile.AreaPaths, cancellationToken);
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

			if (violationsByOwner.Count == 0)
			{
				_logger.LogDebug("No SLA violations found for user {Email}", profile.AzureDevOpsEmail);
				return;
			}

			var totalViolations = violationsByOwner.Sum(kvp => kvp.Value.Count);
			summary.ViolationsDetected += totalViolations;
			_logger.LogInformation("Found {TotalCount} SLA violations for user {Email} ({OwnerCount} owners)",
				totalViolations, profile.AzureDevOpsEmail, violationsByOwner.Count);

			// Evaluate NotificationGate (unless bypassed for testing)
			if (!_configuration.BypassGates)
			{
				var gateResult = await _notificationGate.EvaluateAsync(userConfig.TeamsUserId, cancellationToken);

				if (!gateResult.CanSend)
				{
					summary.NotificationsBlocked++;
					_logger.LogInformation("Notification blocked for user {Email}: {Reason}",
						profile.AzureDevOpsEmail, gateResult.BlockedReason);
					return;
				}
			}
			else
			{
				_logger.LogInformation("Bypassing notification gates for user {Email} (BypassGates=true)",
					profile.AzureDevOpsEmail);
			}

			// Compose message (manager vs IC)
			var message = profile.IsManager
				? _messageComposer.ComposeManagerDigestMessage(violationsByOwner, profile.AzureDevOpsEmail)
				: _messageComposer.ComposeDigestMessage(violationsByOwner[profile.AzureDevOpsEmail]);

			// Send notification
			var sendResult = await _proactiveMessenger.SendMessageByTeamsUserIdAsync(
				userConfig.TeamsUserId,
				message,
				cancellationToken);

			if (!sendResult.Success)
			{
				summary.Errors++;
				_logger.LogError("Failed to send notification to user {Email}: {Error}",
					profile.AzureDevOpsEmail, sendResult.ErrorMessage);
				return;
			}

			summary.NotificationsSent++;
			_logger.LogInformation("Sent SLA notification to user {Email} ({Type}): {Count} total violations",
				profile.AzureDevOpsEmail,
				profile.IsManager ? "Manager" : "IC",
				totalViolations);

			// Record notification in NotificationGate (unless bypassed)
			if (!_configuration.BypassGates)
			{
				var deduplicationKey = $"WorkItemUpdateSla_{userConfig.TeamsUserId}_{DateTime.UtcNow:yyyyMMdd}";
				await _notificationGate.RecordNotificationAsync(
					userConfig.TeamsUserId,
					"WorkItemUpdateSla",
					message,
					deduplicationKey,
					cancellationToken);
			}
		}

		/// <summary>
		/// Calculates SLA violations from Azure DevOps work items JSON.
		/// </summary>
		private List<WorkItemUpdateSlaViolation> _CalculateViolations(string workItemsJson)
		{
			var violations = new List<WorkItemUpdateSlaViolation>();

			try
			{
				using var doc = JsonDocument.Parse(workItemsJson);
				var root = doc.RootElement;

				// Azure DevOps returns { "count": N, "value": [...] }
				if (!root.TryGetProperty("value", out var workItemsArray))
				{
					return violations;
				}

				foreach (var workItem in workItemsArray.EnumerateArray())
				{
					var violation = _CheckWorkItemForViolation(workItem);
					if (violation != null)
					{
						violations.Add(violation);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error parsing work items JSON");
			}

			return violations;
		}

		/// <summary>
		/// Checks a single work item for SLA violation.
		/// </summary>
		private WorkItemUpdateSlaViolation? _CheckWorkItemForViolation(JsonElement workItem)
		{
			try
			{
				// Extract fields
				if (!workItem.TryGetProperty("fields", out var fields))
					return null;

				if (!fields.TryGetProperty("System.Id", out var idElement) ||
					!idElement.TryGetInt32(out var workItemId))
					return null;

				if (!fields.TryGetProperty("System.WorkItemType", out var typeElement))
					return null;

				var workItemType = typeElement.GetString() ?? string.Empty;

				if (!fields.TryGetProperty("System.Title", out var titleElement))
					return null;

				var title = titleElement.GetString() ?? string.Empty;

				if (!fields.TryGetProperty("System.ChangedDate", out var changedDateElement))
					return null;

				if (!changedDateElement.TryGetDateTime(out var changedDate))
					return null;

				// Check if this work item type has an SLA rule
				if (!_configuration.SlaRules.TryGetValue(workItemType, out var slaThresholdDays))
				{
					// No SLA rule for this work item type
					return null;
				}

				// Calculate days since last update
				var daysSinceUpdate = (int)(DateTime.UtcNow - changedDate.ToUniversalTime()).TotalDays;

				// Check if SLA is violated
				if (daysSinceUpdate <= slaThresholdDays)
				{
					// SLA not violated
					return null;
				}

				// SLA violated - create violation object
				var url = $"{_configuration.AzureDevOpsBaseUrl}/{workItemId}";

				return new WorkItemUpdateSlaViolation
				{
					WorkItemId = workItemId,
					Title = title,
					WorkItemType = workItemType,
					DaysSinceUpdate = daysSinceUpdate,
					SlaThresholdDays = slaThresholdDays,
					Url = url
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking work item for violation");
				return null;
			}
		}
	}
}
