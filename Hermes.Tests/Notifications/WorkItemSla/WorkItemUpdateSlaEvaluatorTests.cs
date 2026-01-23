using Hermes.Notifications.Infra;
using Hermes.Notifications.Infra.Models;
using Hermes.Notifications.WorkItemSla;
using Hermes.Notifications.WorkItemSla.Models;
using Hermes.Storage.Repositories.ConversationReference;
using Integrations.AzureDevOps;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.Tests.Notifications.WorkItemSla
{
	public class WorkItemUpdateSlaEvaluatorTests
	{
		private readonly Mock<IConversationReferenceRepository> _conversationRefRepoMock;
		private readonly Mock<IAzureDevOpsWorkItemClient> _azureDevOpsClientMock;
		private readonly Mock<INotificationGate> _notificationGateMock;
		private readonly Mock<IProactiveMessenger> _proactiveMessengerMock;
		private readonly Mock<ILogger<WorkItemUpdateSlaEvaluator>> _loggerMock;
		private readonly WorkItemUpdateSlaConfiguration _configuration;
		private readonly WorkItemUpdateSlaMessageComposer _messageComposer;

		public WorkItemUpdateSlaEvaluatorTests()
		{
			_conversationRefRepoMock = new Mock<IConversationReferenceRepository>();
			_azureDevOpsClientMock = new Mock<IAzureDevOpsWorkItemClient>();
			_notificationGateMock = new Mock<INotificationGate>();
			_proactiveMessengerMock = new Mock<IProactiveMessenger>();
			_loggerMock = new Mock<ILogger<WorkItemUpdateSlaEvaluator>>();

			_configuration = new WorkItemUpdateSlaConfiguration
			{
				Enabled = true,
				AzureDevOpsBaseUrl = "https://dev.azure.com/test",
				SlaRules = new Dictionary<string, int>
				{
					{ "Bug", 2 },
					{ "Task", 5 }
				},
				MaxNotificationsPerRun = 100,
				DeduplicationWindowHours = 24
			};

			_messageComposer = new WorkItemUpdateSlaMessageComposer();
		}

		private WorkItemUpdateSlaEvaluator CreateEvaluator()
		{
			return new WorkItemUpdateSlaEvaluator(
				_conversationRefRepoMock.Object,
				_azureDevOpsClientMock.Object,
				_notificationGateMock.Object,
				_proactiveMessengerMock.Object,
				_configuration,
				_messageComposer,
				_loggerMock.Object);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_SlaDisabled_ReturnsEmptySummary()
		{
			// Arrange
			_configuration.Enabled = false;
			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(0, result.UsersProcessed);
			Assert.Equal(0, result.ViolationsDetected);
			Assert.Equal(0, result.NotificationsSent);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_NoActiveUsers_ReturnsEmptySummary()
		{
			// Arrange
			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<ConversationReferenceDocument>());

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(0, result.UsersProcessed);
			Assert.Equal(0, result.ViolationsDetected);
			Assert.Equal(0, result.NotificationsSent);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_UserWithNoEmail_SkipsUser()
		{
			// Arrange
			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-123",
					ConversationReferenceJson = "{\"from\":{\"name\":\"Test User\"}}" // No email
				}
			};

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(1, result.UsersProcessed);
			Assert.Equal(0, result.ViolationsDetected);
			_azureDevOpsClientMock.Verify(
				c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()),
				Times.Never);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_UserWithNoWorkItems_SkipsNotification()
		{
			// Arrange
			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-123",
					ConversationReferenceJson = "{\"from\":{\"properties\":{\"email\":\"test@example.com\"}}}"
				}
			};

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					"test@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync("{\"count\":0,\"value\":[]}");

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(1, result.UsersProcessed);
			Assert.Equal(0, result.ViolationsDetected);
			Assert.Equal(0, result.NotificationsSent);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_WorkItemWithinSla_NoViolation()
		{
			// Arrange
			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-123",
					ConversationReferenceJson = "{\"from\":{\"properties\":{\"email\":\"test@example.com\"}}}"
				}
			};

			var workItemsJson = $@"{{
				""count"": 1,
				""value"": [
					{{
						""id"": 1,
						""fields"": {{
							""System.Id"": 1,
							""System.Title"": ""Recent bug"",
							""System.WorkItemType"": ""Bug"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-1):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}}
				]
			}}";

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(workItemsJson);

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(1, result.UsersProcessed);
			Assert.Equal(0, result.ViolationsDetected);
			Assert.Equal(0, result.NotificationsSent);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_WorkItemViolatesSla_SendsNotification()
		{
			// Arrange
			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-123",
					ConversationReferenceJson = "{\"from\":{\"properties\":{\"email\":\"test@example.com\"}}}"
				}
			};

			var workItemsJson = $@"{{
				""count"": 1,
				""value"": [
					{{
						""id"": 1,
						""fields"": {{
							""System.Id"": 1,
							""System.Title"": ""Old bug"",
							""System.WorkItemType"": ""Bug"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-5):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}}
				]
			}}";

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(workItemsJson);

			_notificationGateMock
				.Setup(g => g.EvaluateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new GateResult { CanSend = true });

			_proactiveMessengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(new ProactiveMessageResult { Success = true });

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(1, result.UsersProcessed);
			Assert.Equal(1, result.ViolationsDetected);
			Assert.Equal(1, result.NotificationsSent);

			_proactiveMessengerMock.Verify(
				m => m.SendMessageByTeamsUserIdAsync(
					"user-123",
					It.Is<string>(msg => msg.Contains("Old bug") && msg.Contains("5 days")),
					It.IsAny<CancellationToken>()),
				Times.Once);

			_notificationGateMock.Verify(
				g => g.RecordNotificationAsync(
					"user-123",
					"WorkItemUpdateSla",
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_NotificationGateBlocks_DoesNotSendNotification()
		{
			// Arrange
			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-123",
					ConversationReferenceJson = "{\"from\":{\"properties\":{\"email\":\"test@example.com\"}}}"
				}
			};

			var workItemsJson = $@"{{
				""count"": 1,
				""value"": [
					{{
						""id"": 1,
						""fields"": {{
							""System.Id"": 1,
							""System.Title"": ""Old bug"",
							""System.WorkItemType"": ""Bug"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-5):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}}
				]
			}}";

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(workItemsJson);

			_notificationGateMock
				.Setup(g => g.EvaluateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new GateResult { CanSend = false, BlockedReason = "Quiet hours" });

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(1, result.UsersProcessed);
			Assert.Equal(1, result.ViolationsDetected);
			Assert.Equal(1, result.NotificationsBlocked);
			Assert.Equal(0, result.NotificationsSent);

			_proactiveMessengerMock.Verify(
				m => m.SendMessageByTeamsUserIdAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()),
				Times.Never);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_MultipleViolations_SendsDigestMessage()
		{
			// Arrange
			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-123",
					ConversationReferenceJson = "{\"from\":{\"properties\":{\"email\":\"test@example.com\"}}}"
				}
			};

			var workItemsJson = $@"{{
				""count"": 2,
				""value"": [
					{{
						""id"": 1,
						""fields"": {{
							""System.Id"": 1,
							""System.Title"": ""Old bug"",
							""System.WorkItemType"": ""Bug"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-5):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}},
					{{
						""id"": 2,
						""fields"": {{
							""System.Id"": 2,
							""System.Title"": ""Old task"",
							""System.WorkItemType"": ""Task"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-10):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}}
				]
			}}";

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(workItemsJson);

			_notificationGateMock
				.Setup(g => g.EvaluateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new GateResult { CanSend = true });

			_proactiveMessengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(new ProactiveMessageResult { Success = true });

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(1, result.UsersProcessed);
			Assert.Equal(2, result.ViolationsDetected);
			Assert.Equal(1, result.NotificationsSent);

			_proactiveMessengerMock.Verify(
				m => m.SendMessageByTeamsUserIdAsync(
					"user-123",
					It.Is<string>(msg => msg.Contains("2 work items") && msg.Contains("Old bug") && msg.Contains("Old task")),
					It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_ProactiveMessengerFails_IncrementsErrorCount()
		{
			// Arrange
			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-123",
					ConversationReferenceJson = "{\"from\":{\"properties\":{\"email\":\"test@example.com\"}}}"
				}
			};

			var workItemsJson = $@"{{
				""count"": 1,
				""value"": [
					{{
						""id"": 1,
						""fields"": {{
							""System.Id"": 1,
							""System.Title"": ""Old bug"",
							""System.WorkItemType"": ""Bug"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-5):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}}
				]
			}}";

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(workItemsJson);

			_notificationGateMock
				.Setup(g => g.EvaluateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new GateResult { CanSend = true });

			_proactiveMessengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(new ProactiveMessageResult { Success = false, ErrorMessage = "Send failed" });

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(1, result.UsersProcessed);
			Assert.Equal(1, result.ViolationsDetected);
			Assert.Equal(0, result.NotificationsSent);
			Assert.Equal(1, result.Errors);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_MaxNotificationsReached_StopsProcessing()
		{
			// Arrange
			_configuration.MaxNotificationsPerRun = 1;

			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-1",
					ConversationReferenceJson = "{\"from\":{\"properties\":{\"email\":\"test1@example.com\"}}}"
				},
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-2",
					ConversationReferenceJson = "{\"from\":{\"properties\":{\"email\":\"test2@example.com\"}}}"
				}
			};

			var workItemsJson = $@"{{
				""count"": 1,
				""value"": [
					{{
						""id"": 1,
						""fields"": {{
							""System.Id"": 1,
							""System.Title"": ""Old bug"",
							""System.WorkItemType"": ""Bug"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-5):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}}
				]
			}}";

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(workItemsJson);

			_notificationGateMock
				.Setup(g => g.EvaluateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new GateResult { CanSend = true });

			_proactiveMessengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(new ProactiveMessageResult { Success = true });

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(1, result.UsersProcessed); // Only processed first user
			Assert.Equal(1, result.NotificationsSent);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_CallsAzureDevOpsClient_WithCorrectParameters()
		{
			// Arrange
			_configuration.IterationPath = "@CurrentIteration";

			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-123",
					ConversationReferenceJson = "{\"from\":{\"properties\":{\"email\":\"test@example.com\"}}}"
				}
			};

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync("{\"count\":0,\"value\":[]}");

			var evaluator = CreateEvaluator();

			// Act
			await evaluator.EvaluateAndNotifyAsync();

			// Assert
			_azureDevOpsClientMock.Verify(
				c => c.GetWorkItemsByAssignedUserAsync(
					"test@example.com",
					It.Is<IEnumerable<string>>(states => states.Contains("Active") && states.Contains("New")),
					It.IsAny<IEnumerable<string>>(),
					"@CurrentIteration",
					It.Is<IEnumerable<string>>(types => types.Contains("Bug") && types.Contains("Task")),
					It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_BypassGates_SkipsGateEvaluation()
		{
			// Arrange
			_configuration.BypassGates = true;

			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-123",
					ConversationReferenceJson = "{\"user\":{\"email\":\"test@example.com\"}}"
				}
			};

			var workItemsJson = $@"{{
				""count"": 1,
				""value"": [
					{{
						""fields"": {{
							""System.Id"": 1,
							""System.Title"": ""Old bug"",
							""System.WorkItemType"": ""Bug"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-5):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}}
				]
			}}";

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(workItemsJson);

			_proactiveMessengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(new ProactiveMessageResult { Success = true });

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(1, result.NotificationsSent);
			Assert.Equal(0, result.NotificationsBlocked);

			// Verify gate was NOT evaluated
			_notificationGateMock.Verify(
				g => g.EvaluateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
				Times.Never);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_BypassGates_SkipsRecordingNotification()
		{
			// Arrange
			_configuration.BypassGates = true;

			var conversationRefs = new List<ConversationReferenceDocument>
			{
				new ConversationReferenceDocument
				{
					TeamsUserId = "user-123",
					ConversationReferenceJson = "{\"user\":{\"email\":\"test@example.com\"}}"
				}
			};

			var workItemsJson = $@"{{
				""count"": 1,
				""value"": [
					{{
						""fields"": {{
							""System.Id"": 1,
							""System.Title"": ""Old bug"",
							""System.WorkItemType"": ""Bug"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-5):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}}
				]
			}}";

			_conversationRefRepoMock
				.Setup(r => r.GetActiveReferencesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(conversationRefs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(workItemsJson);

			_proactiveMessengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(new ProactiveMessageResult { Success = true });

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.EvaluateAndNotifyAsync();

			// Assert
			Assert.Equal(1, result.NotificationsSent);

			// Verify notification was NOT recorded
			_notificationGateMock.Verify(
				g => g.RecordNotificationAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()),
				Times.Never);
		}
	}
}
