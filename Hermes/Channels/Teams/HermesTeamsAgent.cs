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
        /// </summary>
        /// <param name="turnContext">The turn context containing information about the current conversation turn.</param>
        /// <param name="turnState">The state object for the current turn, providing access to user, conversation, and temporary state information.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation of sending welcome messages.</returns>
        private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
                }
            }
        }

        /// <summary>
        /// Handles incoming message activities from Teams users.
        /// Currently echoes back the user's message as a placeholder implementation.
        /// </summary>
        /// <param name="turnContext">The turn context containing information about the current conversation turn, including the incoming message activity.</param>
        /// <param name="turnState">The state object for the current turn, providing access to user, conversation, and temporary state information.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation of processing and responding to the incoming message.</returns>
        private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken: cancellationToken);
        }
    }
}
