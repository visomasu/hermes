using Hermes.Integrations.MicrosoftGraph;
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
		[Fact]
		public async Task ExecuteAsync_NewUserManager_CreatesConfigAndRegisters()
		{
			// Arrange
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();

			var userProfile = new UserProfileResult
			{
				Email = "manager@example.com",
				DirectReportEmails = new List<string> { "report1@example.com", "report2@example.com" }
			};

			graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			repoMock.Setup(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			var capability = new RegisterSlaNotificationsCapability(graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new RegisterSlaNotificationsCapabilityInput { TeamsUserId = "user-123" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());
			Assert.Contains("team", response.GetProperty("message").GetString()!);
			Assert.True(response.GetProperty("isManager").GetBoolean());
			Assert.Equal(2, response.GetProperty("directReportCount").GetInt32());

			repoMock.Verify(x => x.CreateAsync(It.Is<UserConfigurationDocument>(doc =>
				doc.SlaRegistration != null &&
				doc.SlaRegistration.IsRegistered &&
				doc.SlaRegistration.AzureDevOpsEmail == "manager@example.com" &&
				doc.SlaRegistration.DirectReportEmails.Count == 2 &&
				doc.Notifications.SlaViolationNotifications
			)), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_NewUserIC_CreatesConfigAndRegisters()
		{
			// Arrange
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();

			var userProfile = new UserProfileResult
			{
				Email = "ic@example.com",
				DirectReportEmails = new List<string>() // Empty = IC
			};

			graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			repoMock.Setup(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			var capability = new RegisterSlaNotificationsCapability(graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new RegisterSlaNotificationsCapabilityInput { TeamsUserId = "user-456" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());
			Assert.DoesNotContain("team", response.GetProperty("message").GetString()!);
			Assert.False(response.GetProperty("isManager").GetBoolean());
			Assert.Equal(0, response.GetProperty("directReportCount").GetInt32());

			repoMock.Verify(x => x.CreateAsync(It.Is<UserConfigurationDocument>(doc =>
				doc.SlaRegistration != null &&
				doc.SlaRegistration.IsRegistered &&
				doc.SlaRegistration.AzureDevOpsEmail == "ic@example.com" &&
				doc.SlaRegistration.DirectReportEmails.Count == 0
			)), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_ExistingUser_UpdatesConfig()
		{
			// Arrange
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();

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

			graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(existingConfig);

			repoMock.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UserConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			var capability = new RegisterSlaNotificationsCapability(graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new RegisterSlaNotificationsCapabilityInput { TeamsUserId = "user-789" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());

			repoMock.Verify(x => x.UpdateAsync("user-789", It.Is<UserConfigurationDocument>(doc =>
				doc.SlaRegistration != null &&
				doc.SlaRegistration.IsRegistered
			)), Times.Once);

			repoMock.Verify(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_GraphApiFailure_ReturnsError()
		{
			// Arrange
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();

			graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("Graph API error"));

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			var capability = new RegisterSlaNotificationsCapability(graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new RegisterSlaNotificationsCapabilityInput { TeamsUserId = "user-error" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("error occurred", response.GetProperty("message").GetString()!);

			repoMock.Verify(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()), Times.Never);
			repoMock.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_NoEmailFromGraph_ReturnsError()
		{
			// Arrange
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();

			var userProfile = new UserProfileResult
			{
				Email = string.Empty, // No email
				DirectReportEmails = new List<string>()
			};

			graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			var capability = new RegisterSlaNotificationsCapability(graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new RegisterSlaNotificationsCapabilityInput { TeamsUserId = "user-noemail" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("email", response.GetProperty("message").GetString()!.ToLower());

			repoMock.Verify(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_EmptyTeamsUserId_ReturnsError()
		{
			// Arrange
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();

			var capability = new RegisterSlaNotificationsCapability(graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new RegisterSlaNotificationsCapabilityInput { TeamsUserId = string.Empty };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("required", response.GetProperty("message").GetString()!.ToLower());

			graphMock.Verify(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public void Name_ReturnsCorrectName()
		{
			// Arrange
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();

			var capability = new RegisterSlaNotificationsCapability(graphMock.Object, repoMock.Object, loggerMock.Object);

			// Act
			var name = capability.Name;

			// Assert
			Assert.Equal("RegisterSlaNotifications", name);
		}

		[Fact]
		public void Description_ReturnsCorrectDescription()
		{
			// Arrange
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();

			var capability = new RegisterSlaNotificationsCapability(graphMock.Object, repoMock.Object, loggerMock.Object);

			// Act
			var description = capability.Description;

			// Assert
			Assert.NotNull(description);
			Assert.Contains("Register", description);
			Assert.Contains("SLA", description);
		}

		[Fact]
		public async Task ExecuteAsync_WithAreaPaths_StoresAreaPathsInProfile()
		{
			// Arrange
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();

			var userProfile = new UserProfileResult
			{
				Email = "user@example.com",
				DirectReportEmails = new List<string>()
			};

			graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			UserConfigurationDocument? capturedConfig = null;
			repoMock.Setup(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()))
				.Callback<UserConfigurationDocument>(config => capturedConfig = config)
				.Returns(Task.CompletedTask);

			var capability = new RegisterSlaNotificationsCapability(graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new RegisterSlaNotificationsCapabilityInput
			{
				TeamsUserId = "user-123",
				AreaPaths = new List<string> { "Project\\Team1", "Project\\Team2" }
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());
			Assert.Contains("Project\\Team1", response.GetProperty("message").GetString()!);

			Assert.NotNull(capturedConfig);
			Assert.NotNull(capturedConfig!.SlaRegistration);
			Assert.Equal(2, capturedConfig.SlaRegistration.AreaPaths.Count);
			Assert.Contains("Project\\Team1", capturedConfig.SlaRegistration.AreaPaths);
			Assert.Contains("Project\\Team2", capturedConfig.SlaRegistration.AreaPaths);

			repoMock.Verify(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_WithoutAreaPaths_StoresEmptyAreaPathsList()
		{
			// Arrange
			var graphMock = new Mock<IMicrosoftGraphClient>();
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<RegisterSlaNotificationsCapability>>();

			var userProfile = new UserProfileResult
			{
				Email = "user@example.com",
				DirectReportEmails = new List<string>()
			};

			graphMock.Setup(x => x.GetUserProfileWithDirectReportsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userProfile);

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			UserConfigurationDocument? capturedConfig = null;
			repoMock.Setup(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()))
				.Callback<UserConfigurationDocument>(config => capturedConfig = config)
				.Returns(Task.CompletedTask);

			var capability = new RegisterSlaNotificationsCapability(graphMock.Object, repoMock.Object, loggerMock.Object);
			var input = new RegisterSlaNotificationsCapabilityInput
			{
				TeamsUserId = "user-123",
				AreaPaths = null // No area paths specified
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());

			Assert.NotNull(capturedConfig);
			Assert.NotNull(capturedConfig!.SlaRegistration);
			Assert.Empty(capturedConfig.SlaRegistration.AreaPaths);

			repoMock.Verify(x => x.CreateAsync(It.IsAny<UserConfigurationDocument>()), Times.Once);
		}
	}
}
