using Hermes.Common;
using Hermes.Domain.WorkItemSla.Models;
using Hermes.Notifications.Infra;
using Hermes.Notifications.WorkItemSla;
using Hermes.Notifications.WorkItemSla.Models;
using Hermes.Storage.Repositories.TeamConfiguration;
using Hermes.Storage.Repositories.UserConfiguration;
using Integrations.AzureDevOps;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Hermes.Domain.WorkItemSla
{
	/// <summary>
	/// Evaluates work items against update frequency SLA thresholds and sends notifications.
	/// Supports multi-team subscriptions with per-team iteration caching and SLA rule overrides.
	/// </summary>
	public class WorkItemUpdateSlaEvaluator : IWorkItemUpdateSlaEvaluator
	{
		private readonly IUserConfigurationRepository _userConfigRepo;
		private readonly ITeamConfigurationRepository _teamConfigRepo;
		private readonly IAzureDevOpsWorkItemClient _azureDevOpsClient;
		private readonly INotificationGate _notificationGate;
		private readonly IProactiveMessenger _proactiveMessenger;
		private readonly WorkItemUpdateSlaConfiguration _configuration;
		private readonly WorkItemUpdateSlaMessageComposer _messageComposer;
		private readonly ILogger<WorkItemUpdateSlaEvaluator> _logger;

		// Per-team iteration path cache (reused across all checks in a single evaluation run)
		private Dictionary<string, AsyncLazy<string?>> _iterationCacheByTeam = new();

		public WorkItemUpdateSlaEvaluator(
			ILogger<WorkItemUpdateSlaEvaluator> logger,
			IUserConfigurationRepository userConfigRepo,
			ITeamConfigurationRepository teamConfigRepo,
			IAzureDevOpsWorkItemClient azureDevOpsClient,
			INotificationGate notificationGate,
			IProactiveMessenger proactiveMessenger,
			WorkItemUpdateSlaConfiguration configuration,
			WorkItemUpdateSlaMessageComposer messageComposer)
		{
			_logger = logger;
			_userConfigRepo = userConfigRepo;
			_teamConfigRepo = teamConfigRepo;
			_azureDevOpsClient = azureDevOpsClient;
			_notificationGate = notificationGate;
			_proactiveMessenger = proactiveMessenger;
			_configuration = configuration;
			_messageComposer = messageComposer;
		}

		/// <inheritdoc/>
		public async Task<SlaNotificationRunSummary> EvaluateAndNotifyAsync(
			CancellationToken cancellationToken = default)
		{
			var stopwatch = Stopwatch.StartNew();
			var summary = new SlaNotificationRunSummary();

			// Initialize iteration path cache for this evaluation run
			_iterationCacheByTeam = new Dictionary<string, AsyncLazy<string?>>();

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

				// Process users in parallel batches
				var batchSize = _configuration.UserProcessingBatchSize;
				for (int i = 0; i < registeredUsers.Count; i += batchSize)
				{
					// Check if we've hit the max notifications limit before starting next batch
					if (summary.NotificationsSent >= _configuration.MaxNotificationsPerRun)
					{
						_logger.LogWarning("Reached max notifications per run ({Max}), stopping evaluation", _configuration.MaxNotificationsPerRun);
						break;
					}

					var batch = registeredUsers.Skip(i).Take(batchSize).ToList();
					var batchTasks = batch.Select(async userConfig =>
					{
						try
						{
							await _ProcessUserAsync(userConfig, summary, cancellationToken);
							return (Success: true, Error: (Exception?)null);
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "Error processing user {TeamsUserId}", userConfig.TeamsUserId);
							return (Success: false, Error: ex);
						}
					}).ToArray();

					var results = await Task.WhenAll(batchTasks);

					// Update summary with batch results (sequential, thread-safe)
					foreach (var result in results)
					{
						summary.UsersProcessed++;
						if (!result.Success)
						{
							summary.Errors++;
						}
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
		[Obsolete("Use CheckViolationsForTeamAsync instead. This method is maintained for backwards compatibility.")]
		public async Task<List<WorkItemUpdateSlaViolation>> CheckViolationsForEmailAsync(
			string email,
			IEnumerable<string>? areaPaths = null,
			CancellationToken cancellationToken = default)
		{
			// Backwards compatibility: use global configuration
			if (string.IsNullOrWhiteSpace(email))
			{
				_logger.LogWarning("CheckViolationsForEmailAsync called with empty email");
				return new List<WorkItemUpdateSlaViolation>();
			}

			_logger.LogDebug("CheckViolationsForEmailAsync (legacy) called for {Email}", email);

			// Use global SLA rules from configuration
#pragma warning disable CS0618 // Type or member is obsolete
			var workItemTypes = _configuration.SlaRules.Keys.ToList();
			var configuredIterationPath = _configuration.IterationPath;
			var teamName = _configuration.TeamName;
#pragma warning restore CS0618 // Type or member is obsolete

			// Try to get dynamic iteration path (maintains backwards compatibility with old dynamic iteration logic)
			string? iterationPath = configuredIterationPath;
			try
			{
				var dynamicIteration = await _azureDevOpsClient.GetCurrentIterationPathAsync(
					teamName,
					cancellationToken);

				if (!string.IsNullOrWhiteSpace(dynamicIteration))
				{
					_logger.LogDebug("Using dynamic iteration: {IterationPath}", dynamicIteration);
					iterationPath = dynamicIteration;
				}
				else
				{
					_logger.LogDebug("No current iteration found, using configured: {IterationPath}", configuredIterationPath);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to get current iteration, using configured: {IterationPath}", configuredIterationPath);
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

			// Parse work items and calculate violations using global rules
#pragma warning disable CS0618 // Type or member is obsolete
			var violations = _CalculateViolations(workItemsJson, _configuration.SlaRules, string.Empty, string.Empty);
#pragma warning restore CS0618 // Type or member is obsolete

			_logger.LogDebug("Found {Count} SLA violations for email {Email}", violations.Count, email);

			return violations;
		}

		/// <inheritdoc/>
		public async Task<List<WorkItemUpdateSlaViolation>> CheckViolationsForTeamsAsync(
			string email,
			IEnumerable<string> teamIds,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(email))
			{
				_logger.LogWarning("CheckViolationsForTeamsAsync called with empty email");
				return new List<WorkItemUpdateSlaViolation>();
			}

			if (teamIds == null || !teamIds.Any())
			{
				_logger.LogWarning("CheckViolationsForTeamsAsync called with no team IDs for email {Email}", email);
				return new List<WorkItemUpdateSlaViolation>();
			}

			_logger.LogDebug("CheckViolationsForTeamsAsync called for {Email} with {TeamCount} teams",
				email, teamIds.Count());

			// Initialize iteration path cache for this call if not already initialized
			if (_iterationCacheByTeam == null)
			{
				_iterationCacheByTeam = new Dictionary<string, AsyncLazy<string?>>();
			}

			// Fetch all teams from repository
			var allTeams = await _teamConfigRepo.GetAllTeamsAsync(cancellationToken);
			if (allTeams == null || allTeams.Count == 0)
			{
				_logger.LogWarning("No teams found in configuration for CheckViolationsForTeamsAsync");
				return new List<WorkItemUpdateSlaViolation>();
			}

			// Filter to only subscribed teams
			var subscribedTeams = allTeams.Where(t => teamIds.Contains(t.TeamId)).ToList();
			if (subscribedTeams.Count == 0)
			{
				_logger.LogWarning("None of the provided team IDs found in configuration for email {Email}", email);
				return new List<WorkItemUpdateSlaViolation>();
			}

			_logger.LogDebug("Found {SubscribedCount} subscribed teams out of {TotalCount} for email {Email}",
				subscribedTeams.Count, allTeams.Count, email);

			// Check violations for each subscribed team
			var allViolations = new List<WorkItemUpdateSlaViolation>();

			foreach (var team in subscribedTeams)
			{
				// Merge global defaults with team-specific overrides
				var mergedSlaRules = _MergeSlaRules(_configuration.GlobalSlaDefaults, team.SlaOverrides);

				_logger.LogDebug("Checking team {TeamId} with {OverrideCount} SLA overrides, {TotalCount} total rules",
					team.TeamId, team.SlaOverrides.Count, mergedSlaRules.Count);

				// Check violations for this team
				var teamViolations = await _CheckViolationsForTeamAsync(
					email,
					team,
					mergedSlaRules,
					cancellationToken);

				allViolations.AddRange(teamViolations);
			}

			_logger.LogDebug("CheckViolationsForTeamsAsync found {Count} total violations across {TeamCount} teams for email {Email}",
				allViolations.Count, subscribedTeams.Count, email);

			return allViolations;
		}

		/// <summary>
		/// Checks SLA violations for a specific email and team.
		/// </summary>
		private async Task<List<WorkItemUpdateSlaViolation>> _CheckViolationsForTeamAsync(
			string email,
			TeamConfigurationDocument team,
			Dictionary<string, int> mergedSlaRules,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(email))
			{
				_logger.LogWarning("_CheckViolationsForTeamAsync called with empty email");
				return new List<WorkItemUpdateSlaViolation>();
			}

			_logger.LogDebug("Checking SLA violations for email {Email}, team {TeamId}", email, team.TeamId);

			// Get work item types from merged SLA rules
			var workItemTypes = mergedSlaRules.Keys.ToList();

			// Get iteration path for this team (use cache to avoid redundant API calls)
			var iterationPath = await _GetOrCreateIterationCacheAsync(team, cancellationToken);

			// Query Azure DevOps for assigned work items in this team's area paths
			var workItemsJson = await _azureDevOpsClient.GetWorkItemsByAssignedUserAsync(
				email,
				new[] { "Active", "New" },
				new[] { "System.Id", "System.Title", "System.WorkItemType", "System.ChangedDate" },
				iterationPath,
				team.AreaPaths,
				workItemTypes,
				cancellationToken);

			if (string.IsNullOrWhiteSpace(workItemsJson))
			{
				_logger.LogDebug("No work items found for email {Email} in team {TeamId}", email, team.TeamId);
				return new List<WorkItemUpdateSlaViolation>();
			}

			// Parse work items and calculate violations with team-specific rules
			var violations = _CalculateViolations(workItemsJson, mergedSlaRules, team.TeamId, team.TeamName);

			_logger.LogDebug("Found {Count} SLA violations for email {Email} in team {TeamId}",
				violations.Count, email, team.TeamId);

			return violations;
		}

		/// <summary>
		/// Gets or creates the iteration path cache for a specific team.
		/// </summary>
		private async Task<string?> _GetOrCreateIterationCacheAsync(
			TeamConfigurationDocument team,
			CancellationToken cancellationToken)
		{
			if (!_iterationCacheByTeam.ContainsKey(team.TeamId))
			{
				_iterationCacheByTeam[team.TeamId] = new AsyncLazy<string?>(async () =>
				{
					try
					{
						var currentIteration = await _azureDevOpsClient.GetCurrentIterationPathAsync(
							team.TeamName,
							cancellationToken);

						if (!string.IsNullOrWhiteSpace(currentIteration))
						{
							_logger.LogDebug("Dynamically determined current iteration for team {TeamId}: {IterationPath}",
								team.TeamId, currentIteration);
							return currentIteration;
						}
						else
						{
							_logger.LogDebug("No current iteration found for team {TeamId}, using configured: {IterationPath}",
								team.TeamId, team.IterationPath);
							return team.IterationPath;
						}
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Failed to get current iteration for team {TeamId}, using configured: {IterationPath}",
							team.TeamId, team.IterationPath);
						return team.IterationPath;
					}
				});
			}

			return await _iterationCacheByTeam[team.TeamId].Value;
		}

		/// <summary>
		/// Merges global SLA defaults with team-specific overrides.
		/// Team overrides take precedence over global defaults.
		/// </summary>
		private Dictionary<string, int> _MergeSlaRules(
			Dictionary<string, int> globalDefaults,
			Dictionary<string, int> teamOverrides)
		{
			var merged = new Dictionary<string, int>(globalDefaults);

			foreach (var (workItemType, days) in teamOverrides)
			{
				merged[workItemType] = days; // Team override wins
			}

			return merged;
		}

		/// <summary>
		/// Processes a single user for SLA violations across all subscribed teams.
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

			// Check if user has subscribed teams
			if (profile.SubscribedTeamIds == null || profile.SubscribedTeamIds.Count == 0)
			{
				_logger.LogWarning("User {TeamsUserId} has no subscribed teams, skipping", userConfig.TeamsUserId);
				return;
			}

			_logger.LogDebug("Processing user {Email} (Teams user {TeamsUserId}, IsManager: {IsManager}, Teams: {Teams})",
				profile.AzureDevOpsEmail, userConfig.TeamsUserId, profile.IsManager,
				string.Join(", ", profile.SubscribedTeamIds));

			// Get all team configurations for subscribed teams
			var allTeams = await _teamConfigRepo.GetAllTeamsAsync(cancellationToken);
			var subscribedTeams = allTeams
				.Where(t => profile.SubscribedTeamIds.Contains(t.TeamId))
				.ToList();

			if (subscribedTeams.Count == 0)
			{
				_logger.LogWarning("User {TeamsUserId} subscribed to teams but none found in configuration",
					userConfig.TeamsUserId);
				return;
			}

			// Determine emails to check (user + directs if manager)
			var emailsToCheck = new List<string> { profile.AzureDevOpsEmail };
			if (profile.IsManager)
			{
				emailsToCheck.AddRange(profile.DirectReportEmails);
			}

			_logger.LogInformation("Checking SLA violations for {EmailCount} email(s) across {TeamCount} team(s) (user: {Email}, IsManager: {IsManager})",
				emailsToCheck.Count, subscribedTeams.Count, profile.AzureDevOpsEmail, profile.IsManager);

			// Check violations for all emails across all teams
			var allViolations = new List<WorkItemUpdateSlaViolation>();

			foreach (var team in subscribedTeams)
			{
				// Merge global defaults with team-specific overrides
				var mergedSlaRules = _MergeSlaRules(_configuration.GlobalSlaDefaults, team.SlaOverrides);

				_logger.LogDebug("Team {TeamId} has {OverrideCount} SLA overrides, {TotalCount} total rules",
					team.TeamId, team.SlaOverrides.Count, mergedSlaRules.Count);

				// Check violations for all emails in this team
				var teamViolationTasks = emailsToCheck.Select(async email =>
				{
					var violations = await _CheckViolationsForTeamAsync(email, team, mergedSlaRules, cancellationToken);
					return new { Email = email, Violations = violations };
				}).ToArray();

				var teamResults = await Task.WhenAll(teamViolationTasks);

				// Aggregate violations from this team
				foreach (var result in teamResults)
				{
					allViolations.AddRange(result.Violations);
				}
			}

			// Group violations by owner email
			var violationsByOwner = allViolations
				.GroupBy(v => v.Url.Contains("assignedto=") ?
					v.Url.Split("assignedto=")[1].Split('&')[0] :
					profile.AzureDevOpsEmail)
				.ToDictionary(
					g => g.Key,
					g => g.ToList());

			if (violationsByOwner.Count == 0)
			{
				_logger.LogDebug("No SLA violations found for user {Email} across {TeamCount} teams",
					profile.AzureDevOpsEmail, subscribedTeams.Count);
				return;
			}

			var totalViolations = violationsByOwner.Sum(kvp => kvp.Value.Count);
			summary.ViolationsDetected += totalViolations;
			_logger.LogInformation("Found {TotalCount} SLA violations for user {Email} ({OwnerCount} owners, {TeamCount} teams)",
				totalViolations, profile.AzureDevOpsEmail, violationsByOwner.Count, subscribedTeams.Count);

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

			// Compose message (manager vs IC) with team-aware formatting
			// Detect multi-team scenario
			var uniqueTeamCount = allViolations
				.Select(v => v.TeamId)
				.Where(id => !string.IsNullOrEmpty(id))
				.Distinct()
				.Count();

			// Choose appropriate composer method based on role and team count
			string message;
			if (profile.IsManager)
			{
				message = uniqueTeamCount > 1
					? _messageComposer.ComposeManagerDigestMessageWithTeams(violationsByOwner, profile.AzureDevOpsEmail)
					: _messageComposer.ComposeManagerDigestMessage(violationsByOwner, profile.AzureDevOpsEmail);
			}
			else
			{
				var userViolations = violationsByOwner[profile.AzureDevOpsEmail];
				message = uniqueTeamCount > 1
					? _messageComposer.ComposeDigestMessageWithTeams(userViolations)
					: _messageComposer.ComposeDigestMessage(userViolations);
			}

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
			_logger.LogInformation("Sent SLA notification to user {Email} ({Type}): {Count} total violations across {TeamCount} teams",
				profile.AzureDevOpsEmail,
				profile.IsManager ? "Manager" : "IC",
				totalViolations,
				subscribedTeams.Count);

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
		private List<WorkItemUpdateSlaViolation> _CalculateViolations(
			string workItemsJson,
			Dictionary<string, int> slaRules,
			string teamId,
			string teamName)
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
					var violation = _CheckWorkItemForViolation(workItem, slaRules, teamId, teamName);
					if (violation != null)
					{
						violations.Add(violation);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error parsing work items JSON for team {TeamId}", teamId);
			}

			return violations;
		}

		/// <summary>
		/// Checks a single work item for SLA violation.
		/// </summary>
		private WorkItemUpdateSlaViolation? _CheckWorkItemForViolation(
			JsonElement workItem,
			Dictionary<string, int> slaRules,
			string teamId,
			string teamName)
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
				if (!slaRules.TryGetValue(workItemType, out var slaThresholdDays))
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
					Url = url,
					TeamId = teamId,
					TeamName = teamName
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking work item for violation in team {TeamId}", teamId);
				return null;
			}
		}
	}
}
