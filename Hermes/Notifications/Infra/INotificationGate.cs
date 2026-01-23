using Hermes.Notifications.Infra.Models;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;

namespace Hermes.Notifications.Infra
{
	/// <summary>
	/// Gates notification delivery based on throttling, deduplication, and user preferences.
	/// </summary>
	public interface INotificationGate
	{
		/// <summary>
		/// Evaluates whether a notification can be sent to a user.
		/// Checks throttling limits, quiet hours, and user preferences.
		/// </summary>
		/// <param name="teamsUserId">The Teams user ID.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Result indicating whether notification can be sent and why if blocked.</returns>
		Task<GateResult> EvaluateAsync(
			string teamsUserId,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Checks if the current time falls within user's quiet hours.
		/// Converts UTC time to user's local timezone before comparison.
		/// </summary>
		/// <param name="quietHours">The user's quiet hours configuration (times in user's local timezone).</param>
		/// <param name="utcNow">Current UTC time.</param>
		/// <param name="timeZoneId">User's timezone ID (IANA format, e.g., "America/Los_Angeles"). Defaults to UTC if null.</param>
		/// <returns>True if currently within quiet hours.</returns>
		bool IsInQuietHours(QuietHours? quietHours, DateTime utcNow, string? timeZoneId = null);

		/// <summary>
		/// Records that a generic notification was sent.
		/// </summary>
		/// <param name="teamsUserId">The Teams user ID.</param>
		/// <param name="notificationType">Type of notification (e.g., "Generic").</param>
		/// <param name="content">Notification message content.</param>
		/// <param name="deduplicationKey">Unique key for deduplication.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		Task RecordNotificationAsync(
			string teamsUserId,
			string notificationType,
			string content,
			string deduplicationKey,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Records that a work item notification was sent.
		/// </summary>
		/// <param name="teamsUserId">The Teams user ID.</param>
		/// <param name="notificationType">Type of notification (e.g., "SlaViolation").</param>
		/// <param name="content">Notification message content.</param>
		/// <param name="deduplicationKey">Unique key for deduplication.</param>
		/// <param name="workItemId">The work item ID.</param>
		/// <param name="areaPath">Optional area path.</param>
		/// <param name="workItemType">Optional work item type.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		Task RecordWorkItemNotificationAsync(
			string teamsUserId,
			string notificationType,
			string content,
			string deduplicationKey,
			int workItemId,
			string? areaPath = null,
			string? workItemType = null,
			CancellationToken cancellationToken = default);
	}
}
