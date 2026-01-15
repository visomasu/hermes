using System.Threading;
using System.Threading.Tasks;
using Hermes.Channels.Teams;
using Hermes.Orchestrator;
using Hermes.Orchestrator.PhraseGen;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using Xunit;

namespace Hermes.Tests.Channels.Teams
{
    public class HermesTeamsAgentTests
    {
        [Fact]
        public void CanConstructHermesTeamsAgent()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();

            // Act
            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object);

            // Assert
            Assert.NotNull(agent);
        }

        [Fact]
        public async Task WelcomeMessageAsync_UsesOrchestratorAndFallsBackWhenEmpty()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();

            // First call returns empty, so we assert the fallback text is used
            orchestratorMock
                .Setup(o => o.OrchestrateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(string.Empty);

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object);

            var botAccount = new ChannelAccount(id: "bot-id");
            var userAccount = new ChannelAccount(id: "user-id");

            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                Recipient = botAccount,
                MembersAdded = [ userAccount, botAccount ],
                Conversation = new ConversationAccount(id: "conv-id")
            };

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.SetupGet(c => c.Activity).Returns(activity);

            var turnStateMock = new Mock<ITurnState>();

            Activity? sentActivity = null;
            turnContextMock
                .Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((a, _) => sentActivity = (Activity)a)
                .ReturnsAsync(new ResourceResponse());

            var method = typeof(HermesTeamsAgent)
                .GetMethod("WelcomeMessageAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            await (Task)method!.Invoke(
                agent,
                new object?[] { turnContextMock.Object, turnStateMock.Object, CancellationToken.None }
            )!;

            // Assert
            orchestratorMock.Verify(o => o.OrchestrateAsync("conv-id", It.IsAny<string>()), Times.Once);
            Assert.NotNull(sentActivity);
            Assert.Equal(ActivityTypes.Message, sentActivity!.Type);
            Assert.Contains("Hello and welcome! I am Hermes.", sentActivity.Text);
        }

        [Fact]
        public async Task OnMessageAsync_UsesOrchestratorResponse()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();

            phraseGeneratorMock
                .Setup(p => p.GeneratePhrase())
                .Returns("brilliant-dancing-thought");

            orchestratorMock
                .Setup(o => o.OrchestrateAsync("conv-id", "hello from teams", It.IsAny<Action<string>?>()))
                .ReturnsAsync("orchestrated response");

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object);

            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello from teams",
                ChannelId = "msteams",
                From = new ChannelAccount(id: "user-id"),
                Recipient = new ChannelAccount(id: "bot-id"),
                Conversation = new ConversationAccount(id: "conv-id")
            };

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.SetupGet(c => c.Activity).Returns(activity);

            var turnStateMock = new Mock<ITurnState>();

            Activity? sentActivity = null;
            turnContextMock
                .Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((a, _) => sentActivity = (Activity)a)
                .ReturnsAsync(new ResourceResponse());

            var method = typeof(HermesTeamsAgent)
                .GetMethod("OnMessageAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            await (Task)method!.Invoke(
                agent,
                new object?[] { turnContextMock.Object, turnStateMock.Object, CancellationToken.None }
            )!;

            // Assert
            orchestratorMock.Verify(o => o.OrchestrateAsync("conv-id", "hello from teams", It.IsAny<Action<string>?>()), Times.Once);
            Assert.NotNull(sentActivity);
            Assert.Equal(ActivityTypes.Message, sentActivity!.Type);
            Assert.Equal("orchestrated response", sentActivity.Text);
        }

        [Fact]
        public async Task OnMessageAsync_SendsTypingIndicatorsDuringOrchestration()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var sentActivities = new List<IActivity>();

            phraseGeneratorMock
                .Setup(p => p.GeneratePhrase())
                .Returns("splendid-soaring-sketch");

            // Simulate orchestration taking some time
            orchestratorMock
                .Setup(o => o.OrchestrateAsync("conv-id", "test message", It.IsAny<Action<string>?>()))
                .Returns(async () =>
                {
                    await Task.Delay(5000); // 5 seconds - should trigger ~2 typing indicators
                    return "orchestrated response";
                });

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object);

            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test message",
                ChannelId = "msteams",
                From = new ChannelAccount(id: "user-id"),
                Recipient = new ChannelAccount(id: "bot-id"),
                Conversation = new ConversationAccount(id: "conv-id")
            };

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.SetupGet(c => c.Activity).Returns(activity);

            turnContextMock
                .Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((a, _) => sentActivities.Add(a))
                .ReturnsAsync(new ResourceResponse());

            var turnStateMock = new Mock<ITurnState>();

            var method = typeof(HermesTeamsAgent)
                .GetMethod("OnMessageAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            await (Task)method!.Invoke(
                agent,
                new object?[] { turnContextMock.Object, turnStateMock.Object, CancellationToken.None }
            )!;

            // Assert
            // Should have sent at least 1 typing indicator + 1 final message
            Assert.True(sentActivities.Count >= 2, $"Expected at least 2 activities (typing + message), got {sentActivities.Count}");

            // Check that some activities were typing indicators
            var typingActivities = sentActivities.Where(a => a.Type == ActivityTypes.Typing).ToList();
            Assert.True(typingActivities.Count >= 1, $"Expected at least 1 typing indicator, got {typingActivities.Count}");

            // Check that the final message was sent
            var messageActivity = sentActivities.Last(a => a.Type == ActivityTypes.Message);
            Assert.Equal("orchestrated response", ((Activity)messageActivity).Text);

            // Verify phrase generator was called
            phraseGeneratorMock.Verify(p => p.GeneratePhrase(), Times.Once);
        }
    }
}
