namespace Hermes.Controllers.Models.ProactiveMessaging
{
	/// <summary>
	/// Request model for sending a proactive message by Teams user ID.
	/// </summary>
	public class SendProactiveMessageByTeamsIdRequest
	{
		/// <summary>
		/// The Teams user ID.
		/// </summary>
		public string TeamsUserId { get; set; } = string.Empty;

		/// <summary>
		/// The message to send.
		/// </summary>
		public string Message { get; set; } = string.Empty;
	}
}
