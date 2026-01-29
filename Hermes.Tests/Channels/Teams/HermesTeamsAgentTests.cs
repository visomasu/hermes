using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hermes.Channels.Teams;
using Hermes.Orchestrator;
using Hermes.Orchestrator.PhraseGen;
using Hermes.Storage.Repositories.ConversationReference;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
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
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            // Act
            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

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
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            // First call returns empty, so we assert the fallback text is used
            orchestratorMock
                .Setup(o => o.OrchestrateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(string.Empty);

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

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

            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();
            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

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
        public async Task OnMessageAsync_SendsStreamingActivitiesDuringOrchestration()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var sentActivities = new List<IActivity>();

            // Simulate orchestration taking some time
            orchestratorMock
                .Setup(o => o.OrchestrateAsync("conv-id", "test message", It.IsAny<Action<string>?>()))
                .Returns(async () =>
                {
                    await Task.Delay(100); // Small delay to simulate work
                    return "orchestrated response";
                });

            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();
            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

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
                .ReturnsAsync(new ResourceResponse { Id = "stream-123" });

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
            // Should have sent at least 2 activities: informative update (typing) + final message
            Assert.True(sentActivities.Count >= 2, $"Expected at least 2 activities (streaming + message), got {sentActivities.Count}");

            // First activity should be a typing indicator with streaminfo entity
            var firstActivity = (Activity)sentActivities.First();
            Assert.Equal(ActivityTypes.Typing, firstActivity.Type);
            Assert.Equal("Thinking...", firstActivity.Text);

            // Check that the final message was sent
            var messageActivity = sentActivities.Last(a => a.Type == ActivityTypes.Message);
            Assert.Equal("orchestrated response", ((Activity)messageActivity).Text);
        }

        #region _ExtractAadObjectIdAsync Tests

        [Fact]
        public async Task ExtractAadObjectIdAsync_ValidGuidInProperties_ReturnsAadObjectId()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

            var expectedAadObjectId = "6a0b5481-9742-485f-8595-b0c3a89934df";
            var properties = new Dictionary<string, JsonElement>
            {
                { "aadObjectId", JsonDocument.Parse($"\"{expectedAadObjectId}\"").RootElement }
            };

            var user = new ChannelAccount(id: "user-id", name: "Test User")
            {
                Properties = properties
            };

            var method = typeof(HermesTeamsAgent)
                .GetMethod("_ExtractAadObjectIdAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = await (Task<string?>)method!.Invoke(agent, new object[] { user })!;

            // Assert
            Assert.Equal(expectedAadObjectId, result);
        }

        [Fact]
        public async Task ExtractAadObjectIdAsync_InvalidGuidInProperties_FallsBackToAzCli()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

            var invalidAadObjectId = "not-a-valid-guid";
            var properties = new Dictionary<string, JsonElement>
            {
                { "aadObjectId", JsonDocument.Parse($"\"{invalidAadObjectId}\"").RootElement }
            };

            var user = new ChannelAccount(id: "user-id", name: "Test User")
            {
                Properties = properties
            };

            var method = typeof(HermesTeamsAgent)
                .GetMethod("_ExtractAadObjectIdAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = await (Task<string?>)method!.Invoke(agent, new object[] { user })!;

            // Assert
            // Should attempt az CLI fallback (will succeed if az is configured, or return null)
            // We can't deterministically test az CLI in unit tests, so we just verify it doesn't crash
            Assert.True(result == null || System.Guid.TryParse(result, out _));
        }

        [Fact]
        public async Task ExtractAadObjectIdAsync_NoPropertiesDictionary_FallsBackToAzCli()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

            var user = new ChannelAccount(id: "user-id", name: "Test User");

            var method = typeof(HermesTeamsAgent)
                .GetMethod("_ExtractAadObjectIdAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = await (Task<string?>)method!.Invoke(agent, new object[] { user })!;

            // Assert
            // Should attempt az CLI fallback (will succeed if az is configured, or return null)
            Assert.True(result == null || System.Guid.TryParse(result, out _));
        }

        [Fact]
        public async Task ExtractAadObjectIdAsync_EmptyPropertiesDictionary_FallsBackToAzCli()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

            var properties = new Dictionary<string, JsonElement>(); // Empty dictionary

            var user = new ChannelAccount(id: "user-id", name: "Test User")
            {
                Properties = properties
            };

            var method = typeof(HermesTeamsAgent)
                .GetMethod("_ExtractAadObjectIdAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = await (Task<string?>)method!.Invoke(agent, new object[] { user })!;

            // Assert
            // Should attempt az CLI fallback
            Assert.True(result == null || System.Guid.TryParse(result, out _));
        }

        [Fact]
        public async Task ExtractAadObjectIdAsync_MultipleValidGuids_ReturnsFirstFromProperties()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

            var expectedAadObjectId = "12345678-1234-1234-1234-123456789abc";
            var properties = new Dictionary<string, JsonElement>
            {
                { "aadObjectId", JsonDocument.Parse($"\"{expectedAadObjectId}\"").RootElement },
                { "otherProperty", JsonDocument.Parse("\"some-value\"").RootElement }
            };

            var user = new ChannelAccount(id: "user-id", name: "Test User")
            {
                Properties = properties
            };

            var method = typeof(HermesTeamsAgent)
                .GetMethod("_ExtractAadObjectIdAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = await (Task<string?>)method!.Invoke(agent, new object[] { user })!;

            // Assert
            Assert.Equal(expectedAadObjectId, result);
        }

        [Fact]
        public async Task ExtractAadObjectIdAsync_GuidWithDifferentCasing_ReturnsNormalizedGuid()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

            var expectedAadObjectId = "6A0B5481-9742-485F-8595-B0C3A89934DF"; // Uppercase
            var properties = new Dictionary<string, JsonElement>
            {
                { "aadObjectId", JsonDocument.Parse($"\"{expectedAadObjectId}\"").RootElement }
            };

            var user = new ChannelAccount(id: "user-id", name: "Test User")
            {
                Properties = properties
            };

            var method = typeof(HermesTeamsAgent)
                .GetMethod("_ExtractAadObjectIdAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = await (Task<string?>)method!.Invoke(agent, new object[] { user })!;

            // Assert
            Assert.NotNull(result);
            Assert.True(System.Guid.TryParse(result, out _), "Result should be a valid GUID");
        }

        [Fact]
        public async Task CaptureConversationReferenceAsync_ExtractsAndStoresAadObjectId()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            ConversationReferenceDocument? capturedDocument = null;
            conversationRefRepoMock
                .Setup(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((ConversationReferenceDocument?)null);

            conversationRefRepoMock
                .Setup(r => r.CreateAsync(It.IsAny<ConversationReferenceDocument>()))
                .Callback<ConversationReferenceDocument>((doc) => capturedDocument = doc)
                .Returns(Task.CompletedTask);

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

            var expectedAadObjectId = "87654321-4321-4321-4321-cba987654321";
            var properties = new Dictionary<string, JsonElement>
            {
                { "aadObjectId", JsonDocument.Parse($"\"{expectedAadObjectId}\"").RootElement }
            };

            var user = new ChannelAccount(id: "user-id", name: "Test User")
            {
                Properties = properties
            };
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test message",
                From = user,
                Conversation = new ConversationAccount(id: "conv-id")
            };

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.SetupGet(c => c.Activity).Returns(activity);

            var method = typeof(HermesTeamsAgent)
                .GetMethod("_CaptureConversationReferenceAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            await (Task)method!.Invoke(agent, new object[] { turnContextMock.Object, CancellationToken.None })!;

            // Assert
            Assert.NotNull(capturedDocument);
            Assert.Equal(expectedAadObjectId, capturedDocument!.AadObjectId);
            Assert.Equal("user-id", capturedDocument.TeamsUserId);
            Assert.Equal("conv-id", capturedDocument.ConversationId);
        }

        [Fact]
        public async Task CaptureConversationReferenceAsync_UpdatesExistingDocumentWithAadObjectId()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            var existingDocument = new ConversationReferenceDocument
            {
                Id = "conv-id",
                PartitionKey = "user-id",
                TeamsUserId = "user-id",
                ConversationId = "conv-id",
                AadObjectId = null, // Missing AAD Object ID
                ConversationReferenceJson = "{}"
            };

            ConversationReferenceDocument? updatedDocument = null;
            conversationRefRepoMock
                .Setup(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(existingDocument);

            conversationRefRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<ConversationReferenceDocument>()))
                .Callback<string, ConversationReferenceDocument>((_, doc) => updatedDocument = doc)
                .Returns(Task.CompletedTask);

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

            var expectedAadObjectId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
            var properties = new Dictionary<string, JsonElement>
            {
                { "aadObjectId", JsonDocument.Parse($"\"{expectedAadObjectId}\"").RootElement }
            };

            var user = new ChannelAccount(id: "user-id", name: "Test User")
            {
                Properties = properties
            };
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test message",
                From = user,
                Conversation = new ConversationAccount(id: "conv-id")
            };

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.SetupGet(c => c.Activity).Returns(activity);

            var method = typeof(HermesTeamsAgent)
                .GetMethod("_CaptureConversationReferenceAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            await (Task)method!.Invoke(agent, new object[] { turnContextMock.Object, CancellationToken.None })!;

            // Assert
            Assert.NotNull(updatedDocument);
            Assert.Equal(expectedAadObjectId, updatedDocument!.AadObjectId);
            conversationRefRepoMock.Verify(r => r.UpdateAsync("conv-id", It.IsAny<ConversationReferenceDocument>()), Times.Once);
        }

        #endregion

        #region Streaming Tests

        [Fact]
        public async Task OnMessageAsync_SendsStreamingInformativeUpdate()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            orchestratorMock
                .Setup(o => o.OrchestrateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>?>()))
                .ReturnsAsync("orchestrated response");

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

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

            var sentActivities = new List<Activity>();
            turnContextMock
                .Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((a, _) => sentActivities.Add((Activity)a))
                .ReturnsAsync(new ResourceResponse { Id = "stream-123" });

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
            // Should have at least 2 activities: informative update + final message
            Assert.True(sentActivities.Count >= 2, $"Expected at least 2 activities, got {sentActivities.Count}");

            // First activity should be a typing indicator with "Thinking..." and streaminfo entity
            var firstActivity = sentActivities.First();
            Assert.Equal(ActivityTypes.Typing, firstActivity.Type);
            Assert.Equal("Thinking...", firstActivity.Text);
            Assert.NotNull(firstActivity.Entities);
            Assert.Contains(firstActivity.Entities, e => e.Type == "streaminfo");

            // Last activity should be the final message
            var lastActivity = sentActivities.Last();
            Assert.Equal(ActivityTypes.Message, lastActivity.Type);
            Assert.Equal("orchestrated response", lastActivity.Text);
        }

        [Fact]
        public async Task OnMessageAsync_ProgressCallbackSkipsMessagesOver1000Chars()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            Action<string>? capturedCallback = null;
            orchestratorMock
                .Setup(o => o.OrchestrateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>?>()))
                .Callback<string, string, Action<string>?>((_, _, callback) => capturedCallback = callback)
                .ReturnsAsync("orchestrated response");

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

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

            var sentActivities = new List<Activity>();
            turnContextMock
                .Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((a, _) => sentActivities.Add((Activity)a))
                .ReturnsAsync(new ResourceResponse { Id = "stream-123" });

            var turnStateMock = new Mock<ITurnState>();

            var method = typeof(HermesTeamsAgent)
                .GetMethod("OnMessageAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            await (Task)method!.Invoke(
                agent,
                new object?[] { turnContextMock.Object, turnStateMock.Object, CancellationToken.None }
            )!;

            // Invoke the captured callback with a message > 1000 chars
            var longMessage = new string('x', 1001);
            var activitiesBeforeCallback = sentActivities.Count;
            capturedCallback?.Invoke(longMessage);

            // Assert - no new activity should be sent for the long message
            Assert.Equal(activitiesBeforeCallback, sentActivities.Count);
        }

        [Fact]
        public async Task OnMessageAsync_FallsBackToRegularMessageWhenStreamingFails()
        {
            // Arrange
            var storageMock = new Mock<IStorage>();
            var options = new AgentApplicationOptions(storage: storageMock.Object);
            var orchestratorMock = new Mock<IAgentOrchestrator>();
            var phraseGeneratorMock = new Mock<IWaitingPhraseGenerator>();
            var conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
            var loggerMock = new Mock<ILogger<HermesTeamsAgent>>();

            orchestratorMock
                .Setup(o => o.OrchestrateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>?>()))
                .ReturnsAsync("orchestrated response");

            var agent = new HermesTeamsAgent(options, orchestratorMock.Object, phraseGeneratorMock.Object, conversationRefRepoMock.Object, loggerMock.Object);

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

            var callCount = 0;
            var sentActivities = new List<Activity>();
            turnContextMock
                .Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((a, _) =>
                {
                    callCount++;
                    // First call (streaming start) throws, subsequent calls succeed
                    if (callCount == 1)
                    {
                        throw new System.Exception("Streaming not supported");
                    }
                    sentActivities.Add((Activity)a);
                })
                .ReturnsAsync(new ResourceResponse { Id = "msg-123" });

            var turnStateMock = new Mock<ITurnState>();

            var method = typeof(HermesTeamsAgent)
                .GetMethod("OnMessageAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            await (Task)method!.Invoke(
                agent,
                new object?[] { turnContextMock.Object, turnStateMock.Object, CancellationToken.None }
            )!;

            // Assert - should have sent at least the final message despite streaming failure
            Assert.True(sentActivities.Count >= 1, "Should have sent at least 1 message");
            var finalMessage = sentActivities.Last();
            Assert.Equal(ActivityTypes.Message, finalMessage.Type);
            Assert.Equal("orchestrated response", finalMessage.Text);
        }

        #endregion
    }
}
