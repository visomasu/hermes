using Hermes.Storage.Core;
using Hermes.Storage.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Hermes.Storage.Repositories.UserNotificationState
{
	/// <summary>
	/// Repository implementation for user notification state (aggregate per user).
	/// Maintains a single document per user with sliding window of recent notifications.
	/// </summary>
	public class UserNotificationStateRepository
		: RepositoryBase<UserNotificationStateDocument>,
		  IUserNotificationStateRepository
	{
		private readonly ILogger<UserNotificationStateRepository> _logger;

		/// <inheritdoc/>
		protected override string ObjectTypeCode => "user-notif-state";

		public UserNotificationStateRepository(
			IStorageClient<UserNotificationStateDocument, string> storage,
			ILogger<UserNotificationStateRepository> logger)
			: base(storage)
		{
			_logger = logger;
		}

		/// <inheritdoc/>
		public async Task<UserNotificationStateDocument> GetOrCreateAsync(
			string teamsUserId,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(teamsUserId))
			{
				throw new StorageException(
					"Teams user ID is required",
					StorageExceptionTypes.ErrorCode.InvalidInput);
			}

			// Use same value for Id and PartitionKey (single document per user)
			var existing = await ReadAsync(teamsUserId, teamsUserId);

			if (existing != null)
			{
				return existing;
			}

			// Create new document
			var newDoc = new UserNotificationStateDocument
			{
				Id = teamsUserId,
				PartitionKey = teamsUserId,
				TeamsUserId = teamsUserId,
				RecentNotifications = new List<NotificationEvent>(),
				LastUpdated = DateTime.UtcNow,
				TotalNotificationsSent = 0
			};

			await CreateAsync(newDoc);
			_logger.LogInformation("Created notification state document for user {TeamsUserId}", teamsUserId);

			return newDoc;
		}

	/// <inheritdoc/>
	public async Task RecordNotificationAsync(
		string teamsUserId,
		string notificationType,
		string? deduplicationKey = null,
		int? workItemId = null,
		string? areaPath = null,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(teamsUserId))
		{
			_logger.LogWarning("Cannot record notification: Teams user ID is required");
			return;
		}

		// Get or create document
		var doc = await GetOrCreateAsync(teamsUserId, cancellationToken);

		// Add new notification event
		doc.RecentNotifications.Add(new NotificationEvent
		{
			SentAt = DateTime.UtcNow,
			NotificationType = notificationType,
			DeduplicationKey = deduplicationKey,
			WorkItemId = workItemId,
			AreaPath = areaPath
		});

		// Clean up events older than 24 hours
		var cutoff = DateTime.UtcNow.AddDays(-1);
		var beforeCleanup = doc.RecentNotifications.Count;
		doc.RecentNotifications.RemoveAll(e => e.SentAt < cutoff);
		var afterCleanup = doc.RecentNotifications.Count;

		if (beforeCleanup != afterCleanup)
		{
			_logger.LogDebug(
				"Cleaned up {Count} old notification events for {TeamsUserId}",
				beforeCleanup - afterCleanup,
				teamsUserId);
		}

		// Update metadata
		doc.TotalNotificationsSent++;
		doc.LastUpdated = DateTime.UtcNow;

		// Save document (note: uses UpsertItemAsync, no optimistic concurrency for now)
		await UpdateAsync(teamsUserId, doc);

		_logger.LogInformation(
			"Recorded notification for {TeamsUserId}: Type={Type}, Total={Total}, Recent={Recent}",
			teamsUserId,
			notificationType,
			doc.TotalNotificationsSent,
			doc.RecentNotifications.Count);
	}

		/// <inheritdoc/>
		public async Task<List<NotificationEvent>> GetNotificationsSinceAsync(
			string teamsUserId,
			DateTime sinceUtc,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(teamsUserId))
			{
				return new List<NotificationEvent>();
			}

			var doc = await ReadAsync(teamsUserId, teamsUserId);

			if (doc == null || doc.RecentNotifications == null || doc.RecentNotifications.Count == 0)
			{
				return new List<NotificationEvent>();
			}

			// Filter events since the specified time
			return doc.RecentNotifications
				.Where(e => e.SentAt >= sinceUtc)
				.OrderByDescending(e => e.SentAt)
				.ToList();
		}
	}
}
