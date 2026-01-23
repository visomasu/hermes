using Hermes.Controllers.Models.ProactiveMessaging;
using Hermes.Notifications.Infra;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Controllers
{
	/// <summary>
	/// Controller for testing proactive messaging functionality via Teams user ID.
	/// </summary>
	[ApiController]
	[Route("api/proactive")]
	public class ProactiveMessagingController : ControllerBase
	{
		private readonly IProactiveMessenger _messenger;
		private readonly ILogger<ProactiveMessagingController> _logger;

		public ProactiveMessagingController(
			IProactiveMessenger messenger,
			ILogger<ProactiveMessagingController> logger)
		{
			_messenger = messenger;
			_logger = logger;
		}

		/// <summary>
		/// Test endpoint for sending proactive messages by Teams user ID.
		/// POST /api/proactive/send-by-teams-id
		/// Body: { "teamsUserId": "29:...", "message": "Test message" }
		/// </summary>
		/// <param name="request">The request containing Teams user ID and message.</param>
		/// <returns>Result of the proactive message send operation.</returns>
		[HttpPost("send-by-teams-id")]
		public async Task<IActionResult> SendProactiveMessageByTeamsId(
			[FromBody] SendProactiveMessageByTeamsIdRequest request)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(request.TeamsUserId))
				{
					return BadRequest(new
					{
						success = false,
						error = "Teams user ID is required"
					});
				}

				if (string.IsNullOrWhiteSpace(request.Message))
				{
					return BadRequest(new
					{
						success = false,
						error = "Message is required"
					});
				}

				var result = await _messenger.SendMessageByTeamsUserIdAsync(
					request.TeamsUserId,
					request.Message);

				if (result.Success)
				{
					return Ok(new
					{
						success = true,
						sentAt = result.SentAt,
						message = "Proactive message sent successfully"
					});
				}
				else
				{
					return BadRequest(new
					{
						success = false,
						error = result.ErrorMessage
					});
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error sending proactive message");
				return StatusCode(500, new
				{
					success = false,
					error = ex.Message
				});
			}
		}
	}
}
