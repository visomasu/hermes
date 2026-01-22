namespace Hermes.Controllers.Models.NotificationGate
{
	/// <summary>
	/// Request model for sending a gated notification.
	/// </summary>
	public class SendGatedNotificationRequest
	{
		public string TeamsUserId { get; set; } = string.Empty;
		public string Message { get; set; } = string.Empty;
		public string? NotificationType { get; set; }
		public string? Context { get; set; }
		public string? DeduplicationKey { get; set; }
		public int? DeduplicationLookbackHours { get; set; }
	}
}
