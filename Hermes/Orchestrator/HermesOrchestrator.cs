using Azure.AI.OpenAI;
using Azure.Identity;
using Hermes.Orchestrator.Context;
using Hermes.Orchestrator.PhraseGen;
using Hermes.Orchestrator.Prompts;
using Hermes.Storage.Repositories.HermesInstructions;
using Hermes.Storage.Repositories.ConversationHistory;
using Hermes.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
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
        private readonly IAgentPromptComposer _agentPromptComposer;
        private readonly IWaitingPhraseGenerator _phraseGenerator;
        private readonly IConversationContextSelector _contextSelector;
        private readonly ILogger<HermesOrchestrator> _logger;

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
        /// <param name="logger">Logger instance.</param>
        /// <param name="agentPromptComposer">Component responsible for composing agent prompts from instruction files.</param>
        /// <param name="phraseGenerator">Generator for creating fun waiting phrases.</param>
        /// <param name="contextSelector">Selector for choosing relevant conversation context based on semantic similarity.</param>
        public HermesOrchestrator(
            ILogger<HermesOrchestrator> logger,
            string endpoint,
            string apiKey,
            IEnumerable<IAgentTool> agentTools,
            IHermesInstructionsRepository instructionsRepository,
            IConversationHistoryRepository conversationHistoryRepository,
            IAgentPromptComposer agentPromptComposer,
            IWaitingPhraseGenerator phraseGenerator,
            IConversationContextSelector contextSelector)
        {
            _instructionsRepository = instructionsRepository;
            _conversationHistoryRepository = conversationHistoryRepository;
            _agentPromptComposer = agentPromptComposer;
            _phraseGenerator = phraseGenerator;
            _contextSelector = contextSelector;
            _logger = logger;
            _endpoint = endpoint;
            _apiKey = apiKey;

            InitializeAgentTools(agentTools);
        }

        /// <summary>
        /// Test-only constructor for injecting a mock AIAgent.
        /// This bypasses the internal AzureOpenAIClient creation logic and uses the provided agent instance.
        /// </summary>
        public HermesOrchestrator(
            ILogger<HermesOrchestrator> logger,
            AIAgent agent,
            string endpoint,
            string apiKey,
            IHermesInstructionsRepository instructionsRepository,
            IEnumerable<IAgentTool> agentTools,
            IConversationHistoryRepository conversationHistoryRepository,
            IAgentPromptComposer agentPromptComposer,
            IWaitingPhraseGenerator phraseGenerator,
            IConversationContextSelector contextSelector)
        {
            _instructionsRepository = instructionsRepository;
            _conversationHistoryRepository = conversationHistoryRepository;
            _agentPromptComposer = agentPromptComposer;
            _phraseGenerator = phraseGenerator;
            _contextSelector = contextSelector;
            _logger = logger;
            _endpoint = endpoint;
            _apiKey = apiKey;

            InitializeAgentTools(agentTools);

            // Seed the cache with the provided agent using a stable key for tests.
            var cacheKey = "TestAgent";
            _agentCache[cacheKey] = agent;
        }

        #region Agent Initialization

        /// <summary>
        /// Creates the AI agent with instructions from the prompt composer for the given instruction type.
        /// </summary>
        private async Task<AIAgent> CreateAgentAsync(string instructionText)
        {
            var chatClient = new AzureOpenAIClient(
                new Uri(_endpoint),
                new AzureCliCredential())
                    .GetChatClient("gpt-5-mini");

            return chatClient.CreateAIAgent(
                instructions: instructionText,
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

            //var instructionsEntity = await _instructionsRepository.GetByInstructionTypeAsync(instructionType).ConfigureAwait(false);
            //if (instructionsEntity == null || string.IsNullOrWhiteSpace(instructionsEntity.Instruction))
            //{
            //    throw new InvalidOperationException($"No instructions found for type '{instructionType}'.");
            //}

            var instructionText = _agentPromptComposer.ComposePrompt(instructionType);
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
                        // Truncate input for logging if too long
                        var truncatedInput = input != null && input.Length > 500
                            ? input[..500] + "..."
                            : input ?? "";

                        // Log tool invocation with structured data including input
                        _logger.LogInformation(
                            "[ToolInvocation] Tool={ToolName} Operation={Operation} Input={Input}",
                            tool.Name,
                            operation,
                            truncatedInput);

                        var result = await tool.ExecuteAsync(operation, input).ConfigureAwait(false);

                        _logger.LogInformation(
                            "[ToolResult] Tool={ToolName} Operation={Operation} Success=true",
                            tool.Name,
                            operation);

                        return result;
                    }),
                    options: options);

                _tools.Add(aiFunction);
            }
        }

        #endregion

        /// <inheritdoc/>
        public async Task<string> OrchestrateAsync(string sessionId, string query)
        {
            return await OrchestrateAsync(sessionId, query, null).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> OrchestrateAsync(string sessionId, string query, Action<string>? progressCallback = null)
        {
            // Extract userId from sessionId if present (format: "userId|actualSessionId")
            string? userId = null;
            string actualSessionId = sessionId;

            if (sessionId.Contains('|'))
            {
                var parts = sessionId.Split('|', 2);
                userId = parts[0];
                actualSessionId = parts[1];
            }

            _logger.LogInformation(
                "[OrchestrationStart] SessionId={SessionId} UserId={UserId} QueryLength={QueryLength}",
                actualSessionId,
                userId ?? "none",
                query?.Length ?? 0);

            var agent = await GetAgentAsync().ConfigureAwait(false);

            // Build context window from relevant conversation history using semantic filtering.
            var contextMessages = await BuildContextWindowAsync(actualSessionId, query, DefaultContextTurns).ConfigureAwait(false);

            // If userId is provided, prepend a system message with user context override
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var userContextMessage = new ChatMessage(
                    ChatRole.System,
                    [new TextContent($"## User Context Override\nCurrent user: {userId} (Teams User ID for tool calls)")]);
                contextMessages.Insert(0, userContextMessage);
            }

            // Append the current user query as the last message.
            contextMessages.Add(new ChatMessage(ChatRole.User, [new TextContent(query)]));

            // Invoke progress callback with a fun phrase before starting agent execution.
            progressCallback?.Invoke(_phraseGenerator.GeneratePhrase());

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
                .WriteConversationHistoryAsync(actualSessionId, historyEntries)
                .ConfigureAwait(false);

            return responseText;
        }

        /// <summary>
        /// Builds a list of chat messages representing relevant dialogue turns
        /// from the stored conversation history for the given session, filtered by semantic relevance to the current query.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="currentQuery">The current user query to compare against for relevance.</param>
        /// <param name="maxContextTurns">Maximum number of context turns to include (used as fallback).</param>
        private async Task<List<ChatMessage>> BuildContextWindowAsync(string sessionId, string currentQuery, int maxContextTurns)
        {
            var historyJson = await _conversationHistoryRepository
                .GetConversationHistoryAsync(sessionId)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(historyJson))
            {
                return new List<ChatMessage>();
            }

            // Deserialize the history JSON into ConversationMessage objects.
            var history = JsonSerializer.Deserialize<List<ConversationMessage>>(historyJson) ?? new List<ConversationMessage>();

            if (history.Count == 0)
            {
                return new List<ChatMessage>();
            }

            // Use context selector to filter relevant messages based on semantic similarity.
            var selectedMessages = await _contextSelector
                .SelectRelevantContextAsync(currentQuery, history)
                .ConfigureAwait(false);

            return selectedMessages
                .Select(m => new ChatMessage(
                    m.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                    [new TextContent(m.Content ?? string.Empty)]))
                .ToList();
        }
    }
}
