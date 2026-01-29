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
        /// Uses Teams streaming UX to show tool call progress updates while processing.
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

            // Streaming state - shared across the progress callback
            string? streamId = null;
            int streamSequence = 1;

            // Start streaming with informative update ("Thinking...")
            try
            {
                streamId = await _SendStreamingInformativeUpdateAsync(turnContext, "Thinking...", streamSequence, null, cancellationToken);
                streamSequence++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start streaming, will fall back to regular message");
            }

            // Progress callback for intermediate updates
            Action<string> progressCallback = (message) =>
            {
                if (string.IsNullOrEmpty(streamId))
                {
                    return; // Streaming not available
                }

                // Teams informative updates have a 1000 character limit per the streaming UX docs.
                // Skip messages that exceed this limit to avoid API errors.
                if (message.Length > 1000)
                {
                    _logger.LogWarning("Skipping streaming progress update - message exceeds 1000 char limit: {Length}", message.Length);
                    return;
                }

                try
                {
                    // Fire-and-forget the informative update
                    _ = _SendStreamingInformativeUpdateAsync(turnContext, message, streamSequence, streamId, cancellationToken);
                    streamSequence++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send streaming progress update: {Message}", message);
                }
            };

            // Run orchestration with progress callback
            string response;
            try
            {
                response = await _orchestrator.OrchestrateAsync(sessionId, userText, progressCallback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orchestration error");
                response = "Sorry, I was unable to generate a response. Please try rephrasing your request.";
            }

            if (string.IsNullOrWhiteSpace(response))
            {
                response = "Sorry, I was unable to generate a response. Please try rephrasing your request.";
            }

            // Send final message (with streaming if available, otherwise regular message)
            if (!string.IsNullOrEmpty(streamId))
            {
                try
                {
                    await _SendStreamingFinalMessageAsync(turnContext, streamId, response, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send streaming final message, falling back to regular message");
                    await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
                }
            }
            else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
            }
        }

        /// <summary>
        /// Sends an informative streaming update to Teams (e.g., "Thinking...").
        /// This appears as a blue progress bar at the bottom of the chat.
        /// </summary>
        /// <param name="turnContext">The turn context.</param>
        /// <param name="message">The informative message to display.</param>
        /// <param name="streamSequence">The sequence number for this update (starts at 1).</param>
        /// <param name="streamId">The stream ID for subsequent updates (null for the first update).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stream ID to use for subsequent streaming updates, or null if streaming failed.</returns>
        private async Task<string?> _SendStreamingInformativeUpdateAsync(
            ITurnContext turnContext,
            string message,
            int streamSequence,
            string? streamId,
            CancellationToken cancellationToken)
        {
            var streamInfoProperties = new Dictionary<string, JsonElement>
            {
                ["streamType"] = JsonSerializer.SerializeToElement("informative"),
                ["streamSequence"] = JsonSerializer.SerializeToElement(streamSequence)
            };

            // Add streamId for subsequent updates (after the first one)
            if (!string.IsNullOrEmpty(streamId))
            {
                streamInfoProperties["streamId"] = JsonSerializer.SerializeToElement(streamId);
            }

            var informativeActivity = new Activity
            {
                Type = ActivityTypes.Typing,
                Text = message,
                Entities = new List<Entity>
                {
                    new Entity
                    {
                        Type = "streaminfo",
                        Properties = streamInfoProperties
                    }
                }
            };

            var response = await turnContext.SendActivityAsync(informativeActivity, cancellationToken);
            _logger.LogDebug("Sent streaming informative update (seq={StreamSequence}): {Message}", streamSequence, message);
            return response?.Id;
        }

        /// <summary>
        /// Sends the final streaming message to Teams, completing the stream.
        /// </summary>
        /// <param name="turnContext">The turn context.</param>
        /// <param name="streamId">The stream ID from the initial informative update.</param>
        /// <param name="message">The final message content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task _SendStreamingFinalMessageAsync(
            ITurnContext turnContext,
            string streamId,
            string message,
            CancellationToken cancellationToken)
        {
            var streamInfoProperties = new Dictionary<string, JsonElement>
            {
                ["streamId"] = JsonSerializer.SerializeToElement(streamId),
                ["streamType"] = JsonSerializer.SerializeToElement("final")
            };

            var finalActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = message,
                Entities = new List<Entity>
                {
                    new Entity
                    {
                        Type = "streaminfo",
                        Properties = streamInfoProperties
                    }
                }
            };

            await turnContext.SendActivityAsync(finalActivity, cancellationToken);
            _logger.LogDebug("Sent final streaming message for stream ID: {StreamId}", streamId);
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

                    // Update AadObjectId if it wasn't previously captured (backward compatibility)
                    if (string.IsNullOrEmpty(existing.AadObjectId))
                    {
                        existing.AadObjectId = await _ExtractAadObjectIdAsync(user);
                        if (!string.IsNullOrEmpty(existing.AadObjectId))
                        {
                            _logger.LogInformation("Captured AadObjectId {AadObjectId} for existing conversation {ConversationId}",
                                existing.AadObjectId, conversationId);
                        }
                    }

                    await _conversationRefRepo.UpdateAsync(existing.Id, existing);
                    _logger.LogDebug("Updated LastInteractionAt for conversation {ConversationId}", conversationId);
                    return;
                }

                // Create new conversation reference
                var convRef = activity.GetConversationReference();
                var convRefJson = JsonSerializer.Serialize(convRef);

                // Extract Azure AD Object ID from user Properties (Teams-specific)
                // This is needed for Microsoft Graph API calls
                var aadObjectId = await _ExtractAadObjectIdAsync(user);

                var document = new ConversationReferenceDocument
                {
                    Id = conversationId,
                    PartitionKey = user.Id,
                    TeamsUserId = user.Id,
                    AadObjectId = aadObjectId,
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

        /// <summary>
        /// Extracts Azure AD Object ID from user's Properties.
        /// In production Teams: Reads from activity.From.Properties["aadObjectId"]
        /// In local dev (agentsplayground): Falls back to currently logged-in user from az CLI
        /// </summary>
        /// <param name="user">Channel account from Teams activity</param>
        /// <returns>Azure AD Object ID (GUID), or null if not available</returns>
        private async Task<string?> _ExtractAadObjectIdAsync(ChannelAccount user)
        {
            try
            {
                // Try to get AAD Object ID from user Properties (production Teams scenario)
                if (user.Properties != null && user.Properties.TryGetValue("aadObjectId", out var aadObjIdValue))
                {
                    var jsonElement = (JsonElement)aadObjIdValue;
                    var aadObjectId = jsonElement.GetString();

                    // Check if it's a real GUID or a mock value
                    if (!string.IsNullOrEmpty(aadObjectId) && Guid.TryParse(aadObjectId, out _))
                    {
                        _logger.LogDebug("Extracted AadObjectId {AadObjectId} from user Properties for {TeamsUserId}",
                            aadObjectId, user.Id);
                        return aadObjectId;
                    }

                    _logger.LogWarning("AadObjectId '{AadObjectId}' from Properties is not a valid GUID for {TeamsUserId}",
                        aadObjectId, user.Id);
                }

                // Fallback: Local development with agentsplayground - use az login user
                _logger.LogInformation("AadObjectId not found in user Properties for {TeamsUserId}. Attempting fallback to az CLI logged-in user (local dev scenario).",
                    user.Id);

                // On Windows, we need to use cmd.exe to execute az CLI
                // On Linux/Mac, we can execute az directly
                var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                var fileName = isWindows ? "cmd.exe" : "az";
                var arguments = isWindows ? "/c az ad signed-in-user show --query id -o tsv" : "ad signed-in-user show --query id -o tsv";

                var azProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                azProcess.Start();
                var output = await azProcess.StandardOutput.ReadToEndAsync();
                var error = await azProcess.StandardError.ReadToEndAsync();
                await azProcess.WaitForExitAsync();

                if (azProcess.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    var fallbackAadObjectId = output.Trim();
                    if (Guid.TryParse(fallbackAadObjectId, out _))
                    {
                        _logger.LogInformation("Using fallback AadObjectId {AadObjectId} from az CLI for {TeamsUserId} (local dev)",
                            fallbackAadObjectId, user.Id);
                        return fallbackAadObjectId;
                    }
                }

                _logger.LogWarning("Failed to get AadObjectId from az CLI. Error: {Error}. Exit code: {ExitCode}",
                    error, azProcess.ExitCode);
                _logger.LogWarning("AadObjectId not available for {TeamsUserId}. Microsoft Graph features may not work.", user.Id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting AadObjectId for user {TeamsUserId}", user.Id);
                return null;
            }
        }
    }
}
