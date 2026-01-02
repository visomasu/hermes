using Azure.AI.OpenAI;
using Azure.Identity;
using Hermes.Storage.Repositories.HermesInstructions;
using Hermes.Storage.Repositories.ConversationHistory;
using Hermes.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Orchestrator
{
    /// <summary>
    /// Hermes Orchestrator.
    /// Responsible for orchestrating the execution of tasks in response to the user input over chat modality.
    /// </summary>
    public class HermesOrchestrator : IAgentOrchestrator
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        private readonly IHermesInstructionsRepository _instructionsRepository;
        private readonly IConversationHistoryRepository _conversationHistoryRepository;

        private readonly List<AITool> _tools = new();
        private readonly Dictionary<string, AIAgent> _agentCache = new();

        // Default number of past turns to include in the context window.
        private const int DefaultContextTurns = 5;

        /// <summary>
        /// Initializes a new instance of the HermesOrchestrator class using the specified endpoint and API key.
        /// </summary>
        /// <param name="endpoint">The URI endpoint of the Azure OpenAI resource to connect to. Must be a valid, accessible endpoint.</param>
        /// <param name="apiKey">The API key used to authenticate requests to the Azure OpenAI service. Cannot be null or empty.</param>
        /// <param name="agentTools">An optional list of agent tools to register with the agent.</param>
        /// <param name="instructionsRepository">Repository for fetching agent instructions.</param>
        /// <param name="conversationHistoryRepository">Repository for persisting conversation history across turns.</param>
        public HermesOrchestrator(
            string endpoint,
            string apiKey,
            IEnumerable<IAgentTool> agentTools,
            IHermesInstructionsRepository instructionsRepository,
            IConversationHistoryRepository conversationHistoryRepository)
        {
            _instructionsRepository = instructionsRepository;
            _conversationHistoryRepository = conversationHistoryRepository;
            _endpoint = endpoint;
            _apiKey = apiKey;

            InitializeAgentTools(agentTools);
        }

        /// <summary>
        /// Test-only constructor for injecting a mock AIAgent.
        /// This bypasses the internal AzureOpenAIClient creation logic and uses the provided agent instance.
        /// </summary>
        public HermesOrchestrator(
            AIAgent agent,
            string endpoint,
            string apiKey,
            IHermesInstructionsRepository instructionsRepository,
            IEnumerable<IAgentTool> agentTools,
            IConversationHistoryRepository conversationHistoryRepository)
        {
            _instructionsRepository = instructionsRepository;
            _conversationHistoryRepository = conversationHistoryRepository;
            _endpoint = endpoint;
            _apiKey = apiKey;

            InitializeAgentTools(agentTools);

            // Seed the cache with the provided agent using a stable key for tests.
            var cacheKey = "TestAgent";
            _agentCache[cacheKey] = agent;
        }

        #region Agent Initialization

        /// <summary>
        /// Creates the AI agent with instructions from the repository.
        /// </summary>
        private async Task<AIAgent> CreateAgentAsync(string instructions)
        {
            var chatClient = new AzureOpenAIClient(
                new Uri(_endpoint),
                new AzureCliCredential())
                    .GetChatClient("gpt-5-mini");

            return chatClient.CreateAIAgent(
                instructions: instructions,
                tools: _tools
            );
        }

        /// <summary>
        /// Gets the agent, creating it lazily if not already created.
        /// Checks if instructions have changed and recreates the agent if necessary.
        /// If a test agent was provided via the test-only constructor, it will be returned from the cache.
        /// </summary>
        private async Task<AIAgent> GetAgentAsync()
        {
            // If a test agent has been injected via the alternate constructor, return it.
            if (_agentCache.TryGetValue("TestAgent", out var testAgent))
            {
                return testAgent;
            }

            var instructionType = HermesInstructionType.ProjectAssistant;

            var instructionsEntity = await _instructionsRepository.GetByInstructionTypeAsync(instructionType).ConfigureAwait(false);
            if (instructionsEntity == null || string.IsNullOrWhiteSpace(instructionsEntity.Instruction))
            {
                throw new InvalidOperationException($"No instructions found for type '{instructionType}'.");
            }

            var instructionText = instructionsEntity.Instruction;
            var cacheKey = GenerateCacheKey(instructionType, instructionText);

            if (!_agentCache.TryGetValue(cacheKey, out var agent))
            {
                agent = await CreateAgentAsync(instructionText).ConfigureAwait(false);
                _agentCache[cacheKey] = agent;
            }

            return agent;
        }

        private static string GenerateCacheKey(HermesInstructionType instructionType, string instruction)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(instruction));
            var hashString = Convert.ToHexString(hash);
            return $"{instructionType}:{hashString}";
        }

        private void InitializeAgentTools(IEnumerable<IAgentTool> agentTools)
        {
            foreach (var tool in agentTools)
            {
                var options = new AIFunctionFactoryOptions
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    AdditionalProperties = new Dictionary<string, object?>
                    {
                        { "metadata", tool.GetMetadata() }
                    }
                };

                var aiFunction = AIFunctionFactory.Create(
                    method: new Func<string, string, Task<string>>(async (operation, input) =>
                    {
                        return await tool.ExecuteAsync(operation, input).ConfigureAwait(false);
                    }),
                    options: options);

                _tools.Add(aiFunction);
            }
        }

        #endregion

        public async Task<string> OrchestrateAsync(string sessionId, string query)
        {
            var agent = await GetAgentAsync().ConfigureAwait(false);

            // Build context window from recent conversation history.
            var contextMessages = await BuildContextWindowAsync(sessionId, DefaultContextTurns).ConfigureAwait(false);

            // Append the current user query as the last message.
            contextMessages.Add(new ChatMessage(ChatRole.User, [new TextContent(query)]));

            var response = await agent.RunAsync(contextMessages).ConfigureAwait(false);
            var responseText = response.AsChatResponse().Text;

            var historyEntries = new List<ConversationMessage>
            {
                new ConversationMessage
                {
                    Role = "user",
                    Content = query,
                    Timestamp = DateTimeOffset.UtcNow
                },
                new ConversationMessage
                {
                    Role = "assistant",
                    Content = responseText,
                    Timestamp = DateTimeOffset.UtcNow
                }
            };

            await _conversationHistoryRepository
                .WriteConversationHistoryAsync(sessionId, historyEntries)
                .ConfigureAwait(false);

            return responseText;
        }

        /// <summary>
        /// Builds a list of chat messages representing the top N most recent dialogue turns
        /// from the stored conversation history for the given session.
        /// </summary>
        private async Task<List<ChatMessage>> BuildContextWindowAsync(string sessionId, int topNTurns)
        {
            var historyJson = await _conversationHistoryRepository
                .GetConversationHistoryAsync(sessionId)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(historyJson) || topNTurns <= 0)
            {
                return new List<ChatMessage>();
            }

            var history = JsonSerializer.Deserialize<List<ConversationMessage>>(historyJson) ?? new List<ConversationMessage>();

            if (history.Count == 0)
            {
                return new List<ChatMessage>();
            }

            var ordered = history
                .OrderBy(m => m.Timestamp)
                .ToList();

            var result = new List<ConversationMessage>();
            var turnsCollected = 0;

            // Walk backwards, collecting (user, assistant) pairs as "turns".
            for (var i = ordered.Count - 1; i >= 0 && turnsCollected < topNTurns;)
            {
                ConversationMessage? assistant = null;
                ConversationMessage? user = null;

                if (i >= 0 && string.Equals(ordered[i].Role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    assistant = ordered[i];
                    i--;
                }

                if (i >= 0 && string.Equals(ordered[i].Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    user = ordered[i];
                    i--;
                }

                if (assistant != null || user != null)
                {
                    // Insert at front to keep chronological order.
                    if (user != null)
                    {
                        result.Insert(0, user);
                    }

                    if (assistant != null)
                    {
                        result.Insert(0, assistant);
                    }

                    turnsCollected++;
                }
            }

            var messages = new List<ChatMessage>(result.Count);

            foreach (var entry in result)
            {
                var role = string.Equals(entry.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User;

                messages.Add(new ChatMessage(role, [new TextContent(entry.Content ?? string.Empty)]));
            }

            return messages;
        }
    }
}
