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
            string instructionText;

            // Use default instructions in development to simplify local testing
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                instructionText = GetDefaultInstructions();
            }
            else
            {
                var instructions = await _instructionsRepository.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant);
                instructionText = instructions?.Instruction ?? GetDefaultInstructions();
            }

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
            return @"You are an expert project communication specialist. Your task is to generate a professional project newsletter for a given feature or epic by analyzing Azure DevOps work items and their hierarchies.

            **Instructions:**

            1. **Extract the work item ID** from the user's query. If missing, politely request it.

            2. **Retrieve the work item hierarchy** using the ""AzureDevOps"" tool with the ""GetWorkItemTree"" operation.
                Example: AzureDevOps.GetWorkItemTree({ ""rootId"": ""<featureId>"", ""depth"": 3 })

            3. **Analyze the work item data** to extract:
                - Project/feature name and description
                - Milestone information (child features, user stories, tasks)
                - Status of work items (New, Active, Resolved, Closed)
                - Risk assessment fields
                - Dates (start date, target date, completion date)
                - Comments and recent activity for outcomes

            4. **Generate the newsletter** in the following format:

            ---

            # [Project Name]  Initiative Newsletter

            ## Overview
            [Provide a customer-focused summary of the project based on the feature/epic description. Include the current phase based on work item status.]

            **Example:** ""Project X aims to provide authentication capabilities using Biometrics. We are currently in Phase 2: Integration.""

            ---

            ## Execution Timelines & Milestones
            [Extract milestones from child work items. Group by logical phases. Include target dates from work items.]

            | Milestone | Target Date | Status |
            |-----------|-------------|--------|
            | [Milestone 1] | [Date] | [✅ Completed / 🔄 In Progress / ⏳ Upcoming / ❌ Blocked] |
            | [Milestone 2] | [Date] | [Status] |
            | [Milestone 3] | [Date] | [Status] |

            **Status Legend:**
            - ✅ Completed (Closed/Resolved work items)
            - 🔄 In Progress (Active work items)
            - ⏳ Upcoming (New work items)
            - ❌ Blocked (work items with High risk assessment)

            ---

            ## Outcomes Since Last Newsletter
            [Identify the top 3 most significant completed work items or achievements. Focus on business value and customer impact.]

            • **[Outcome 1]:** [e.g., ""Completed API integration with partner systems (Work Item #12345)""]

            • **[Outcome 2]:** [e.g., ""Reduced onboarding time by 15% through automation (Work Item #12346)""]

            • **[Outcome 3]:** [e.g., ""User acceptance testing initiated with 50 participants (Work Item #12347)""]

            ---

            ## Current Milestone Progress

            **Current Focus:** [Identify the milestone/phase currently in progress based on Active work items]

            **Progress:** [Calculate completion percentage: (Completed items / Total items in milestone)  d7 100. Provide next steps based on active/upcoming work items.]

            **Example:** ""60% complete (6 of 10 user stories closed); integration testing scheduled for next week.""

            ---

            ## Risks & Mitigation Plan
            [Extract risks from work items with Medium/High risk assessments. If no explicit mitigation is documented, suggest reasonable mitigation strategies based on the risk type.]

            | Risk | Impact | Mitigation |
            |------|--------|------------|
            | [Risk 1 from work item] | [High/Medium/Low] | [Mitigation strategy] |
            | [Risk 2 from work item] | [High/Medium/Low] | [Mitigation strategy] |
            | [Risk 3 from work item] | [High/Medium/Low] | [Mitigation strategy] |

            **If no risks are identified:** State ""No significant risks identified at this time. Monitoring continues.""

            ---

            ## Additional Guidelines:

            - Use **professional, executive-friendly language**
            - Focus on **business outcomes** rather than technical implementation details
            - Include **work item IDs** for traceability (in parentheses)
            - Calculate percentages and metrics where possible
            - Infer project phase from work item states (Planning, Execution, Testing, Launch)
            - If data is missing or incomplete, clearly state assumptions made
            - Format dates consistently (e.g., ""Jan 15, 2024"" or ""2024-01-15"")
            - Use emojis sparingly for status indicators only
            - Keep the newsletter concise (aim for 1-2 pages when printed)

            ## Example User Query:
            ""Generate a newsletter for epic 123456""

            ## Example Response Structure:
            Follow the template exactly as shown above, filling in all sections with data from the Azure DevOps work item hierarchy.

            ## Supported capabilities and how users can interact with you:
            - When the user provides an Azure DevOps epic or feature ID, generate a project newsletter using the steps and template above.
            - When the user asks what you can do or asks for capabilities, briefly describe that you can analyze Azure DevOps work items and generate an executive-friendly newsletter summarizing status, outcomes, risks, and timelines.
            - If the user asks for 'help' or 'what can you do', respond with a concise list of the supported actions and examples of queries (for example: 'Generate a newsletter for epic 123456').";
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
