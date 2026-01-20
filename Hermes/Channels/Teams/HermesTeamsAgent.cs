using System.Text.Json;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Hermes.Orchestrator;
using Hermes.Orchestrator.PhraseGen;
using Hermes.Storage.Repositories.ConversationReference;

namespace Hermes.Channels.Teams
{
    /// <summary>
    /// Hermes Teams Agent Application using Microsoft Agents SDK.
    /// Provides Teams channel integration for the Hermes AI assistant, handling conversation updates, member additions, and message activities.
    /// </summary>
    public class HermesTeamsAgent : AgentApplication
    {
        private readonly IAgentOrchestrator _orchestrator;
        private readonly IWaitingPhraseGenerator _phraseGenerator;
        private readonly IConversationReferenceRepository _conversationRefRepo;
        private readonly ILogger<HermesTeamsAgent> _logger;

        /// <summary>
        /// Initializes a new instance of the HermesTeamsAgent class.
        /// Configures activity handlers for conversation updates and messages.
        /// </summary>
        /// <param name="options">Configuration options for the agent application, including authentication settings, storage configuration, and other application-level settings.</param>
        /// <param name="orchestrator">The Hermes orchestrator for processing user queries and generating AI responses.</param>
        /// <param name="phraseGenerator">Generator for creating fun waiting phrases.</param>
        /// <param name="conversationReferenceRepository">Repository for storing conversation references for proactive messaging.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public HermesTeamsAgent(
            AgentApplicationOptions options,
            IAgentOrchestrator orchestrator,
            IWaitingPhraseGenerator phraseGenerator,
            IConversationReferenceRepository conversationReferenceRepository,
            ILogger<HermesTeamsAgent> logger) : base(options)
        {
            _orchestrator = orchestrator;
            _phraseGenerator = phraseGenerator;
            _conversationRefRepo = conversationReferenceRepository;
            _logger = logger;

            // Register WelcomeMessageAsync delegate to handle when members are added to the conversation
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

            // Register OnMessageAsync delegate to handle all incoming message activities (runs last)
            OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
        }

        /// <summary>
        /// Sends a welcome message to new members added to the conversation.
        /// Excludes the bot itself from receiving welcome messages.
        /// Also invokes the agent orchestrator to provide a description of supported actions
        /// so users know how to interact with the agent.
        /// </summary>
        /// <param name="turnContext">The turn context containing information about the current conversation turn.</param>
        /// <param name="turnState">The state object for the current turn, providing access to user, conversation, and temporary state information.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation of sending welcome messages.</returns>
        private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id == turnContext.Activity.Recipient.Id)
                {
                    continue;
                }

                // Prompt the orchestrator to describe the supported set of actions
                const string capabilitiesPrompt =
                    "Provide a concise, user-friendly description of the supported actions and capabilities " +
                    "of the Hermes agent in Microsoft Teams so a new user understands how to interact with it. " +
                    "If the user is asking for help or capabilities, answer accordingly based on your supported capabilities section.";

                string capabilities = await _orchestrator.OrchestrateAsync(turnContext.Activity.Conversation?.Id ?? string.Empty, capabilitiesPrompt);

                // Fallback in case orchestrator returns an empty response
                if (string.IsNullOrWhiteSpace(capabilities))
                {
                    capabilities = "Hello and welcome! I am Hermes. I can analyze Azure DevOps work items and " +
                        "generate an executive-friendly newsletter summarizing project status, outcomes, risks, and timelines. " +
                        "For example, you can say: 'Generate a newsletter for epic 123456'.";
                }

                await turnContext.SendActivityAsync(
                    MessageFactory.Text(capabilities),
                    cancellationToken);
            }
        }

        /// <summary>
        /// Handles incoming message activities from Teams users.
        /// Uses the Hermes orchestrator to process the user's message and returns the AI response.
        /// Shows typing indicators with fun phrases while processing.
        /// </summary>
        /// <param name="turnContext">The turn context containing information about the current conversation turn, including the incoming message activity.</param>
        /// <param name="turnState">The state object for the current turn, providing access to user, conversation, and temporary state information.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation of processing and responding to the incoming message.</returns>
        private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            // Capture conversation reference for proactive messaging (fire-and-forget - non-critical)
            _ = _CaptureConversationReferenceAsync(turnContext, cancellationToken);

            var userText = turnContext.Activity.Text ?? string.Empty;

            // Use the Teams conversation id as the session id for history and orchestration
            var sessionId = turnContext.Activity.Conversation?.Id ?? string.Empty;

            string response;

            // Create typing indicator with a fun phrase and start sending typing activities
            using (var typingIndicator = new PeriodicTypingIndicator(turnContext, _phraseGenerator.GeneratePhrase()))
            {
                // Orchestrate with progress callback that receives waiting phrases
                response = await _orchestrator.OrchestrateAsync(
                    sessionId,
                    userText,
                    progressCallback: phrase =>
                    {
                        // Progress callback invoked when orchestration starts
                        // Typing indicator is already running in the background
                    });

                // Stop typing indicator before sending final response
                await typingIndicator.StopAsync();
            }

            if (string.IsNullOrWhiteSpace(response))
            {
                response = "Sorry, I was unable to generate a response. Please try rephrasing your request.";
            }

            await turnContext.SendActivityAsync(
                MessageFactory.Text(response),
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Captures and stores the conversation reference for proactive messaging.
        /// Updates existing references with the latest interaction time.
        /// </summary>
        /// <param name="turnContext">The turn context containing the activity information.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task _CaptureConversationReferenceAsync(
            ITurnContext turnContext,
            CancellationToken cancellationToken)
        {
            try
            {
                var activity = turnContext.Activity;
                var user = activity.From;
                var conversationId = activity.Conversation?.Id;

                if (user == null || string.IsNullOrEmpty(user.Id))
                {
                    _logger.LogWarning("Cannot capture conversation reference: user info missing");
                    return;
                }

                if (string.IsNullOrEmpty(conversationId))
                {
                    _logger.LogWarning("Cannot capture conversation reference: conversation ID missing");
                    return;
                }

                // Check if we already have this reference for this specific conversation
                var existing = await _conversationRefRepo.ReadAsync(conversationId, user.Id);

                if (existing != null)
                {
                    // Update last interaction timestamp
                    existing.LastInteractionAt = DateTime.UtcNow;
                    await _conversationRefRepo.UpdateAsync(existing.Id, existing);
                    _logger.LogDebug("Updated LastInteractionAt for conversation {ConversationId}", conversationId);
                    return;
                }

                // Create new conversation reference
                var convRef = activity.GetConversationReference();
                var convRefJson = JsonSerializer.Serialize(convRef);

                var document = new ConversationReferenceDocument
                {
                    Id = conversationId,
                    PartitionKey = user.Id,
                    TeamsUserId = user.Id,
                    ConversationId = conversationId,
                    ConversationReferenceJson = convRefJson,
                    LastInteractionAt = DateTime.UtcNow,
                    IsActive = true,
                    ConsecutiveFailureCount = 0
                };

                await _conversationRefRepo.CreateAsync(document);
                _logger.LogInformation("Captured new conversation reference for {TeamsUserId} in conversation {ConversationId}", user.Id, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing conversation reference");
                // Don't throw - this is non-critical for message handling
            }
        }
    }
}
