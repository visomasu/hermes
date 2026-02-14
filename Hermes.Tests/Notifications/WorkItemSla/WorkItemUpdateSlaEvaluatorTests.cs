using Hermes.Domain.WorkItemSla;
using Hermes.Domain.WorkItemSla.Models;
using Hermes.Notifications.Infra;
using Hermes.Notifications.Infra.Models;
using Hermes.Notifications.WorkItemSla;
using Hermes.Notifications.WorkItemSla.Models;
using Hermes.Storage.Repositories.TeamConfiguration;
using Hermes.Storage.Repositories.TeamConfiguration;
using Hermes.Storage.Repositories.TeamConfiguration;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;
using Integrations.AzureDevOps;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.Tests.Notifications.WorkItemSla
{
	// Suppress warnings for testing obsolete methods (maintained for backwards compatibility and regression coverage)
#pragma warning disable CS0618
	public class WorkItemUpdateSlaEvaluatorTests
	{
		private readonly Mock<IUserConfigurationRepository> _userConfigRepoMock;
		private readonly Mock<ITeamConfigurationRepository> _teamConfigRepoMock;
		private readonly Mock<IAzureDevOpsWorkItemClient> _azureDevOpsClientMock;
		private readonly Mock<INotificationGate> _notificationGateMock;
		private readonly Mock<IProactiveMessenger> _proactiveMessengerMock;
		private readonly Mock<ILogger<WorkItemUpdateSlaEvaluator>> _loggerMock;
		private readonly WorkItemUpdateSlaConfiguration _configuration;
		private readonly WorkItemUpdateSlaMessageComposer _messageComposer;

		public WorkItemUpdateSlaEvaluatorTests()
		{
			_userConfigRepoMock = new Mock<IUserConfigurationRepository>();
			_teamConfigRepoMock = new Mock<ITeamConfigurationRepository>();
			_azureDevOpsClientMock = new Mock<IAzureDevOpsWorkItemClient>();
			_notificationGateMock = new Mock<INotificationGate>();
			_proactiveMessengerMock = new Mock<IProactiveMessenger>();
			_loggerMock = new Mock<ILogger<WorkItemUpdateSlaEvaluator>>();

			_configuration = new WorkItemUpdateSlaConfiguration
			{
				Enabled = true,
				AzureDevOpsBaseUrl = "https://dev.azure.com/test",
#pragma warning disable CS0618 // Type or member is obsolete
				TeamName = "Test Team",
				IterationPath = @"OneCRM\FY26\Q3\Sprint1",
				SlaRules = new Dictionary<string, int>
				{
					{ "Bug", 2 },
					{ "Task", 5 }
				},
#pragma warning restore CS0618 // Type or member is obsolete
				GlobalSlaDefaults = new Dictionary<string, int>
				{
					{ "Bug", 2 },
					{ "Task", 5 }
				},
				MaxNotificationsPerRun = 100,
				DeduplicationWindowHours = 24
			};

			_messageComposer = new WorkItemUpdateSlaMessageComposer();

			// Setup default team configuration mock to return test team
			var defaultTeam = CreateTeamConfig("test-team", "Test Team");
			_teamConfigRepoMock
				.Setup(r => r.GetAllTeamsAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<TeamConfigurationDocument> { defaultTeam });
		}

		private WorkItemUpdateSlaEvaluator CreateEvaluator()
	{
		return new WorkItemUpdateSlaEvaluator(
			_loggerMock.Object,
			_userConfigRepoMock.Object,
			_teamConfigRepoMock.Object,
			_azureDevOpsClientMock.Object,
			_notificationGateMock.Object,
			_proactiveMessengerMock.Object,
			_configuration,
			_messageComposer);
	}

		private UserConfigurationDocument CreateUserConfig(string teamsUserId, string email, List<string>? directReports = null, List<string>? subscribedTeams = null)
		{
			return new UserConfigurationDocument
			{
				Id = Guid.NewGuid().ToString(),
				TeamsUserId = teamsUserId,
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = email,
					DirectReportEmails = directReports ?? new List<string>(),
					SubscribedTeamIds = subscribedTeams ?? new List<string> { "test-team" }
				}
			};
		}

	private TeamConfigurationDocument CreateTeamConfig(string teamId, string teamName, List<string>? areaPaths = null, Dictionary<string, int>? slaOverrides = null)
	{
		return new TeamConfigurationDocument
		{
			TeamId = teamId,
			TeamName = teamName,
			IterationPath = @"OneCRM\FY26\Q3\Sprint1",
			AreaPaths = areaPaths ?? new List<string> { @"OneCRM\Test" },
			SlaOverrides = slaOverrides ?? new Dictionary<string, int>()
		};
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
		public async Task EvaluateAndNotifyAsync_NoRegisteredUsers_ReturnsEmptySummary()
		{
			// Arrange
			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument>());

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
			var userConfig = new UserConfigurationDocument
			{
				Id = "user-123",
				TeamsUserId = "user-123",
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = string.Empty, // No email
					DirectReportEmails = new List<string>()
				}
			};

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });

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
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()),
				Times.Never);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_ICWithNoWorkItems_SkipsNotification()
		{
			// Arrange
			var userConfig = CreateUserConfig("user-123", "test@example.com");

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					"test@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
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
		public async Task EvaluateAndNotifyAsync_ICWorkItemWithinSla_NoViolation()
		{
			// Arrange
			var userConfig = CreateUserConfig("user-123", "test@example.com");

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

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
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
		public async Task EvaluateAndNotifyAsync_ICWorkItemViolatesSla_SendsNotification()
		{
			// Arrange
			var userConfig = CreateUserConfig("user-123", "test@example.com");

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

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
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
		public async Task EvaluateAndNotifyAsync_ManagerWithTeamViolations_SendsManagerDigest()
		{
			// Arrange
			var userConfig = CreateUserConfig("manager-123", "manager@example.com",
				new List<string> { "report1@example.com", "report2@example.com" });

			var managerWorkItemsJson = $@"{{
				""count"": 1,
				""value"": [
					{{
						""id"": 1,
						""fields"": {{
							""System.Id"": 1,
							""System.Title"": ""Manager bug"",
							""System.WorkItemType"": ""Bug"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-5):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}}
				]
			}}";

			var report1WorkItemsJson = $@"{{
				""count"": 1,
				""value"": [
					{{
						""id"": 2,
						""fields"": {{
							""System.Id"": 2,
							""System.Title"": ""Report1 task"",
							""System.WorkItemType"": ""Task"",
							""System.ChangedDate"": ""{DateTime.UtcNow.AddDays(-10):yyyy-MM-ddTHH:mm:ssZ}""
						}}
					}}
				]
			}}";

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					"manager@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(managerWorkItemsJson);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					"report1@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(report1WorkItemsJson);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					"report2@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync("{\"count\":0,\"value\":[]}");

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
			Assert.Equal(2, result.ViolationsDetected); // Manager + Report1
			Assert.Equal(1, result.NotificationsSent);

			// Verify manager digest sent
			_proactiveMessengerMock.Verify(
				m => m.SendMessageByTeamsUserIdAsync(
					"manager-123",
					It.Is<string>(msg => msg.Contains("Manager SLA Violation Report") &&
					                      msg.Contains("Manager bug") &&
					                      msg.Contains("Report1 task")),
					It.IsAny<CancellationToken>()),
				Times.Once);

			// Verify all 3 emails were checked (manager + 2 directs)
			_azureDevOpsClientMock.Verify(
				c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()),
				Times.Exactly(3));
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_NotificationGateBlocks_DoesNotSendNotification()
		{
			// Arrange
			var userConfig = CreateUserConfig("user-123", "test@example.com");

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

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
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
		public async Task EvaluateAndNotifyAsync_ICMultipleViolations_SendsDigestMessage()
		{
			// Arrange
			var userConfig = CreateUserConfig("user-123", "test@example.com");

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

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
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
			var userConfig = CreateUserConfig("user-123", "test@example.com");

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

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
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
			_configuration.UserProcessingBatchSize = 1; // Process users sequentially

			var userConfigs = new List<UserConfigurationDocument>
			{
				CreateUserConfig("user-1", "test1@example.com"),
				CreateUserConfig("user-2", "test2@example.com")
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

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfigs);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
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

			var userConfig = CreateUserConfig("user-123", "test@example.com");

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });


			// Mock GetCurrentIterationPathAsync to return @CurrentIteration
			_azureDevOpsClientMock
				.Setup(c => c.GetCurrentIterationPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync("@CurrentIteration");
			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
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
					It.IsAny<IEnumerable<string>?>(),
					It.Is<IEnumerable<string>>(types => types.Contains("Bug") && types.Contains("Task")),
					It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task EvaluateAndNotifyAsync_BypassGates_SkipsGateEvaluation()
		{
			// Arrange
			_configuration.BypassGates = true;

			var userConfig = CreateUserConfig("user-123", "test@example.com");

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

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
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

			var userConfig = CreateUserConfig("user-123", "test@example.com");

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

			_userConfigRepoMock
				.Setup(r => r.GetAllWithSlaRegistrationAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<UserConfigurationDocument> { userConfig });

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
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

		[Fact]
		public async Task CheckViolationsForEmailAsync_DynamicIterationReturnsValid_UsesDynamicIteration()
		{
			// Arrange
			const string fallbackIteration = "OneCRM\\FY26\\Q3\\Sprint1";
			const string dynamicIteration = "OneCRM\\FY26\\Q3\\Sprint2";
			const string teamName = "Test Team";

			_configuration.TeamName = teamName;
			_configuration.IterationPath = fallbackIteration;

			// Mock GetCurrentIterationPathAsync to return a valid dynamic iteration
			_azureDevOpsClientMock
				.Setup(c => c.GetCurrentIterationPathAsync(teamName, It.IsAny<CancellationToken>()))
				.ReturnsAsync(dynamicIteration);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					"test@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					dynamicIteration, // Expect dynamic iteration to be used
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync("{\"count\":0,\"value\":[]}");

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.CheckViolationsForEmailAsync("test@example.com");

			// Assert
			Assert.Empty(result);

			// Verify GetCurrentIterationPathAsync was called
			_azureDevOpsClientMock.Verify(
				c => c.GetCurrentIterationPathAsync(teamName, It.IsAny<CancellationToken>()),
				Times.Once);

			// Verify GetWorkItemsByAssignedUserAsync was called with the DYNAMIC iteration
			_azureDevOpsClientMock.Verify(
				c => c.GetWorkItemsByAssignedUserAsync(
					"test@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					dynamicIteration,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task CheckViolationsForEmailAsync_DynamicIterationReturnsNull_UsesFallbackIteration()
		{
			// Arrange
			const string fallbackIteration = "OneCRM\\FY26\\Q3\\Sprint1";
			const string teamName = "Test Team";

			_configuration.TeamName = teamName;
			_configuration.IterationPath = fallbackIteration;

			// Mock GetCurrentIterationPathAsync to return null (no matching iteration)
			_azureDevOpsClientMock
				.Setup(c => c.GetCurrentIterationPathAsync(teamName, It.IsAny<CancellationToken>()))
				.ReturnsAsync((string?)null);

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					"test@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					fallbackIteration, // Expect fallback iteration to be used
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync("{\"count\":0,\"value\":[]}");

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.CheckViolationsForEmailAsync("test@example.com");

			// Assert
			Assert.Empty(result);

			// Verify GetCurrentIterationPathAsync was called
			_azureDevOpsClientMock.Verify(
				c => c.GetCurrentIterationPathAsync(teamName, It.IsAny<CancellationToken>()),
				Times.Once);

			// Verify GetWorkItemsByAssignedUserAsync was called with the FALLBACK iteration
			_azureDevOpsClientMock.Verify(
				c => c.GetWorkItemsByAssignedUserAsync(
					"test@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					fallbackIteration,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task CheckViolationsForEmailAsync_DynamicIterationThrowsException_UsesFallbackIteration()
		{
			// Arrange
			const string fallbackIteration = "OneCRM\\FY26\\Q3\\Sprint1";
			const string teamName = "Test Team";

			_configuration.TeamName = teamName;
			_configuration.IterationPath = fallbackIteration;

			// Mock GetCurrentIterationPathAsync to throw an exception
			_azureDevOpsClientMock
				.Setup(c => c.GetCurrentIterationPathAsync(teamName, It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("Azure DevOps API error"));

			_azureDevOpsClientMock
				.Setup(c => c.GetWorkItemsByAssignedUserAsync(
					"test@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					fallbackIteration, // Expect fallback iteration to be used
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync("{\"count\":0,\"value\":[]}");

			var evaluator = CreateEvaluator();

			// Act
			var result = await evaluator.CheckViolationsForEmailAsync("test@example.com");

			// Assert
			Assert.Empty(result);

			// Verify GetCurrentIterationPathAsync was called (and threw exception)
			_azureDevOpsClientMock.Verify(
				c => c.GetCurrentIterationPathAsync(teamName, It.IsAny<CancellationToken>()),
				Times.Once);

			// Verify GetWorkItemsByAssignedUserAsync was still called with the FALLBACK iteration
			_azureDevOpsClientMock.Verify(
				c => c.GetWorkItemsByAssignedUserAsync(
					"test@example.com",
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					fallbackIteration,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<CancellationToken>()),
				Times.Once);
		}
	}
#pragma warning restore CS0618
}
