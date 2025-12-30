using System.Threading;
using System.Threading.Tasks;
using Hermes.Channels.Teams;
using Hermes.Orchestrator;
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

            // Act
            var agent = new HermesTeamsAgent(options, orchestratorMock.Object);

            // Assert
            Assert.NotNull(agent);
        }

        [Fact]
        public async Task WelcomeMessageAsync_SendsWelcomeToNonBotMembers()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var agent = new HermesTeamsAgent(options, orchestratorMock.Object);

            var botAccount = new ChannelAccount(id: "bot-id");
            var userAccount = new ChannelAccount(id: "user-id");

            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                Recipient = botAccount,
                MembersAdded = [ userAccount, botAccount ]
            };

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.SetupGet(c => c.Activity).Returns(activity);

            var turnStateMock = new Mock<ITurnState>();

            Activity? sentActivity = null;
            turnContextMock
                .Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((a, _) => sentActivity = (Activity)a)
                .ReturnsAsync(new ResourceResponse());

            // Use reflection to invoke the private WelcomeMessageAsync method
            var method = typeof(HermesTeamsAgent)
                .GetMethod("WelcomeMessageAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            await (Task)method!.Invoke(
                agent,
                new object?[] { turnContextMock.Object, turnStateMock.Object, CancellationToken.None }
            )!;

            // Assert
            Assert.NotNull(sentActivity);
            Assert.Equal(ActivityTypes.Message, sentActivity!.Type);
            Assert.Equal("Hello and Welcome!", sentActivity.Text);
        }

        [Fact]
        public async Task OnMessageAsync_SendsEchoForMessageActivity()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object);

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

            // Use reflection to invoke the private OnMessageAsync method
            var method = typeof(HermesTeamsAgent)
                .GetMethod("OnMessageAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            await (Task)method!.Invoke(
                agent,
                new object?[] { turnContextMock.Object, turnStateMock.Object, CancellationToken.None }
            )!;

            // Assert
            Assert.NotNull(sentActivity);
            Assert.Equal(ActivityTypes.Message, sentActivity!.Type);
            Assert.Equal("You said: hello from teams", sentActivity.Text);
        }
    }
}
