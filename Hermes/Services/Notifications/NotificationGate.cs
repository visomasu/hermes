using Hermes.Services.Notifications.Models;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;
using Hermes.Storage.Repositories.UserNotificationState;
using Microsoft.Extensions.Logging;

namespace Hermes.Services.Notifications
{
	/// <summary>
	/// Implementation of notification gating logic.
	/// Enforces throttling and user preferences.
	/// </summary>
	public class NotificationGate : INotificationGate
	{
		private readonly IUserConfigurationRepository _userConfigRepo;
		private readonly IUserNotificationStateRepository _notificationStateRepo;
		private readonly ILogger<NotificationGate> _logger;

		public NotificationGate(
			IUserConfigurationRepository userConfigRepo,
			IUserNotificationStateRepository notificationStateRepo,
			ILogger<NotificationGate> logger)
		{
			_userConfigRepo = userConfigRepo;
			_notificationStateRepo = notificationStateRepo;
			_logger = logger;
		}

		/// <inheritdoc/>
		public async Task<GateResult> EvaluateAsync(
			string teamsUserId,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(teamsUserId))
			{
				return new GateResult
				{
					CanSend = false,
					BlockedReason = "Teams user ID is required"
				};
			}

			// Get user configuration (or use defaults if not found)
			var userConfig = await _userConfigRepo.GetByTeamsUserIdAsync(teamsUserId, cancellationToken);
			var prefs = userConfig?.Notifications ?? new NotificationPreferences();

			// Check quiet hours
			if (IsInQuietHours(prefs.QuietHours, DateTime.UtcNow, prefs.TimeZoneId))
			{
				_logger.LogDebug("Notification blocked for {TeamsUserId}: Quiet hours", teamsUserId);
				return new GateResult
				{
					CanSend = false,
					BlockedReason = "User is in quiet hours"
				};
			}

			// Get recent notifications from aggregate document (single read!)
			var recentNotifications = await _notificationStateRepo.GetNotificationsSinceAsync(
				teamsUserId,
				DateTime.UtcNow.AddDays(-1),
				cancellationToken);

			// Count notifications in time windows
			var lastHour = recentNotifications.Count(n => n.SentAt >= DateTime.UtcNow.AddHours(-1));
			var lastDay = recentNotifications.Count;

			_logger.LogDebug(
				"Notification gate evaluation for {TeamsUserId}: {LastHour} in last hour, {LastDay} in last day",
				teamsUserId,
				lastHour,
				lastDay);

			// Check hourly limit
			if (lastHour >= prefs.MaxNotificationsPerHour)
			{
				_logger.LogDebug(
					"Notification blocked for {TeamsUserId}: Hourly limit exceeded ({Count}/{Limit})",
					teamsUserId,
					lastHour,
					prefs.MaxNotificationsPerHour);

				return new GateResult
				{
					CanSend = false,
					BlockedReason = $"Hourly limit exceeded ({lastHour}/{prefs.MaxNotificationsPerHour})",
					NotificationsSentInLastHour = lastHour,
					NotificationsSentInLastDay = lastDay
				};
			}

			// Check daily limit
			if (lastDay >= prefs.MaxNotificationsPerDay)
			{
				_logger.LogDebug(
					"Notification blocked for {TeamsUserId}: Daily limit exceeded ({Count}/{Limit})",
					teamsUserId,
					lastDay,
					prefs.MaxNotificationsPerDay);

				return new GateResult
				{
					CanSend = false,
					BlockedReason = $"Daily limit exceeded ({lastDay}/{prefs.MaxNotificationsPerDay})",
					NotificationsSentInLastHour = lastHour,
					NotificationsSentInLastDay = lastDay
				};
			}

			// All checks passed
			return new GateResult
			{
				CanSend = true,
				NotificationsSentInLastHour = lastHour,
				NotificationsSentInLastDay = lastDay
			};
		}


		/// <inheritdoc/>
		public bool IsInQuietHours(QuietHours? quietHours, DateTime utcNow, string? timeZoneId = null)
		{
			if (quietHours == null || !quietHours.Enabled)
			{
				return false;
			}

			// Convert UTC to user's local timezone
			TimeZoneInfo userTimeZone;
			try
			{
				userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? "UTC");
			}
			catch (TimeZoneNotFoundException)
			{
				_logger.LogWarning("Invalid timezone ID '{TimeZoneId}', falling back to UTC", timeZoneId);
				userTimeZone = TimeZoneInfo.Utc;
			}
			catch (InvalidTimeZoneException)
			{
				_logger.LogWarning("Invalid timezone ID '{TimeZoneId}', falling back to UTC", timeZoneId);
				userTimeZone = TimeZoneInfo.Utc;
			}

			var userLocalTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, userTimeZone);
			var currentTime = TimeOnly.FromDateTime(userLocalTime);

			// Handle overnight quiet hours (e.g., 10 PM - 8 AM)
			if (quietHours.StartTime > quietHours.EndTime)
			{
				return currentTime >= quietHours.StartTime || currentTime < quietHours.EndTime;
			}

			// Handle same-day quiet hours (e.g., 1 PM - 5 PM)
			return currentTime >= quietHours.StartTime && currentTime < quietHours.EndTime;
		}

		/// <inheritdoc/>
		public async Task RecordNotificationAsync(
			string teamsUserId,
			string notificationType,
			string content,
			string deduplicationKey,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(teamsUserId))
			{
				_logger.LogWarning("Cannot record notification: Teams user ID is required");
				return;
			}


			await _notificationStateRepo.RecordNotificationAsync(
			teamsUserId,
			notificationType,
			deduplicationKey,
			cancellationToken: cancellationToken);

			_logger.LogInformation(
				"Recorded generic notification for {TeamsUserId}: Type={Type}",
				teamsUserId,
				notificationType);
		}

		/// <inheritdoc/>
		public async Task RecordWorkItemNotificationAsync(
			string teamsUserId,
			string notificationType,
			string content,
			string deduplicationKey,
			int workItemId,
			string? areaPath = null,
			string? workItemType = null,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(teamsUserId))
			{
				_logger.LogWarning("Cannot record notification: Teams user ID is required");
				return;
			}


			await _notificationStateRepo.RecordNotificationAsync(
			teamsUserId,
			notificationType,
			deduplicationKey,
			workItemId,
			areaPath,
			cancellationToken);

			_logger.LogInformation(
				"Recorded work item notification for {TeamsUserId}: Type={Type}, WorkItemId={WorkItemId}",
				teamsUserId,
				notificationType,
				workItemId);
		}
	}
}
