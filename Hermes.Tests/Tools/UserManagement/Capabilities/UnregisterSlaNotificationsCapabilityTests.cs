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
	public class UnregisterSlaNotificationsCapabilityTests
	{
		[Fact]
		public async Task ExecuteAsync_RegisteredUser_UnregistersSuccessfully()
		{
			// Arrange
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<UnregisterSlaNotificationsCapability>>();

			var userConfig = new UserConfigurationDocument
			{
				Id = "user-123",
				PartitionKey = "user-123",
				TeamsUserId = "user-123",
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = "user@example.com",
					DirectReportEmails = new List<string>()
				},
				Notifications = new NotificationPreferences
				{
					SlaViolationNotifications = true
				}
			};

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfig);

			repoMock.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UserConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			var capability = new UnregisterSlaNotificationsCapability(repoMock.Object, loggerMock.Object);
			var input = new UnregisterSlaNotificationsCapabilityInput { TeamsUserId = "user-123" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.True(response.GetProperty("success").GetBoolean());
			Assert.Contains("Unregistered successfully", response.GetProperty("message").GetString()!);

			repoMock.Verify(x => x.UpdateAsync("user-123", It.Is<UserConfigurationDocument>(doc =>
				!doc.SlaRegistration!.IsRegistered &&
				!doc.Notifications.SlaViolationNotifications
			)), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_NotRegisteredUser_ReturnsError()
		{
			// Arrange
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<UnregisterSlaNotificationsCapability>>();

			var userConfig = new UserConfigurationDocument
			{
				Id = "user-456",
				PartitionKey = "user-456",
				TeamsUserId = "user-456",
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = false, // Already unregistered
					AzureDevOpsEmail = "user@example.com"
				}
			};

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfig);

			var capability = new UnregisterSlaNotificationsCapability(repoMock.Object, loggerMock.Object);
			var input = new UnregisterSlaNotificationsCapabilityInput { TeamsUserId = "user-456" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("not currently registered", response.GetProperty("message").GetString()!);

			repoMock.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_UserWithNoSlaRegistration_ReturnsError()
		{
			// Arrange
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<UnregisterSlaNotificationsCapability>>();

			var userConfig = new UserConfigurationDocument
			{
				Id = "user-789",
				PartitionKey = "user-789",
				TeamsUserId = "user-789",
				SlaRegistration = null // Never registered
			};

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(userConfig);

			var capability = new UnregisterSlaNotificationsCapability(repoMock.Object, loggerMock.Object);
			var input = new UnregisterSlaNotificationsCapabilityInput { TeamsUserId = "user-789" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());

			repoMock.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_UserNotFound_ReturnsError()
		{
			// Arrange
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<UnregisterSlaNotificationsCapability>>();

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			var capability = new UnregisterSlaNotificationsCapability(repoMock.Object, loggerMock.Object);
			var input = new UnregisterSlaNotificationsCapabilityInput { TeamsUserId = "user-notfound" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());

			repoMock.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UserConfigurationDocument>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_EmptyTeamsUserId_ReturnsError()
		{
			// Arrange
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<UnregisterSlaNotificationsCapability>>();

			var capability = new UnregisterSlaNotificationsCapability(repoMock.Object, loggerMock.Object);
			var input = new UnregisterSlaNotificationsCapabilityInput { TeamsUserId = string.Empty };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("required", response.GetProperty("message").GetString()!.ToLower());

			repoMock.Verify(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task ExecuteAsync_RepositoryFailure_ReturnsError()
		{
			// Arrange
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<UnregisterSlaNotificationsCapability>>();

			repoMock.Setup(x => x.GetByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("Database error"));

			var capability = new UnregisterSlaNotificationsCapability(repoMock.Object, loggerMock.Object);
			var input = new UnregisterSlaNotificationsCapabilityInput { TeamsUserId = "user-error" };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			var response = JsonSerializer.Deserialize<JsonElement>(result);
			Assert.False(response.GetProperty("success").GetBoolean());
			Assert.Contains("error occurred", response.GetProperty("message").GetString()!);
		}

		[Fact]
		public void Name_ReturnsCorrectName()
		{
			// Arrange
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<UnregisterSlaNotificationsCapability>>();

			var capability = new UnregisterSlaNotificationsCapability(repoMock.Object, loggerMock.Object);

			// Act
			var name = capability.Name;

			// Assert
			Assert.Equal("UnregisterSlaNotifications", name);
		}

		[Fact]
		public void Description_ReturnsCorrectDescription()
		{
			// Arrange
			var repoMock = new Mock<IUserConfigurationRepository>();
			var loggerMock = new Mock<ILogger<UnregisterSlaNotificationsCapability>>();

			var capability = new UnregisterSlaNotificationsCapability(repoMock.Object, loggerMock.Object);

			// Act
			var description = capability.Description;

			// Assert
			Assert.NotNull(description);
			Assert.Contains("Unregister", description);
			Assert.Contains("SLA", description);
		}
	}
}
