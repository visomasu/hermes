using Hermes.Domain.WorkItemSla;
using Hermes.Domain.WorkItemSla.Models;
using Hermes.Integrations.MicrosoftGraph;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;
using Hermes.Tools.WorkItemSla.Capabilities;
using Hermes.Tools.WorkItemSla.Capabilities.Inputs;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Hermes.Tests.Tools.WorkItemSla.Capabilities
{
	// Suppress warnings for testing obsolete CheckViolationsForEmailAsync method
	// (maintained for backwards compatibility)
#pragma warning disable CS0618
	public class CheckSlaViolationsCapabilityTests
	{
		[Fact]
		public async Task ExecuteAsync_RegisteredManagerWithViolations_ReturnsGroupedResults()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			var userConfig = new UserConfigurationDocument
			{
				Id = "user-123",
				TeamsUserId = "user-123",
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = "manager@example.com",
					DirectReportEmails = new List<string> { "report1@example.com" }
				}
			};

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfig);

			// Manager has violations
			evaluatorMock.Setup(x => x.CheckViolationsForEmailAsync("manager@example.com", It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<WorkItemUpdateSlaViolation>
				{
					new() { WorkItemId = 1, Title = "Manager task", WorkItemType = "Task", DaysSinceUpdate = 10, SlaThresholdDays = 5 }
				});

			// Direct report has violations
			evaluatorMock.Setup(x => x.CheckViolationsForEmailAsync("report1@example.com", It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<WorkItemUpdateSlaViolation>
				{
					new() { WorkItemId = 2, Title = "Report task", WorkItemType = "Task", DaysSinceUpdate = 8, SlaThresholdDays = 5 }
				});

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = "user-123" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());
			Assert.Contains("team", response.GetProperty("message").GetString()!.ToLower());
			Assert.True(response.GetProperty("isManager").GetBoolean());
			Assert.Equal(1, response.GetProperty("directReportCount").GetInt32());

			var violations = response.GetProperty("violations");
			Assert.Equal(2, violations.GetProperty("manager@example.com").GetArrayLength() +
			                   violations.GetProperty("report1@example.com").GetArrayLength());

			// Verify both emails were checked
			evaluatorMock.Verify(x => x.CheckViolationsForEmailAsync("manager@example.com", It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
			evaluatorMock.Verify(x => x.CheckViolationsForEmailAsync("report1@example.com", It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_RegisteredICWithViolations_ReturnsSingleOwner()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			var userConfig = new UserConfigurationDocument
			{
				Id = "user-456",
				TeamsUserId = "user-456",
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = "ic@example.com",
					DirectReportEmails = new List<string>() // IC has no directs
				}
			};

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfig);

			evaluatorMock.Setup(x => x.CheckViolationsForEmailAsync("ic@example.com", It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<WorkItemUpdateSlaViolation>
				{
					new() { WorkItemId = 3, Title = "IC task", WorkItemType = "Bug", DaysSinceUpdate = 7, SlaThresholdDays = 3 }
				});

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = "user-456" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());
			Assert.DoesNotContain("team", response.GetProperty("message").GetString()!.ToLower());
			Assert.False(response.GetProperty("isManager").GetBoolean());
			Assert.Equal(0, response.GetProperty("directReportCount").GetInt32());

			// Verify only user's email was checked
			evaluatorMock.Verify(x => x.CheckViolationsForEmailAsync("ic@example.com", It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
			evaluatorMock.Verify(x => x.CheckViolationsForEmailAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_UnregisteredUser_FetchesFromGraph()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			// User not registered
			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			var userProfile = new UserProfileResult
			{
				Email = "unregistered@example.com",
				DirectReportEmails = new List<string>()
			};

			graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			evaluatorMock.Setup(x => x.CheckViolationsForEmailAsync("unregistered@example.com", It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<WorkItemUpdateSlaViolation>());

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = "user-unregistered" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());

			// Verify Graph was called since user not registered
			graphMock.Verify(x => x.GetUserProfileWithDirectReportsAsync("user-unregistered", It.IsAny<CancellationToken>()), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_NoViolations_ReturnsSuccessMessage()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			var userConfig = new UserConfigurationDocument
			{
				Id = "user-789",
				TeamsUserId = "user-789",
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = "clean@example.com",
					DirectReportEmails = new List<string>()
				}
			};

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfig);

			evaluatorMock.Setup(x => x.CheckViolationsForEmailAsync("clean@example.com", It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<WorkItemUpdateSlaViolation>());

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = "user-789" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());
			Assert.Contains("No SLA violations", response.GetProperty("message").GetString()!);
		}

		[Fact]
		public async Task ExecuteAsync_GraphApiFails_ReturnsError()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("Graph API error"));

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = "user-error" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("error occurred", response.GetProperty("message").GetString()!);
		}

		[Fact]
		public async Task ExecuteAsync_EmptyTeamsUserId_ReturnsError()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = string.Empty };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("required", response.GetProperty("message").GetString()!.ToLower());

			// Verify no dependencies were called
			repoMock.Verify(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_NoEmailFromProfile_ReturnsError()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			var userProfile = new UserProfileResult
			{
				Email = string.Empty, // No email
				DirectReportEmails = new List<string>()
			};

			graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = "user-noemail" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("email", response.GetProperty("message").GetString()!.ToLower());
		}

		[Fact]
		public void Name_ReturnsCorrectName()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);

			// Act
			var name = capability.Name;

			// Assert
			Assert.Equal("CheckSlaViolations", name);
		}

		[Fact]
		public void Description_ReturnsCorrectDescription()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);

			// Act
			var description = capability.Description;

			// Assert
			Assert.NotNull(description);
			Assert.Contains("SLA", description);
			Assert.Contains("violations", description);
		}

		// =============================================================================
		// Multi-Team Support Tests (Option A Implementation)
		// =============================================================================

		[Fact]
		public async Task ExecuteAsync_RegisteredWithSingleTeam_UsesMultiTeamMethod()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			var userConfig = new UserConfigurationDocument
			{
				Id = "user-mt1",
				TeamsUserId = "user-mt1",
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = "user@example.com",
					DirectReportEmails = new List<string>(),
					SubscribedTeamIds = new List<string> { "team-1" } // Single team subscription
				}
			};

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfig);

			// Mock multi-team method
			evaluatorMock.Setup(x => x.CheckViolationsForTeamsAsync("user@example.com", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<WorkItemUpdateSlaViolation>
				{
					new() { WorkItemId = 1, Title = "Task 1", WorkItemType = "Task", DaysSinceUpdate = 10, SlaThresholdDays = 5, TeamId = "team-1", TeamName = "Team Alpha" }
				});

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = "user-mt1" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());

			// Verify multi-team method was called
			evaluatorMock.Verify(x => x.CheckViolationsForTeamsAsync("user@example.com", It.Is<IEnumerable<string>>(t => t.Contains("team-1")), It.IsAny<CancellationToken>()), Times.Once);

			// Verify legacy method was NOT called
			evaluatorMock.Verify(x => x.CheckViolationsForEmailAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_RegisteredWithMultipleTeams_ChecksAllTeams()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			var userConfig = new UserConfigurationDocument
			{
				Id = "user-mt2",
				TeamsUserId = "user-mt2",
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = "manager@example.com",
					DirectReportEmails = new List<string> { "report@example.com" },
					SubscribedTeamIds = new List<string> { "team-1", "team-2" } // Multiple teams
				}
			};

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfig);

			// Manager violations
			evaluatorMock.Setup(x => x.CheckViolationsForTeamsAsync("manager@example.com", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<WorkItemUpdateSlaViolation>
				{
					new() { WorkItemId = 1, Title = "Task 1", WorkItemType = "Task", DaysSinceUpdate = 10, SlaThresholdDays = 5, TeamId = "team-1", TeamName = "Team Alpha" },
					new() { WorkItemId = 2, Title = "Bug 1", WorkItemType = "Bug", DaysSinceUpdate = 4, SlaThresholdDays = 2, TeamId = "team-2", TeamName = "Team Beta" }
				});

			// Direct report violations
			evaluatorMock.Setup(x => x.CheckViolationsForTeamsAsync("report@example.com", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<WorkItemUpdateSlaViolation>
				{
					new() { WorkItemId = 3, Title = "Task 2", WorkItemType = "Task", DaysSinceUpdate = 8, SlaThresholdDays = 5, TeamId = "team-1", TeamName = "Team Alpha" }
				});

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = "user-mt2" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());
			Assert.Contains("team", response.GetProperty("message").GetString()!.ToLower());

			// Verify multi-team method was called for both emails
			evaluatorMock.Verify(x => x.CheckViolationsForTeamsAsync("manager@example.com", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
			evaluatorMock.Verify(x => x.CheckViolationsForTeamsAsync("report@example.com", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);

			// Verify both team IDs were passed
			evaluatorMock.Verify(x => x.CheckViolationsForTeamsAsync(It.IsAny<string>(), It.Is<IEnumerable<string>>(t => t.Contains("team-1") && t.Contains("team-2")), It.IsAny<CancellationToken>()), Times.Exactly(2));
		}

		[Fact]
		public async Task ExecuteAsync_RegisteredWithoutTeams_FallsBackToLegacyMethod()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			var userConfig = new UserConfigurationDocument
			{
				Id = "user-legacy",
				TeamsUserId = "user-legacy",
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = "legacy@example.com",
					DirectReportEmails = new List<string>(),
					SubscribedTeamIds = null // No team subscriptions - should use legacy path
				}
			};

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfig);

			// Mock legacy method
			evaluatorMock.Setup(x => x.CheckViolationsForEmailAsync("legacy@example.com", It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<WorkItemUpdateSlaViolation>
				{
					new() { WorkItemId = 1, Title = "Legacy task", WorkItemType = "Task", DaysSinceUpdate = 10, SlaThresholdDays = 5 }
				});

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = "user-legacy" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());

			// Verify legacy method was called
			evaluatorMock.Verify(x => x.CheckViolationsForEmailAsync("legacy@example.com", It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()), Times.Once);

			// Verify multi-team method was NOT called
			evaluatorMock.Verify(x => x.CheckViolationsForTeamsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_MultiTeamNoViolations_ReturnsSuccessMessage()
		{
			// Arrange
			var evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<CheckSlaViolationsCapability>>();

			var userConfig = new UserConfigurationDocument
			{
				Id = "user-clean-mt",
				TeamsUserId = "user-clean-mt",
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = "clean@example.com",
					DirectReportEmails = new List<string>(),
					SubscribedTeamIds = new List<string> { "team-1", "team-2" }
				}
			};

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfig);

			// No violations across all teams
			evaluatorMock.Setup(x => x.CheckViolationsForTeamsAsync("clean@example.com", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<WorkItemUpdateSlaViolation>());

			var capability = new CheckSlaViolationsCapability(evaluatorMock.Object, graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new CheckSlaViolationsCapabilityInput { TeamsUserId = "user-clean-mt" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());
			Assert.Contains("No SLA violations", response.GetProperty("message").GetString()!);

			// Verify multi-team method was called
			evaluatorMock.Verify(x => x.CheckViolationsForTeamsAsync("clean@example.com", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
		}
	}
#pragma warning restore CS0618
}
