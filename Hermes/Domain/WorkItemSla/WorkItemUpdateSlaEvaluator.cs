using Hermes.Domain.WorkItemSla.Models;
using Hermes.Notifications.Infra;
using Hermes.Notifications.WorkItemSla;
using Hermes.Notifications.WorkItemSla.Models;
using Hermes.Storage.Repositories.ConversationReference;
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
		private readonly IConversationReferenceRepository _conversationRefRepo;
		private readonly IAzureDevOpsWorkItemClient _azureDevOpsClient;
		private readonly INotificationGate _notificationGate;
		private readonly IProactiveMessenger _proactiveMessenger;
		private readonly WorkItemUpdateSlaConfiguration _configuration;
		private readonly WorkItemUpdateSlaMessageComposer _messageComposer;
		private readonly ILogger<WorkItemUpdateSlaEvaluator> _logger;

		public WorkItemUpdateSlaEvaluator(
			IConversationReferenceRepository conversationRefRepo,
			IAzureDevOpsWorkItemClient azureDevOpsClient,
			INotificationGate notificationGate,
			IProactiveMessenger proactiveMessenger,
			WorkItemUpdateSlaConfiguration configuration,
			WorkItemUpdateSlaMessageComposer messageComposer,
			ILogger<WorkItemUpdateSlaEvaluator> logger)
		{
			_conversationRefRepo = conversationRefRepo;
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

				// Get all active conversation references
				var conversationRefs = await _conversationRefRepo.GetActiveReferencesAsync(cancellationToken);

				if (conversationRefs == null || conversationRefs.Count == 0)
				{
					_logger.LogInformation("No active conversation references found");
					return summary;
				}

				_logger.LogInformation("Processing {Count} active users for SLA violations", conversationRefs.Count);

				// Process each user
				foreach (var conversationRefDoc in conversationRefs)
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
						await _ProcessUserAsync(conversationRefDoc, summary, cancellationToken);
					}
					catch (Exception ex)
					{
						summary.Errors++;
						_logger.LogError(ex, "Error processing user {TeamsUserId}", conversationRefDoc.TeamsUserId);
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
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(email))
			{
				_logger.LogWarning("CheckViolationsForEmailAsync called with empty email");
				return new List<WorkItemUpdateSlaViolation>();
			}

			_logger.LogDebug("Checking SLA violations for email {Email}", email);

			// Get work item types we care about from SLA rules
			var workItemTypes = _configuration.SlaRules.Keys.ToList();

			// Query Azure DevOps for assigned work items
			var workItemsJson = await _azureDevOpsClient.GetWorkItemsByAssignedUserAsync(
				email,
				new[] { "Active", "New" },
				new[] { "System.Id", "System.Title", "System.WorkItemType", "System.ChangedDate" },
				_configuration.IterationPath,
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
			ConversationReferenceDocument conversationRefDoc,
			SlaNotificationRunSummary summary,
			CancellationToken cancellationToken)
		{
			// Extract email from conversation reference
			var email = _ExtractEmailFromConversationReference(conversationRefDoc.ConversationReferenceJson);

			if (string.IsNullOrWhiteSpace(email))
			{
				_logger.LogWarning("Could not extract email from conversation reference for Teams user {TeamsUserId}", conversationRefDoc.TeamsUserId);
				return;
			}

			_logger.LogDebug("Processing user {Email} (Teams user {TeamsUserId})", email, conversationRefDoc.TeamsUserId);

			// Use shared method to check violations
			var violations = await CheckViolationsForEmailAsync(email, cancellationToken);

			if (violations.Count == 0)
			{
				_logger.LogDebug("No SLA violations found for user {Email}", email);
				return;
			}

			summary.ViolationsDetected += violations.Count;
			_logger.LogInformation("Found {Count} SLA violations for user {Email}", violations.Count, email);

			// TODO: Phase 3 - Check deduplication with SlaViolationHistoryRepository
			// For now, we rely on NotificationGate's general deduplication

			// Evaluate NotificationGate (unless bypassed for testing)
			if (!_configuration.BypassGates)
			{
				var gateResult = await _notificationGate.EvaluateAsync(conversationRefDoc.TeamsUserId, cancellationToken);

				if (!gateResult.CanSend)
				{
					summary.NotificationsBlocked++;
					_logger.LogInformation("Notification blocked for user {Email}: {Reason}", email, gateResult.BlockedReason);
					return;
				}
			}
			else
			{
				_logger.LogInformation("Bypassing notification gates for user {Email} (BypassGates=true)", email);
			}

			// Compose digest message
			var message = _messageComposer.ComposeDigestMessage(violations);

			// Send notification
			var sendResult = await _proactiveMessenger.SendMessageByTeamsUserIdAsync(
				conversationRefDoc.TeamsUserId,
				message,
				cancellationToken);

			if (!sendResult.Success)
			{
				summary.Errors++;
				_logger.LogError("Failed to send notification to user {Email}: {Error}", email, sendResult.ErrorMessage);
				return;
			}

			summary.NotificationsSent++;
			_logger.LogInformation("Sent SLA digest notification to user {Email} ({Count} violations)", email, violations.Count);

			// Record notification in NotificationGate (unless bypassed)
			if (!_configuration.BypassGates)
			{
				var deduplicationKey = $"WorkItemUpdateSla_{conversationRefDoc.TeamsUserId}_{DateTime.UtcNow:yyyyMMdd}";
				await _notificationGate.RecordNotificationAsync(
					conversationRefDoc.TeamsUserId,
					"WorkItemUpdateSla",
					message,
					deduplicationKey,
					cancellationToken);
			}

			// TODO: Phase 3 - Record individual violations in SlaViolationHistoryRepository
		}

		/// <summary>
		/// Extracts email address from conversation reference JSON.
		/// TODO: Implement proper email extraction from conversation reference.
		/// For now, this is a placeholder that needs to be completed when the
		/// ConversationReference type structure is better understood.
		/// </summary>
		private string? _ExtractEmailFromConversationReference(string conversationRefJson)
		{
			try
			{
				// TODO: Parse the conversation reference JSON to extract the user's email
				// The structure may vary depending on the Teams SDK version
				// For now, we'll parse it as a generic JSON document and look for email fields
				using var doc = JsonDocument.Parse(conversationRefJson);
				var root = doc.RootElement;

				// Try common paths where email might be stored
				// Path 1: from.properties.email
				if (root.TryGetProperty("from", out var from) &&
					from.TryGetProperty("properties", out var properties) &&
					properties.TryGetProperty("email", out var email))
				{
					var emailStr = email.GetString();
					if (!string.IsNullOrWhiteSpace(emailStr))
						return emailStr;
				}

				// Path 2: from.name (if it contains @)
				if (root.TryGetProperty("from", out from) &&
					from.TryGetProperty("name", out var name))
				{
					var nameStr = name.GetString();
					if (!string.IsNullOrWhiteSpace(nameStr) && nameStr.Contains("@"))
						return nameStr;
				}

				// Path 3: user.email
				if (root.TryGetProperty("user", out var user) &&
					user.TryGetProperty("email", out email))
				{
					var emailStr = email.GetString();
					if (!string.IsNullOrWhiteSpace(emailStr))
						return emailStr;
				}

				return null;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error extracting email from conversation reference JSON");
				return null;
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
