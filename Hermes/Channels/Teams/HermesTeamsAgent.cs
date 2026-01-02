using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Hermes.Orchestrator;

namespace Hermes.Channels.Teams
{
    /// <summary>
    /// Hermes Teams Agent Application using Microsoft Agents SDK.
    /// Provides Teams channel integration for the Hermes AI assistant, handling conversation updates, member additions, and message activities.
    /// </summary>
    public class HermesTeamsAgent : AgentApplication
    {
        private readonly IAgentOrchestrator _orchestrator;

        /// <summary>
        /// Initializes a new instance of the HermesTeamsAgent class.
        /// Configures activity handlers for conversation updates and messages.
        /// </summary>
        /// <param name="options">Configuration options for the agent application, including authentication settings, storage configuration, and other application-level settings.</param>
        /// <param name="orchestrator">The Hermes orchestrator for processing user queries and generating AI responses.</param>
        public HermesTeamsAgent(AgentApplicationOptions options, IAgentOrchestrator orchestrator) : base(options)
        {
            _orchestrator = orchestrator;

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
        /// </summary>
        /// <param name="turnContext">The turn context containing information about the current conversation turn, including the incoming message activity.</param>
        /// <param name="turnState">The state object for the current turn, providing access to user, conversation, and temporary state information.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation of processing and responding to the incoming message.</returns>
        private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var userText = turnContext.Activity.Text ?? string.Empty;

            // Use the Teams conversation id as the session id for history and orchestration
            var sessionId = turnContext.Activity.Conversation?.Id ?? string.Empty;

            var response = await _orchestrator.OrchestrateAsync(sessionId, userText);

            if (string.IsNullOrWhiteSpace(response))
            {
                response = "Sorry, I was unable to generate a response. Please try rephrasing your request.";
            }

            await turnContext.SendActivityAsync(
                MessageFactory.Text(response),
                cancellationToken: cancellationToken);
        }
    }
}
