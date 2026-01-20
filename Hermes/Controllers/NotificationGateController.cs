using Hermes.Controllers.Models.NotificationGate;
using Hermes.Services.Notifications;
using Hermes.Storage.Repositories.UserNotificationState;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Hermes.Controllers
{
	/// <summary>
	/// Controller for testing notification gate functionality.
	/// </summary>
	[ApiController]
	[Route("api/notification-gate")]
	public class NotificationGateController : ControllerBase
	{
		private readonly INotificationGate _notificationGate;
		private readonly IProactiveMessenger _proactiveMessenger;
		private readonly IUserNotificationStateRepository _notificationStateRepo;
		private readonly ILogger<NotificationGateController> _logger;

		public NotificationGateController(
			INotificationGate notificationGate,
			IProactiveMessenger proactiveMessenger,
			IUserNotificationStateRepository notificationStateRepo,
			ILogger<NotificationGateController> logger)
		{
			_notificationGate = notificationGate;
			_proactiveMessenger = proactiveMessenger;
			_notificationStateRepo = notificationStateRepo;
			_logger = logger;
		}

		/// <summary>
		/// Evaluates if a notification can be sent to a user.
		/// POST /api/notification-gate/evaluate
		/// Body: { "teamsUserId": "29:..." }
		/// </summary>
		[HttpPost("evaluate")]
		public async Task<IActionResult> EvaluateGate([FromBody] EvaluateGateRequest request)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(request.TeamsUserId))
				{
					return BadRequest(new { error = "Teams user ID is required" });
				}

				var result = await _notificationGate.EvaluateAsync(request.TeamsUserId);

				return Ok(new
				{
					canSend = result.CanSend,
					blockedReason = result.BlockedReason,
					notificationsSentInLastHour = result.NotificationsSentInLastHour,
					notificationsSentInLastDay = result.NotificationsSentInLastDay
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error evaluating notification gate");
				return StatusCode(500, new { error = ex.Message });
			}
		}


		/// <summary>
		/// Sends a notification through the gate (full flow: evaluate, check duplicate, send, record).
		/// POST /api/notification-gate/send
		/// Body: { "teamsUserId": "29:...", "message": "Test", "notificationType": "SlaViolation", "context": { "workItemId": 12345 } }
		/// </summary>
		[HttpPost("send")]
		public async Task<IActionResult> SendGatedNotification([FromBody] SendGatedNotificationRequest request)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(request.TeamsUserId))
				{
					return BadRequest(new { error = "Teams user ID is required" });
				}

				if (string.IsNullOrWhiteSpace(request.Message))
				{
					return BadRequest(new { error = "Message is required" });
				}

				// Step 1: Evaluate gate
				var gateResult = await _notificationGate.EvaluateAsync(request.TeamsUserId);

				if (!gateResult.CanSend)
				{
					return Ok(new
					{
						sent = false,
						reason = "Blocked by gate",
						blockedReason = gateResult.BlockedReason,
						notificationsSentInLastHour = gateResult.NotificationsSentInLastHour,
						notificationsSentInLastDay = gateResult.NotificationsSentInLastDay
					});
				}

				// Step 2: Generate deduplication key for tracking
				string deduplicationKey = request.DeduplicationKey
					?? $"{request.NotificationType}_{Guid.NewGuid()}";


				// Step 3: Send notification
				var sendResult = await _proactiveMessenger.SendMessageByTeamsUserIdAsync(
					request.TeamsUserId,
					request.Message);

				if (!sendResult.Success)
				{
					return Ok(new
					{
						sent = false,
						reason = "Failed to send",
						error = sendResult.ErrorMessage
					});
				}

				// Step 4: Record notification
				// Parse context to determine if it's a work item notification
				if (!string.IsNullOrWhiteSpace(request.Context))
				{
					try
					{
						var contextDoc = JsonDocument.Parse(request.Context);
						if (contextDoc.RootElement.TryGetProperty("workItemId", out var workItemIdProp) &&
						    workItemIdProp.TryGetInt32(out var workItemId))
						{
							// This is a work item notification
							string? areaPath = null;
							string? workItemType = null;

							if (contextDoc.RootElement.TryGetProperty("areaPath", out var areaPathProp))
								areaPath = areaPathProp.GetString();

							if (contextDoc.RootElement.TryGetProperty("workItemType", out var workItemTypeProp))
								workItemType = workItemTypeProp.GetString();

							await _notificationGate.RecordWorkItemNotificationAsync(
								request.TeamsUserId,
								request.NotificationType ?? "Test",
								request.Message,
								deduplicationKey,
								workItemId,
								areaPath,
								workItemType);
						}
						else
						{
							// Generic notification with context
							await _notificationGate.RecordNotificationAsync(
								request.TeamsUserId,
								request.NotificationType ?? "Test",
								request.Message,
								deduplicationKey);
						}
					}
					catch
					{
						// Invalid JSON, record as generic
						await _notificationGate.RecordNotificationAsync(
							request.TeamsUserId,
							request.NotificationType ?? "Test",
							request.Message,
							deduplicationKey);
					}
				}
				else
				{
					// No context - generic notification
					await _notificationGate.RecordNotificationAsync(
						request.TeamsUserId,
						request.NotificationType ?? "Test",
						request.Message,
						deduplicationKey);
				}

				return Ok(new
				{
					sent = true,
					sentAt = sendResult.SentAt,
					deduplicationKey,
					notificationType = request.NotificationType ?? "Test",
					context = request.Context
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error sending gated notification");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// Gets notification history for a user.
		/// GET /api/notification-gate/history/{teamsUserId}?hours=24
		/// </summary>
		[HttpGet("history/{teamsUserId}")]
		public async Task<IActionResult> GetNotificationHistory(
			string teamsUserId,
			[FromQuery] int hours = 24)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(teamsUserId))
				{
					return BadRequest(new { error = "Teams user ID is required" });
				}

				var since = DateTime.UtcNow.AddHours(-hours);


				// Query notification events from aggregate document
				var notifications = await _notificationStateRepo.GetNotificationsSinceAsync(
					teamsUserId,
					since);

				return Ok(new
				{
					teamsUserId,
					hoursBack = hours,
					count = notifications.Count,
					notifications = notifications.Select(n => new
					{
						n.NotificationType,
						n.DeduplicationKey,
						workItemId = n.WorkItemId,
						areaPath = n.AreaPath,
						n.SentAt
					})
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving notification history");
				return StatusCode(500, new { error = ex.Message });
			}
		}
	}
}
