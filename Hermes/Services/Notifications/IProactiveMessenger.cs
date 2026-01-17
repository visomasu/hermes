using Hermes.Services.Notifications.Models;

namespace Hermes.Services.Notifications
{
	/// <summary>
	/// Service for sending proactive messages to Teams users.
	/// </summary>
	public interface IProactiveMessenger
	{
		/// <summary>
		/// Sends a proactive message to a Teams user by Teams user ID.
		/// </summary>
		/// <param name="teamsUserId">The Teams user ID.</param>
		/// <param name="message">The message to send.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Result indicating if message was sent, or an error.</returns>
		Task<ProactiveMessageResult> SendMessageByTeamsUserIdAsync(
			string teamsUserId,
			string message,
			CancellationToken cancellationToken = default);
	}
}
