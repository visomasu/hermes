using Hermes.Orchestrator;
using Hermes.Storage.Repositories.ConversationHistory;
using Hermes.Storage.Repositories.HermesInstructions;
using Hermes.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace Hermes.Tests.Orchestrator
{
	public class HermesOrchestratorTests
	{
		[Fact]
		public void CanConstructHermesOrchestrator_WithTools()
		{
			// Arrange
			var tools = new List<IAgentTool> {
				new Mock<IAgentTool>().Object
			};
			var agentMock = new Mock<AIAgent>();
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();

			// Act
			var orchestrator = new HermesOrchestrator(
				agentMock.Object, 
				"https://test.openai.azure.com/", 
				"test-api-key", 
				instructionsRepoMock.Object, 
				tools,
				new Mock<IConversationHistoryRepository>().Object);

			// Assert
			Assert.NotNull(orchestrator);
		}

		[Fact]
		public async Task OrchestrateAsync_ReturnsResponse()
		{
			// Arrange
			var tools = new List<IAgentTool>();
			var agentMock = new Mock<AIAgent>();
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			var historyRepoMock = new Mock<IConversationHistoryRepository>();

			var chatResponseMock = new ChatResponse(new ChatMessage(ChatRole.Assistant, "The status is in-progress."));
			var agentResponseMock = new AgentRunResponse(chatResponseMock);

			agentMock.Setup(a => a.RunAsync(
				It.IsAny<IEnumerable<ChatMessage>>(),
				It.IsAny<AgentThread>(),
				It.IsAny<AgentRunOptions>(),
				It.IsAny<CancellationToken>()
			)).ReturnsAsync(agentResponseMock);

			var orchestrator = new HermesOrchestrator(
				agentMock.Object,
				"https://test.openai.azure.com/",
				"test-api-key",
				instructionsRepoMock.Object,
				tools,
				historyRepoMock.Object);

			// Act
			var response = await orchestrator.OrchestrateAsync("session-1", "What is the status of feature123?");

			// Assert
			Assert.NotNull(response);
			Assert.Equal("The status is in-progress.", response);
			historyRepoMock.Verify(h => h.WriteConversationHistoryAsync(
				"session-1",
				It.Is<List<ConversationMessage>>(m => m.Count == 2 && m[0].Role == "user" && m[1].Role == "assistant"),
				It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task OrchestrateAsync_WritesHistoryWithUserAndAssistantMessages()
		{
			// Arrange
			var tools = new List<IAgentTool>();
			var agentMock = new Mock<AIAgent>();
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			var historyRepoMock = new Mock<IConversationHistoryRepository>();

			var chatResponseMock = new ChatResponse(new ChatMessage(ChatRole.Assistant, "History test response."));
			var agentResponseMock = new AgentRunResponse(chatResponseMock);

			agentMock.Setup(a => a.RunAsync(
				It.IsAny<IEnumerable<ChatMessage>>(),
				It.IsAny<AgentThread>(),
				It.IsAny<AgentRunOptions>(),
				It.IsAny<CancellationToken>()
			)).ReturnsAsync(agentResponseMock);

			var orchestrator = new HermesOrchestrator(
				agentMock.Object,
				"https://test.openai.azure.com/",
				"test-api-key",
				instructionsRepoMock.Object,
				tools,
				historyRepoMock.Object);

			var sessionId = "history-session";
			var query = "History test query";

			// Act
			await orchestrator.OrchestrateAsync(sessionId, query);

			// Assert
			historyRepoMock.Verify(h => h.WriteConversationHistoryAsync(
				sessionId,
				It.Is<List<ConversationMessage>>(m =>
					m.Count == 2 &&
					m[0].Role == "user" && m[0].Content == query &&
					m[1].Role == "assistant" && m[1].Content == "History test response."),
				It.IsAny<CancellationToken>()),
				Times.Once);
		}
	}
}
