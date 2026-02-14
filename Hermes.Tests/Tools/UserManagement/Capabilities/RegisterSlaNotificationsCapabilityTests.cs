using Hermes.Integrations.MicrosoftGraph;
using Hermes.Storage.Repositories.TeamConfiguration;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;
using Hermes.Tools.UserManagement.Capabilities;
using Hermes.Tools.UserManagement.Capabilities.Inputs;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Hermes.Tests.Tools.UserManagement.Capabilities
{
	public class RegisterSlaNotificationsCapabilityTests
	{
		private readonly Mock<ILogger<RegisterSlaNotificationsCapability>> _loggerMock;
		private readonly Mock<IMicrosoftGraphClient> _graphMock;
		private readonly Mock<IUserConfigurationRepository> _userRepoMock;
		private readonly Mock<ITeamConfigurationRepository> _teamRepoMock;

		public RegisterSlaNotificationsCapabilityTests()
		{
			_loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();
			_graphMock = new Mock<IMicrosoftGraphClient>();
			_userRepoMock = new Mock<IUserConfigurationRepository>();
			_teamRepoMock = new Mock<ITeamConfigurationRepository>();
		}

		private RegisterSlaNotificationsCapability CreateCapability()
		{
			return new RegisterSlaNotificationsCapability(
				_loggerMock.Object,
				_graphMock.Object,
				_userRepoMock.Object,
				_teamRepoMock.Object);
		}

		[Fact]
		public async Task ExecuteAsync_WithValidTeamIds_RegistersSuccessfully()
		{
			// Arrange
			var teams = new List<TeamConfigurationDocument>
			{
				new TeamConfigurationDocument
				{
					TeamId = "contact-center-ai",
					TeamName = "Contact Center AI",
					AreaPaths = new List<string> { "OneCRM\\AI\\ContactCenter" }
				},
				new TeamConfigurationDocument
				{
					TeamId = "auth-antifraud",
					TeamName = "Authentication & Anti-Fraud",
					AreaPaths = new List<string> { "OneCRM\\Security\\Auth" }
				}
			};

			var userProfile = new UserProfileResult
			{
				Email = "manager@example.com",
				DirectReportEmails = new List<string> { "report1@example.com", "report2@example.com" }
			};

			_graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			_userRepoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			_userRepoMock.Setup(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			_teamRepoMock.Setup(x => x.GetAllTeamsAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(teams);

			var capability = CreateCapability();
			var input = new RegisterSlaNotificationsCapabilityInput
			{
				TeamsUserId = "user-123",
				TeamIds = new List<string> { "contact-center-ai", "auth-antifraud" }
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());
			Assert.Contains("2 team", response.GetProperty("message").GetString()!);
			Assert.Contains("Contact Center AI", response.GetProperty("message").GetString()!);

			_userRepoMock.Verify(x => x.CreateAsync(It.Is<UserConfigurationDocument>(doc =>
				doc.SlaRegistration != null &&
				doc.SlaRegistration.IsRegistered &&
				doc.SlaRegistration.SubscribedTeamIds.Count == 2 &&
				doc.SlaRegistration.SubscribedTeamIds.Contains("contact-center-ai") &&
				doc.SlaRegistration.SubscribedTeamIds.Contains("auth-antifraud")
			)), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_WithInvalidTeamIds_ReturnsError()
		{
			// Arrange
			var teams = new List<TeamConfigurationDocument>
			{
				new TeamConfigurationDocument
				{
					TeamId = "valid-team",
					TeamName = "Valid Team"
				}
			};

			var userProfile = new UserProfileResult
			{
				Email = "user@example.com",
				DirectReportEmails = new List<string>()
			};

			_graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			_userRepoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			_teamRepoMock.Setup(x => x.GetAllTeamsAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(teams);

			var capability = CreateCapability();
			var input = new RegisterSlaNotificationsCapabilityInput
			{
				TeamsUserId = "user-123",
				TeamIds = new List<string> { "invalid-team-1", "invalid-team-2" }
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("None of the specified teams", response.GetProperty("message").GetString()!);
			Assert.Contains("valid-team", response.GetProperty("message").GetString()!);

			_userRepoMock.Verify(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_WithoutTeamIds_ReturnsError()
		{
			// Arrange
			var userProfile = new UserProfileResult
			{
				Email = "user@example.com",
				DirectReportEmails = new List<string>()
			};

			_graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			_userRepoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			var capability = CreateCapability();
			var input = new RegisterSlaNotificationsCapabilityInput
			{
				TeamsUserId = "user-123",
				TeamIds = null
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("no teams", response.GetProperty("message").GetString()!.ToLower());

			_userRepoMock.Verify(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_MigratesExistingAreaPathsToTeams()
		{
			// Arrange
			var teams = new List<TeamConfigurationDocument>
			{
				new TeamConfigurationDocument
				{
					TeamId = "contact-center-ai",
					TeamName = "Contact Center AI",
					AreaPaths = new List<string> { "OneCRM\\AI\\ContactCenter" }
				}
			};

			var existingConfig = new UserConfigurationDocument
			{
				Id = "user-123",
				PartitionKey = "user-123",
				TeamsUserId = "user-123",
				Notifications = new NotificationPreferences(),
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = "user@example.com",
#pragma warning disable CS0618 // Type or member is obsolete
					AreaPaths = new List<string> { "OneCRM\\AI\\ContactCenter" }
#pragma warning restore CS0618 // Type or member is obsolete
				}
			};

			var userProfile = new UserProfileResult
			{
				Email = "user@example.com",
				DirectReportEmails = new List<string>()
			};

			_graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			_userRepoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(existingConfig);

			_userRepoMock.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UserConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			_teamRepoMock.Setup(x => x.GetAllTeamsAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(teams);

			var capability = CreateCapability();
			var input = new RegisterSlaNotificationsCapabilityInput
			{
				TeamsUserId = "user-123",
				TeamIds = null // Migration happens when no teams specified
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());

			_userRepoMock.Verify(x => x.UpdateAsync("user-123", It.Is<UserConfigurationDocument>(doc =>
				doc.SlaRegistration != null &&
				doc.SlaRegistration.SubscribedTeamIds.Count == 1 &&
				doc.SlaRegistration.SubscribedTeamIds.Contains("contact-center-ai")
			)), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_ExistingUser_UpdatesConfig()
		{
			// Arrange
			var teams = new List<TeamConfigurationDocument>
			{
				new TeamConfigurationDocument
				{
					TeamId = "contact-center-ai",
					TeamName = "Contact Center AI"
				}
			};

			var existingConfig = new UserConfigurationDocument
			{
				Id = "user-789",
				PartitionKey = "user-789",
				TeamsUserId = "user-789",
				Notifications = new NotificationPreferences()
			};

			var userProfile = new UserProfileResult
			{
				Email = "manager@example.com",
				DirectReportEmails = new List<string> { "report1@example.com" }
			};

			_graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			_userRepoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(existingConfig);

			_userRepoMock.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UserConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			_teamRepoMock.Setup(x => x.GetAllTeamsAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(teams);

			var capability = CreateCapability();
			var input = new RegisterSlaNotificationsCapabilityInput
			{
				TeamsUserId = "user-789",
				TeamIds = new List<string> { "contact-center-ai" }
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());

			_userRepoMock.Verify(x => x.UpdateAsync("user-789", It.Is<UserConfigurationDocument>(doc =>
				doc.SlaRegistration != null &&
				doc.SlaRegistration.IsRegistered
			)), Times.Once);

			_userRepoMock.Verify(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_GraphApiFailure_ReturnsError()
		{
			// Arrange
			_graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("Graph API error"));

			_userRepoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			var capability = CreateCapability();
			var input = new RegisterSlaNotificationsCapabilityInput
			{
				TeamsUserId = "user-error",
				TeamIds = new List<string> { "contact-center-ai" }
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("error occurred", response.GetProperty("message").GetString()!);

			_userRepoMock.Verify(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()), Times.Never);
			_userRepoMock.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_NoEmailFromGraph_ReturnsError()
		{
			// Arrange
			var userProfile = new UserProfileResult
			{
				Email = string.Empty, // No email
				DirectReportEmails = new List<string>()
			};

			_graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			_userRepoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			var capability = CreateCapability();
			var input = new RegisterSlaNotificationsCapabilityInput
			{
				TeamsUserId = "user-noemail",
				TeamIds = new List<string> { "contact-center-ai" }
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("email", response.GetProperty("message").GetString()!.ToLower());

			_userRepoMock.Verify(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_EmptyTeamsUserId_ReturnsError()
		{
			// Arrange
			var capability = CreateCapability();
			var input = new RegisterSlaNotificationsCapabilityInput { TeamsUserId = string.Empty };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("required", response.GetProperty("message").GetString()!.ToLower());

			_graphMock.Verify(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public void Name_ReturnsCorrectName()
		{
			// Arrange
			var capability = CreateCapability();

			// Act
			var name = capability.Name;

			// Assert
			Assert.Equal("RegisterSlaNotifications", name);
		}

		[Fact]
		public void Description_ReturnsCorrectDescription()
		{
			// Arrange
			var capability = CreateCapability();

			// Act
			var description = capability.Description;

			// Assert
			Assert.NotNull(description);
			Assert.Contains("Register", description);
			Assert.Contains("SLA", description);
		}

		[Fact]
		public async Task ExecuteAsync_WithPartiallyValidTeamIds_RegistersOnlyValidTeams()
		{
			// Arrange
			var teams = new List<TeamConfigurationDocument>
			{
				new TeamConfigurationDocument
				{
					TeamId = "contact-center-ai",
					TeamName = "Contact Center AI"
				}
			};

			var userProfile = new UserProfileResult
			{
				Email = "user@example.com",
				DirectReportEmails = new List<string>()
			};

			_graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			_userRepoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			_userRepoMock.Setup(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			_teamRepoMock.Setup(x => x.GetAllTeamsAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(teams);

			var capability = CreateCapability();
			var input = new RegisterSlaNotificationsCapabilityInput
			{
				TeamsUserId = "user-123",
				TeamIds = new List<string> { "contact-center-ai", "invalid-team" }
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());

			_userRepoMock.Verify(x => x.CreateAsync(It.Is<UserConfigurationDocument>(doc =>
				doc.SlaRegistration != null &&
				doc.SlaRegistration.SubscribedTeamIds.Count == 1 &&
				doc.SlaRegistration.SubscribedTeamIds.Contains("contact-center-ai")
			)), Times.Once);
		}
	}
}
