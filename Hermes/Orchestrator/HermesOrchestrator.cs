using Azure.AI.OpenAI;
using Azure.Identity;
using Hermes.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Hermes.Orchestrator
{
    /// <summary>
    /// Hermes Orchestrator.
    /// Responsible for orchestrating the execution of tasks in response to the user input over chat modality.
    /// </summary>
    public class HermesOrchestrator : IAgentOrchestrator
    {
        private readonly AIAgent _hermes;
        private readonly List<AITool> _tools = new();

        /// <summary>
        /// Initializes a new instance of the HermesOrchestrator class using the specified endpoint and API key.
        /// </summary>
        /// <param name="endpoint">The URI endpoint of the Azure OpenAI resource to connect to. Must be a valid, accessible endpoint.</param>
        /// <param name="apiKey">The API key used to authenticate requests to the Azure OpenAI service. Cannot be null or empty.</param>
        /// <param name="agentTools">An optional list of agent tools to register with the agent.</param>
        public HermesOrchestrator(string endpoint, string apiKey, IEnumerable<IAgentTool> agentTools)
        {
            RegisterAgentTools(agentTools);

            this._hermes = new AzureOpenAIClient(
                new Uri(endpoint),
                new AzureCliCredential())
                    .GetChatClient("gpt-5-mini")
                    .CreateAIAgent(
                        instructions:
                            @"You are an expert project assistant. Your task is to summarize the status of a software feature by evaluating the current status and risk assessment fields for all related work items in the feature's hierarchy.

                            1. Extract the feature's work item ID from the user's query. If the work item ID is missing, politely request it from the user.
                            2. When you have the work item ID, use the ""AzureDevOps"" tool with the ""GetWorkItemTree"" operation, passing the feature's work item ID as input. The tool will return the full hierarchy of work items in JSON format.
                               Example: AzureDevOps.GetWorkItemTree({ ""rootId"": ""<featureId>"", ""depth"": 2 })
                               If depth is not specified, default to 2.
                            3. Analyze the ""status"" and ""riskAssessment"" fields for each work item in the hierarchy.
                            4. Summarize the overall feature status, clearly highlighting any risks or blockers found.
                            5. Present your summary in concise, non-technical language suitable for project stakeholders.",
                        tools: _tools
                    );
        }

        /// <summary>
        /// Test-only constructor for injecting a mock AIAgent.
        /// </summary>
        public HermesOrchestrator(AIAgent agent, IEnumerable<IAgentTool> agentTools)
        {
            RegisterAgentTools(agentTools);
            this._hermes = agent;
        }

        /// <summary>
        /// Registers agent tools by converting them to AIFunctions and adding to the toolset.
        /// </summary>
        /// <param name="agentTools">The agent tools to register.</param>
        private void RegisterAgentTools(IEnumerable<IAgentTool> agentTools)
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

        /// <summary>
        /// Orchestrates operations based on the user's query and returns the response.
        /// </summary>
        /// <param name="query">The input query from the user.</param>
        /// <returns>Response as a string.</returns>
        public async Task<string> OrchestrateAsync(string query)
        {
            ChatMessage message = new(ChatRole.User, [
                new TextContent(query),
            ]);

            // Example orchestration: send the query to the agent and return its response
            var response = await _hermes.RunAsync(message);
            return response.AsChatResponse().Text;
        }
    }
}
