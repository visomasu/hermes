using Hermes.Orchestrator;
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

			// Act
			var orchestrator = new HermesOrchestrator(agentMock.Object, tools);

			// Assert
			Assert.NotNull(orchestrator);
		}

		[Fact]
		public async Task OrchestrateAsync_ReturnsResponse()
		{
			// Arrange
			var tools = new List<IAgentTool>();
			var agentMock = new Mock<AIAgent>();

			var chatResponseMock = new ChatResponse(new ChatMessage(ChatRole.Assistant, "The status is in-progress."));
			var agentResponseMock = new AgentRunResponse(chatResponseMock);

			agentMock.Setup(a => a.RunAsync(
				It.IsAny<IEnumerable<ChatMessage>>(),
				It.IsAny<AgentThread>(),
				It.IsAny<AgentRunOptions>(),
				It.IsAny<CancellationToken>()
			)).ReturnsAsync(agentResponseMock);

			var orchestrator = new HermesOrchestrator(agentMock.Object, tools);

			// Act
			var response = await orchestrator.OrchestrateAsync("What is the status of feature123?");

			// Assert
			Assert.NotNull(response);
			Assert.Equal("The status is in-progress.", response);
		}
	}
}
