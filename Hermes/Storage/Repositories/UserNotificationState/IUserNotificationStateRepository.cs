using Hermes.Storage.Core;

namespace Hermes.Storage.Repositories.UserNotificationState
{
	/// <summary>
	/// Repository for managing user notification state (aggregate per user).
	/// Tracks recent notifications for throttling and basic audit trail.
	/// </summary>
	public interface IUserNotificationStateRepository : IRepository<UserNotificationStateDocument>
	{
		/// <summary>
		/// Gets or creates the notification state document for a user.
		/// </summary>
		/// <param name="teamsUserId">The Teams user ID.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>The user's notification state document (never null).</returns>
		Task<UserNotificationStateDocument> GetOrCreateAsync(
			string teamsUserId,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Records a notification event and automatically cleans up old events.
		/// </summary>
		/// <param name="teamsUserId">The Teams user ID.</param>
		/// <param name="notificationType">Type of notification.</param>
		/// <param name="deduplicationKey">Optional deduplication key for auditing.</param>
		/// <param name="workItemId">Optional work item ID.</param>
		/// <param name="areaPath">Optional area path.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		Task RecordNotificationAsync(
			string teamsUserId,
			string notificationType,
			string? deduplicationKey = null,
			int? workItemId = null,
			string? areaPath = null,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Gets recent notification events for a user since a specific time.
		/// </summary>
		/// <param name="teamsUserId">The Teams user ID.</param>
		/// <param name="sinceUtc">Only return events after this time (UTC).</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>List of recent notification events.</returns>
		Task<List<NotificationEvent>> GetNotificationsSinceAsync(
			string teamsUserId,
			DateTime sinceUtc,
			CancellationToken cancellationToken = default);
	}
}
