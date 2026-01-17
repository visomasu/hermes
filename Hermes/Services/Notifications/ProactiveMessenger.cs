using System.Security.Claims;
using System.Text.Json;
using Hermes.Services.Notifications.Models;
using Hermes.Storage.Repositories.ConversationReference;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hermes.Services.Notifications
{
	/// <summary>
	/// Sends proactive notifications via Teams using stored conversation references.
	/// </summary>
	public class ProactiveMessenger : IProactiveMessenger
	{
		private readonly IConversationReferenceRepository _conversationRefRepo;
		private readonly CloudAdapter _adapter;
		private readonly ILogger<ProactiveMessenger> _logger;
		private readonly ClaimsIdentity _botIdentity;

		public ProactiveMessenger(
			IConversationReferenceRepository conversationRefRepo,
			CloudAdapter adapter,
			IConfiguration configuration,
			ILogger<ProactiveMessenger> logger)
		{
			_conversationRefRepo = conversationRefRepo;
			_adapter = adapter;
			_logger = logger;

			// Create bot identity for proactive messaging (empty for local development)
			var botAppId = configuration["MicrosoftApp:AppId"] ?? "";
			_botIdentity = new ClaimsIdentity("Bot");
			_botIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, botAppId));
		}

		public async Task<ProactiveMessageResult> SendMessageByTeamsUserIdAsync(
			string teamsUserId,
			string message,
			CancellationToken cancellationToken = default)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(teamsUserId))
				{
					return new ProactiveMessageResult
					{
						Success = false,
						ErrorMessage = "Teams user ID cannot be null or empty"
					};
				}

				if (string.IsNullOrWhiteSpace(message))
				{
					return new ProactiveMessageResult
					{
						Success = false,
						ErrorMessage = "Message cannot be null or empty"
					};
				}

				// Retrieve conversation reference
				var conversationRef = await _conversationRefRepo.GetByTeamsUserIdAsync(teamsUserId, cancellationToken);

				if (conversationRef == null || !conversationRef.IsActive)
				{
					_logger.LogWarning("No active conversation reference found for Teams user {TeamsUserId}", teamsUserId);
					return new ProactiveMessageResult
					{
						Success = false,
						ErrorMessage = "No active conversation reference found"
					};
				}

				// Deserialize ConversationReference
				var convRef = JsonSerializer.Deserialize<ConversationReference>(
					conversationRef.ConversationReferenceJson);

				if (convRef == null)
				{
					_logger.LogError("Failed to deserialize conversation reference for Teams user {TeamsUserId}", teamsUserId);
					return new ProactiveMessageResult
					{
						Success = false,
						ErrorMessage = "Invalid conversation reference"
					};
				}

				// Send proactive message
				await _adapter.ContinueConversationAsync(
					claimsIdentity: _botIdentity,
					reference: convRef,
					callback: async (turnContext, ct) =>
					{
						await turnContext.SendActivityAsync(
							MessageFactory.Text(message),
							ct);
					},
					cancellationToken: cancellationToken);

				_logger.LogInformation("Sent proactive message to Teams user {TeamsUserId}", teamsUserId);

				// Reset failure count on successful send
				conversationRef.ConsecutiveFailureCount = 0;
				await _conversationRefRepo.UpdateAsync(conversationRef.Id, conversationRef);

				return new ProactiveMessageResult
				{
					Success = true,
					SentAt = DateTime.UtcNow
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error sending proactive message to Teams user {TeamsUserId}", teamsUserId);

				// Update failure count on conversation reference
				try
				{
					var conversationRef = await _conversationRefRepo.GetByTeamsUserIdAsync(teamsUserId, cancellationToken);
					if (conversationRef != null)
					{
						conversationRef.ConsecutiveFailureCount++;

						if (conversationRef.ConsecutiveFailureCount >= 5)
						{
							conversationRef.IsActive = false;
							_logger.LogWarning("Circuit breaker: Deactivated reference for Teams user {TeamsUserId} after {Count} consecutive failures",
								teamsUserId, conversationRef.ConsecutiveFailureCount);
						}

						await _conversationRefRepo.UpdateAsync(conversationRef.Id, conversationRef);
					}
				}
				catch (Exception updateEx)
				{
					_logger.LogError(updateEx, "Error updating failure count for Teams user {TeamsUserId}", teamsUserId);
				}

				return new ProactiveMessageResult
				{
					Success = false,
					ErrorMessage = ex.Message,
					SentAt = DateTime.UtcNow
				};
			}
		}
	}
}
