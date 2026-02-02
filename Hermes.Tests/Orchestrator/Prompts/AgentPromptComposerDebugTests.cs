using Xunit;
using Hermes.Orchestrator.Prompts;
using Hermes.Storage.Repositories.HermesInstructions;

namespace Hermes.Tests.Orchestrator.Prompts;

/// <summary>
/// Debug tests to verify prompt composition includes necessary JSON format instructions.
/// </summary>
public class AgentPromptComposerDebugTests
{
    [Fact]
    public void ComposePrompt_IncludesSlaJsonInstructions()
    {
        // Arrange
        var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var hermesPath = Path.Combine(basePath, "Hermes");
        var composer = new AgentPromptComposer(hermesPath);

        // Act
        var prompt = composer.ComposePrompt(HermesInstructionType.ProjectAssistant);

        // Assert and Debug Output
        Console.WriteLine("=== PROMPT COMPOSITION DEBUG ===");
        Console.WriteLine($"Base Path: {hermesPath}");
        Console.WriteLine($"Prompt Length: {prompt.Length} characters");
        Console.WriteLine();

        // Check for SLA-specific instructions
        var hasJsonInstructions = prompt.Contains("YOUR TOOL CALL MUST LOOK LIKE THIS");
        var hasRegisterCapability = prompt.Contains("RegisterSlaNotifications");
        var hasTeamsUserIdParam = prompt.Contains("teamsUserId");

        Console.WriteLine($"✓ Contains 'YOUR TOOL CALL MUST LOOK LIKE THIS': {hasJsonInstructions}");
        Console.WriteLine($"✓ Contains 'RegisterSlaNotifications': {hasRegisterCapability}");
        Console.WriteLine($"✓ Contains 'teamsUserId': {hasTeamsUserIdParam}");
        Console.WriteLine();

        // Print relevant sections
        var lines = prompt.Split('\n');
        Console.WriteLine("=== RELEVANT PROMPT SECTIONS ===");
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("RegisterSlaNotifications") ||
                lines[i].Contains("YOUR TOOL CALL") ||
                lines[i].Contains("teamsUserId") ||
                (i > 0 && lines[i - 1].Contains("YOUR TOOL CALL")))
            {
                Console.WriteLine($"Line {i + 1}: {lines[i].Trim()}");
            }
        }

        // Assertions
        Assert.True(hasJsonInstructions, "Prompt should include JSON format instructions");
        Assert.True(hasRegisterCapability, "Prompt should mention RegisterSlaNotifications capability");
        Assert.True(hasTeamsUserIdParam, "Prompt should mention teamsUserId parameter");
    }
}
