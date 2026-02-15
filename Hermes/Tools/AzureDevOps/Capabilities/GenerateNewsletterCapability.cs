using System.Text.Json;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Hermes.Orchestrator.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Hermes.Tools.AzureDevOps.Capabilities
{
	/// <summary>
	/// Capability for generating executive newsletters from Azure DevOps work item hierarchies.
	/// Uses a two-phase approach: data retrieval followed by LLM synthesis with an optimized model.
	/// </summary>
	public sealed class GenerateNewsletterCapability : IAgentToolCapability<GenerateNewsletterCapabilityInput>
	{
		private readonly IAgentToolCapability<GetWorkItemTreeCapabilityInput> _treeCapability;
		private readonly IModelSelector _modelSelector;
		private readonly ILogger<GenerateNewsletterCapability> _logger;

		/// <summary>
		/// Initializes a new instance of <see cref="GenerateNewsletterCapability"/>.
		/// </summary>
		/// <param name="treeCapability">Capability for retrieving work item hierarchies</param>
		/// <param name="modelSelector">Selector for choosing appropriate LLM models</param>
		/// <param name="logger">Logger instance</param>
		public GenerateNewsletterCapability(
			IAgentToolCapability<GetWorkItemTreeCapabilityInput> treeCapability,
			IModelSelector modelSelector,
			ILogger<GenerateNewsletterCapability> logger)
		{
			_treeCapability = treeCapability ?? throw new ArgumentNullException(nameof(treeCapability));
			_modelSelector = modelSelector ?? throw new ArgumentNullException(nameof(modelSelector));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <inheritdoc />
		public string Name => "GenerateNewsletter";

		/// <inheritdoc />
		public string Description => "Generate an executive newsletter from an Azure DevOps Feature or Epic, including status, progress, timelines, risks, and milestones.";

		/// <inheritdoc />
		public async Task<string> ExecuteAsync(GenerateNewsletterCapabilityInput input)
		{
			_logger.LogInformation(
				"Starting newsletter generation for work item {WorkItemId} with format '{Format}'",
				input.WorkItemId,
				input.Format ?? "Executive");

			// PHASE 1: Data Retrieval (~1 second)
			// Fetch the work item hierarchy using the optimized tree capability
			var treeInput = new GetWorkItemTreeCapabilityInput
			{
				WorkItemId = input.WorkItemId,
				Depth = input.Depth > 0 ? input.Depth : 3
			};

			var hierarchyJson = await _treeCapability.ExecuteAsync(treeInput);

			_logger.LogDebug(
				"Retrieved work item hierarchy for {WorkItemId}, length: {Length} characters",
				input.WorkItemId,
				hierarchyJson?.Length ?? 0);

			// PHASE 2: Newsletter Synthesis (~8-12 seconds with Advanced model)
			// Use the model configured for newsletter generation (typically gpt-4o for quality)
			var chatClient = _modelSelector.GetChatClientForOperation("NewsletterGeneration");
			var modelName = _modelSelector.GetModelForOperation("NewsletterGeneration");

			_logger.LogInformation(
				"Synthesizing newsletter for work item {WorkItemId} using model '{Model}'",
				input.WorkItemId,
				modelName);

			// Build the synthesis prompt
			var synthesisPrompt = BuildNewsletterPrompt(hierarchyJson, input.Format);

			var messages = new List<ChatMessage>
			{
				new SystemChatMessage(GetSystemPrompt()),
				new UserChatMessage(synthesisPrompt)
			};

			var options = new ChatCompletionOptions
			{
				Temperature = 0.7f, // Balanced creativity for professional writing
				MaxOutputTokenCount = 4000 // Allow comprehensive newsletters
			};

			var response = await chatClient.CompleteChatAsync(messages, options);
			var newsletter = response.Value.Content[0].Text;

			_logger.LogInformation(
				"Newsletter generation complete for work item {WorkItemId}, length: {Length} characters",
				input.WorkItemId,
				newsletter?.Length ?? 0);

			// Return the newsletter wrapped in JSON for consistency with other capabilities
			return JsonSerializer.Serialize(new
			{
				workItemId = input.WorkItemId,
				format = input.Format ?? "Executive",
				newsletter = newsletter,
				generatedAt = DateTimeOffset.UtcNow
			});
		}

		/// <summary>
		/// Builds the system prompt that defines the newsletter synthesis behavior.
		/// </summary>
		private static string GetSystemPrompt()
		{
			return @"You are an executive communications assistant specializing in creating clear, concise project status newsletters.

Your task is to analyze Azure DevOps work item hierarchies and synthesize them into professional executive-friendly status updates.

Focus on:
- High-level progress and outcomes
- Key milestones and timelines
- Risks and mitigation strategies
- Actionable insights for stakeholders

Use professional language appropriate for senior leadership audiences.";
		}

		/// <summary>
		/// Builds the user prompt with the work item hierarchy data and formatting instructions.
		/// </summary>
		private static string BuildNewsletterPrompt(string hierarchyJson, string? format)
		{
			var formatInstructions = format?.ToLowerInvariant() switch
			{
				"brief" => "Keep the newsletter concise (2-3 paragraphs maximum). Focus only on the most critical information.",
				"technical" => "Include technical details such as implementation approaches, dependencies, and technical risks. Target audience: engineering leadership.",
				_ => "Use a balanced executive format suitable for senior leadership." // Default: Executive
			};

			return $@"Generate an executive newsletter from the following Azure DevOps work item hierarchy:

{hierarchyJson}

Format Instructions: {formatInstructions}

Your newsletter should include:

1. **Overview** (2-3 sentences)
   - Project scope and current status
   - High-level progress summary

2. **Execution Timelines & Milestones** (grouped by weekly iteration)
   - **CRITICAL**: Use System.IterationPath from CHILD work items ONLY (Features, User Stories, Tasks)
   - **IGNORE** the root Epic/Feature iteration path (which is typically a monthly/quarterly rollup for overall project tracking)
   - Group milestones by weekly iteration paths extracted from child work items
   - For each weekly iteration, show:
     * Iteration name (extract the week identifier, e.g., ""Week 33"" from path like ""OneCRM\FY26\Q3\1Wk\1Wk33"")
     * Date range if available from work item dates
     * Associated work items (Features, User Stories, Tasks) with their:
       - Work item ID and Title
       - Work item Type (Feature/User Story/Task)
       - State (New/Active/Resolved/Closed)
       - StartDate, TargetDate, FinishDate (if available)
     * Completion status: count of completed vs total work items
     * Progress percentage for the iteration
   - Sort iterations chronologically (earliest week first)
   - Format as clear tables or structured lists for executive readability
   - Highlight the current week's iteration first if identifiable by dates

3. **Outcomes Since Last Newsletter** (bullet points)
   - Completed work items and their business impact
   - Delivered features or capabilities

4. **Current Sprint Progress**
   - Active work items and their status
   - Completion percentage for current milestone
   - Next steps and upcoming deliverables

5. **Risks & Mitigation Plan** (table format)
   - Identified risks from RiskAssessmentComment fields
   - Impact assessment (High/Medium/Low)
   - Mitigation strategies and owners

6. **Recommendations** (if applicable)
   - Suggested actions for stakeholders
   - Areas requiring attention or decisions

Format the newsletter professionally with clear section headers and concise language. Use markdown formatting for readability.

Include work item IDs as citations where relevant (e.g., ""Feature 12345 is 80% complete"").

Do not mention missing data - focus on what is available. If timeline information is incomplete, focus on status and risks instead.";
		}
	}
}
