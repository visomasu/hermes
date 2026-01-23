namespace Hermes.Notifications.Infra.Models
{
	/// <summary>
	/// Result of attempting to send a proactive notification.
	/// </summary>
	public class ProactiveMessageResult
	{
		/// <summary>
		/// Gets or sets a value indicating whether the message was successfully sent.
		/// </summary>
		public bool Success { get; set; }

		/// <summary>
		/// Gets or sets the error message if the send failed.
		/// </summary>
		public string? ErrorMessage { get; set; }

		/// <summary>
		/// Gets or sets the timestamp when the message was sent.
		/// </summary>
		public DateTime SentAt { get; set; }
	}
}
