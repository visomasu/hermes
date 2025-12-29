using Azure.AI.OpenAI;
using Azure.Identity;
using Hermes.Storage.Repositories.HermesInstructions;
using Hermes.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.Security.Cryptography;
using System.Text;

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

        private readonly List<AITool> _tools = new();
        private readonly Dictionary<string, AIAgent> _agentCache = new();

        /// <summary>
        /// Initializes a new instance of the HermesOrchestrator class using the specified endpoint and API key.
        /// </summary>
        /// <param name="endpoint">The URI endpoint of the Azure OpenAI resource to connect to. Must be a valid, accessible endpoint.</param>
        /// <param name="apiKey">The API key used to authenticate requests to the Azure OpenAI service. Cannot be null or empty.</param>
        /// <param name="agentTools">An optional list of agent tools to register with the agent.</param>
        /// <param name="instructionsRepository">Repository for fetching agent instructions.</param>
        public HermesOrchestrator(
            string endpoint, 
            string apiKey, 
            IEnumerable<IAgentTool> agentTools,
            IHermesInstructionsRepository instructionsRepository)
        {
            _instructionsRepository = instructionsRepository;
            _endpoint = endpoint;
            _apiKey = apiKey;

            InitializeAgentTools(agentTools);        
        }

        /// <summary>
        /// Test-only constructor for injecting a mock AIAgent.
        /// </summary>
        public HermesOrchestrator(AIAgent agent, string endpoint, string apiKey, IHermesInstructionsRepository instructionsRepository, IEnumerable<IAgentTool> agentTools)
        {
            _instructionsRepository = instructionsRepository;
            _endpoint = endpoint;
            _apiKey = apiKey;

            InitializeAgentTools(agentTools);

            var defaultInstructions = GetDefaultInstructions();
            var cacheKey = GenerateCacheKey(HermesInstructionType.ProjectAssistant, defaultInstructions);
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
        /// </summary>
        private async Task<AIAgent> GetAgentAsync()
        {
            var instructions = await _instructionsRepository.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant);
            var instructionText = instructions?.Instruction ?? GetDefaultInstructions();

            var cacheKey = GenerateCacheKey(HermesInstructionType.ProjectAssistant, instructionText);

            if (!_agentCache.TryGetValue(cacheKey, out var agent))
            {
                agent = await CreateAgentAsync(instructionText);
                _agentCache[cacheKey] = agent;
            }

            return agent;
        }

        /// <summary>
        /// Generates a cache key based on instruction type and instruction content hash.
        /// </summary>
        /// <param name="instructionType">The type of instruction.</param>
        /// <param name="instruction">The instruction content.</param>
        /// <returns>A unique cache key.</returns>
        private static string GenerateCacheKey(HermesInstructionType instructionType, string instruction)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(instruction));
            var hashString = Convert.ToHexString(hash);
            return $"{instructionType}:{hashString}";
        }

        private string GetDefaultInstructions()
        {
            return @"You are an expert project assistant. Your task is to summarize the status of a software feature by evaluating the current status and risk assessment fields for all related work items in the feature's hierarchy.

                            1. Extract the feature's work item ID from the user's query. If the work item ID is missing, politely request it from the user.
                            2. When you have the work item ID, use the ""AzureDevOps"" tool with the ""GetWorkItemTree"" operation, passing the feature's work item ID as input. The tool will return the full hierarchy of work items in JSON format.
                               Example: AzureDevOps.GetWorkItemTree({ ""rootId"": ""<featureId>"", ""depth"": 2 })
                               If depth is not specified, default to 2.
                            3. Analyze the ""status"" and ""riskAssessment"" fields for each work item in the hierarchy.
                            4. Summarize the overall feature status, clearly highlighting any risks or blockers found.
                            5. Present your summary in concise, non-technical language suitable for project stakeholders.";
        }

        /// <summary>
        /// Registers agent tools by converting them to AIFunctions and adding to the toolset.
        /// </summary>
        /// <param name="agentTools">The agent tools to register.</param>
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
                        return await tool.ExecuteAsync(operation, input);
                    }),
                    options: options);

                _tools.Add(aiFunction);
            }
        }

        #endregion

        /// <summary>
        /// Orchestrates operations based on the user's query and returns the response.
        /// </summary>
        /// <param name="query">The input query from the user.</param>
        /// <returns>Response as a string.</returns>
        public async Task<string> OrchestrateAsync(string query)
        {
            var agent = await GetAgentAsync();
            
            ChatMessage message = new(ChatRole.User, [
                new TextContent(query),
            ]);

            var response = await agent.RunAsync(message);
            return response.AsChatResponse().Text;
        }
    }
}
