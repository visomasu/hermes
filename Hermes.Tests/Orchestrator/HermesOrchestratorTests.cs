using Hermes.Orchestrator;
using Hermes.Orchestrator.Context;
using Hermes.Orchestrator.PhraseGen;
using Hermes.Orchestrator.Prompts;
using Hermes.Storage.Repositories.ConversationHistory;
using Hermes.Storage.Repositories.HermesInstructions;
using Hermes.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
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
			var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
			var contextSelectorMock = new Mock<IConversationContextSelector>();

			// Return a basic instruction so the orchestrator can resolve instructions if needed
			instructionsRepoMock
				.Setup(r => r.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant, null))
				.ReturnsAsync(new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions(
					"Test instructions",
					HermesInstructionType.ProjectAssistant,
					1));

			// Act - use the test-only constructor that accepts an AIAgent
			var orchestrator = new HermesOrchestrator(
				new Mock<ILogger<HermesOrchestrator>>().Object,
				agentMock.Object,
				"https://test.openai.azure.com/",
				"test-api-key",
				instructionsRepoMock.Object,
				tools,
				new Mock<IConversationHistoryRepository>().Object,
				new Mock<IAgentPromptComposer>().Object,
				phraseGeneratorMock.Object,
				contextSelectorMock.Object);

			// Assert
			Assert.NotNull(orchestrator);
		}

		[Fact]
		public async Task OrchestrateAsync_ReturnsResponse_AndPassesMessagesAndThreadToAgent()
		{
			// Arrange
			var tools = new List<IAgentTool>();
			var agentMock = new Mock<AIAgent>();
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			var historyRepoMock = new Mock<IConversationHistoryRepository>();
			var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();

			instructionsRepoMock
				.Setup(r => r.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant, null))
				.ReturnsAsync(new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions(
					"Test instructions",
					HermesInstructionType.ProjectAssistant,
					1));

			// No prior history for this session
			historyRepoMock
				.Setup(h => h.GetConversationHistoryAsync("session-1", It.IsAny<CancellationToken>()))
				.ReturnsAsync((string?)null);

			var chatResponseMock = new ChatResponse(new ChatMessage(ChatRole.Assistant, "The status is in-progress."));
			var agentResponseMock = new AgentRunResponse(chatResponseMock);

			var threadMock = new Mock<AgentThread>();
			agentMock
				.Setup(a => a.GetNewThread())
				.Returns(threadMock.Object);

			IEnumerable<ChatMessage>? capturedMessages = null;
			AgentThread? capturedThread = null;

			agentMock
				.Setup(a => a.RunAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<AgentThread>(), It.IsAny<AgentRunOptions>(), It.IsAny<CancellationToken>()))
				.Callback<IEnumerable<ChatMessage>, AgentThread, AgentRunOptions?, CancellationToken>((msgs, thread, _, _) =>
				{
					capturedMessages = msgs;
					capturedThread = thread;
				})
				.ReturnsAsync(agentResponseMock);

			var contextSelectorMock = new Mock<IConversationContextSelector>();
			contextSelectorMock
				.Setup(c => c.SelectRelevantContextAsync(It.IsAny<string>(), It.IsAny<List<ConversationMessage>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<ConversationMessage>());

			var orchestrator = new HermesOrchestrator(
				new Mock<ILogger<HermesOrchestrator>>().Object,
				agentMock.Object,
				"https://test.openai.azure.com/",
				"test-api-key",
				instructionsRepoMock.Object,
				tools,
				historyRepoMock.Object,
				new Mock<IAgentPromptComposer>().Object,
				phraseGeneratorMock.Object,
				contextSelectorMock.Object);

			// Act
			var response = await orchestrator.OrchestrateAsync("session-1", "What is the status of feature123?");

			// Assert
			Assert.Equal("The status is in-progress.", response);
			Assert.NotNull(capturedMessages);
			var messagesList = capturedMessages!.ToList();
			Assert.Single(messagesList);
			Assert.Equal(ChatRole.User, messagesList[0].Role);
			Assert.Equal("What is the status of feature123?", messagesList[0].Text);

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
			var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();

			instructionsRepoMock
				.Setup(r => r.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant, null))
				.ReturnsAsync(new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions(
					"Test instructions",
					HermesInstructionType.ProjectAssistant,
					1));

			// No prior history
			historyRepoMock
				.Setup(h => h.GetConversationHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((string?)null);

			var chatResponseMock = new ChatResponse(new ChatMessage(ChatRole.Assistant, "History test response."));
			var agentResponseMock = new AgentRunResponse(chatResponseMock);

			var threadMock = new Mock<AgentThread>();
			agentMock
				.Setup(a => a.GetNewThread())
				.Returns(threadMock.Object);

			agentMock
				.Setup(a => a.RunAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<AgentThread>(), It.IsAny<AgentRunOptions>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(agentResponseMock);

			var contextSelectorMock = new Mock<IConversationContextSelector>();
			contextSelectorMock
				.Setup(c => c.SelectRelevantContextAsync(It.IsAny<string>(), It.IsAny<List<ConversationMessage>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<ConversationMessage>());

			var orchestrator = new HermesOrchestrator(
				new Mock<ILogger<HermesOrchestrator>>().Object,
				agentMock.Object,
				"https://test.openai.azure.com/",
				"test-api-key",
				instructionsRepoMock.Object,
				tools,
				historyRepoMock.Object,
				new Mock<IAgentPromptComposer>().Object,
				phraseGeneratorMock.Object,
				contextSelectorMock.Object);

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

		[Fact]
		public async Task OrchestrateAsync_UsesConversationHistoryForContextWindow()
		{
			// Arrange
			var tools = new List<IAgentTool>();
			var agentMock = new Mock<AIAgent>();
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			var historyRepoMock = new Mock<IConversationHistoryRepository>();
			var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();

			instructionsRepoMock
				.Setup(r => r.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant, null))
				.ReturnsAsync(new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions(
					"Test instructions",
					HermesInstructionType.ProjectAssistant,
					1));

			var existingHistory = new List<ConversationMessage>
			{
				new ConversationMessage { Role = "user", Content = "Old question", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10) },
				new ConversationMessage { Role = "assistant", Content = "Old answer", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-9) }
			};

			var historyJson = JsonSerializer.Serialize(existingHistory);

			historyRepoMock
				.Setup(h => h.GetConversationHistoryAsync("session-with-history", It.IsAny<CancellationToken>()))
				.ReturnsAsync(historyJson);

			var chatResponseMock = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Response with context."));
			var agentResponseMock = new AgentRunResponse(chatResponseMock);

			var threadMock = new Mock<AgentThread>();
			agentMock
				.Setup(a => a.GetNewThread())
				.Returns(threadMock.Object);

			IEnumerable<ChatMessage>? capturedMessages = null;

			agentMock
				.Setup(a => a.RunAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<AgentThread>(), It.IsAny<AgentRunOptions>(), It.IsAny<CancellationToken>()))
				.Callback<IEnumerable<ChatMessage>, AgentThread, AgentRunOptions?, CancellationToken>((msgs, _, _, _) =>
				{
					capturedMessages = msgs;
				})
				.ReturnsAsync(agentResponseMock);

			var contextSelectorMock = new Mock<IConversationContextSelector>();
			// Return the existing history from context selector
			contextSelectorMock
				.Setup(c => c.SelectRelevantContextAsync(It.IsAny<string>(), It.IsAny<List<ConversationMessage>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(existingHistory);

			var orchestrator = new HermesOrchestrator(
				new Mock<ILogger<HermesOrchestrator>>().Object,
				agentMock.Object,
				"https://test.openai.azure.com/",
				"test-api-key",
				instructionsRepoMock.Object,
				tools,
				historyRepoMock.Object,
				new Mock<IAgentPromptComposer>().Object,
				phraseGeneratorMock.Object,
				contextSelectorMock.Object);

			// Act
			await orchestrator.OrchestrateAsync("session-with-history", "New question");

			// Assert
			Assert.NotNull(capturedMessages);
			var msgsList = capturedMessages!.ToList();
			// Existing two messages from history + current user message
			Assert.Equal(3, msgsList.Count);
			Assert.Equal("Old question", msgsList[0].Text);
			Assert.Equal("Old answer", msgsList[1].Text);
			Assert.Equal("New question", msgsList[2].Text);
		}

		[Fact]
		public async Task OrchestrateAsync_WithProgressCallback_InvokesCallbackWithPhrase()
		{
			// Arrange
			var tools = new List<IAgentTool>();
			var agentMock = new Mock<AIAgent>();
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			var historyRepoMock = new Mock<IConversationHistoryRepository>();
			var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();

			phraseGeneratorMock
				.Setup(p => p.GeneratePhrase())
				.Returns("brilliant-dancing-thought");

			instructionsRepoMock
				.Setup(r => r.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant, null))
				.ReturnsAsync(new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions(
					"Test instructions",
					HermesInstructionType.ProjectAssistant,
					1));

			historyRepoMock
				.Setup(h => h.GetConversationHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((string?)null);

			var chatResponseMock = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
			var agentResponseMock = new AgentRunResponse(chatResponseMock);

			var threadMock = new Mock<AgentThread>();
			agentMock
				.Setup(a => a.GetNewThread())
				.Returns(threadMock.Object);

			agentMock
				.Setup(a => a.RunAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<AgentThread>(), It.IsAny<AgentRunOptions>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(agentResponseMock);

			var contextSelectorMock = new Mock<IConversationContextSelector>();
			contextSelectorMock
				.Setup(c => c.SelectRelevantContextAsync(It.IsAny<string>(), It.IsAny<List<ConversationMessage>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<ConversationMessage>());

			var orchestrator = new HermesOrchestrator(
				new Mock<ILogger<HermesOrchestrator>>().Object,
				agentMock.Object,
				"https://test.openai.azure.com/",
				"test-api-key",
				instructionsRepoMock.Object,
				tools,
				historyRepoMock.Object,
				new Mock<IAgentPromptComposer>().Object,
				phraseGeneratorMock.Object,
				contextSelectorMock.Object);

			string? capturedPhrase = null;
			Action<string> progressCallback = phrase => capturedPhrase = phrase;

			// Act
			await orchestrator.OrchestrateAsync("test-session", "Test query", progressCallback);

			// Assert
			Assert.NotNull(capturedPhrase);
			Assert.Equal("brilliant-dancing-thought", capturedPhrase);
			phraseGeneratorMock.Verify(p => p.GeneratePhrase(), Times.Once);
		}

		[Fact]
		public async Task OrchestrateAsync_WithoutProgressCallback_DoesNotInvokeCallback()
		{
			// Arrange
			var tools = new List<IAgentTool>();
			var agentMock = new Mock<AIAgent>();
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			var historyRepoMock = new Mock<IConversationHistoryRepository>();
			var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();

			instructionsRepoMock
				.Setup(r => r.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant, null))
				.ReturnsAsync(new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions(
					"Test instructions",
					HermesInstructionType.ProjectAssistant,
					1));

			historyRepoMock
				.Setup(h => h.GetConversationHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((string?)null);

			var chatResponseMock = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
			var agentResponseMock = new AgentRunResponse(chatResponseMock);

			var threadMock = new Mock<AgentThread>();
			agentMock
				.Setup(a => a.GetNewThread())
				.Returns(threadMock.Object);

			agentMock
				.Setup(a => a.RunAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<AgentThread>(), It.IsAny<AgentRunOptions>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(agentResponseMock);

			var contextSelectorMock = new Mock<IConversationContextSelector>();
			contextSelectorMock
				.Setup(c => c.SelectRelevantContextAsync(It.IsAny<string>(), It.IsAny<List<ConversationMessage>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<ConversationMessage>());

			var orchestrator = new HermesOrchestrator(
				new Mock<ILogger<HermesOrchestrator>>().Object,
				agentMock.Object,
				"https://test.openai.azure.com/",
				"test-api-key",
				instructionsRepoMock.Object,
				tools,
				historyRepoMock.Object,
				new Mock<IAgentPromptComposer>().Object,
				phraseGeneratorMock.Object,
				contextSelectorMock.Object);

			// Act - call with null callback (tests backward compatibility)
			var response = await orchestrator.OrchestrateAsync("test-session", "Test query", null);

			// Assert
			Assert.Equal("Test response.", response);
			phraseGeneratorMock.Verify(p => p.GeneratePhrase(), Times.Never);
		}

		[Fact]
		public async Task OrchestrateAsync_BackwardCompatibleMethod_CallsNewOverload()
		{
			// Arrange
			var tools = new List<IAgentTool>();
			var agentMock = new Mock<AIAgent>();
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			var historyRepoMock = new Mock<IConversationHistoryRepository>();
			var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();

			instructionsRepoMock
				.Setup(r => r.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant, null))
				.ReturnsAsync(new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions(
					"Test instructions",
					HermesInstructionType.ProjectAssistant,
					1));

			historyRepoMock
				.Setup(h => h.GetConversationHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((string?)null);

			var chatResponseMock = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Backward compat response."));
			var agentResponseMock = new AgentRunResponse(chatResponseMock);

			var threadMock = new Mock<AgentThread>();
			agentMock
				.Setup(a => a.GetNewThread())
				.Returns(threadMock.Object);

			agentMock
				.Setup(a => a.RunAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<AgentThread>(), It.IsAny<AgentRunOptions>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(agentResponseMock);

			var contextSelectorMock = new Mock<IConversationContextSelector>();
			contextSelectorMock
				.Setup(c => c.SelectRelevantContextAsync(It.IsAny<string>(), It.IsAny<List<ConversationMessage>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<ConversationMessage>());

			var orchestrator = new HermesOrchestrator(
				new Mock<ILogger<HermesOrchestrator>>().Object,
				agentMock.Object,
				"https://test.openai.azure.com/",
				"test-api-key",
				instructionsRepoMock.Object,
				tools,
				historyRepoMock.Object,
				new Mock<IAgentPromptComposer>().Object,
				phraseGeneratorMock.Object,
				contextSelectorMock.Object);

			// Act - call the old method without progress callback
			var response = await orchestrator.OrchestrateAsync("test-session", "Test query");

			// Assert - should still work and return response
			Assert.Equal("Backward compat response.", response);
			// Since we didn't provide a callback, phrase generator should never be called
			phraseGeneratorMock.Verify(p => p.GeneratePhrase(), Times.Never);
		}
	}
}
