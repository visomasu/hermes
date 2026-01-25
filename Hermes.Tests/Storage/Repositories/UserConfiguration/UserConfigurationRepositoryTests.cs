using Hermes.Storage.Core;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.Tests.Storage.Repositories.UserConfiguration
{
	public class UserConfigurationRepositoryTests
	{
		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsNull_WhenNoDocumentExists()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<UserConfigurationDocument, string>>();
			storageMock
				.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()))
				.ReturnsAsync((UserConfigurationDocument?)null);

			var loggerMock = new Mock<ILogger<UserConfigurationRepository>>();
			var repo = new UserConfigurationRepository(storageMock.Object, loggerMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync("user-123");

			// Assert
			Assert.Null(result);
			// Repository should prefix the partition key with ObjectTypeCode
			storageMock.Verify(s => s.ReadAsync("user-123", "user-config:user-123"), Times.Once);
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsDocument_WhenExists()
		{
			// Arrange
			var document = new UserConfigurationDocument
			{
				Id = "user-123",
				PartitionKey = "user-123",
				TeamsUserId = "user-123",
				Notifications = new NotificationPreferences
				{
					SlaViolationNotifications = true,
					MaxNotificationsPerHour = 5,
					MaxNotificationsPerDay = 20
				}
			};

			var storageMock = new Mock<IStorageClient<UserConfigurationDocument, string>>();
			// Mock expects prefixed partition key from storage layer
			storageMock
				.Setup(s => s.ReadAsync("user-123", "user-config:user-123"))
				.ReturnsAsync(document);

			var loggerMock = new Mock<ILogger<UserConfigurationRepository>>();
			var repo = new UserConfigurationRepository(storageMock.Object, loggerMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync("user-123");

			// Assert
			Assert.NotNull(result);
			Assert.Equal("user-123", result.TeamsUserId);
			Assert.True(result.Notifications.SlaViolationNotifications);
			Assert.Equal(5, result.Notifications.MaxNotificationsPerHour);
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsNull_WhenTeamsUserIdIsNull()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<UserConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<UserConfigurationRepository>>();
			var repo = new UserConfigurationRepository(storageMock.Object, loggerMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync(null!);

			// Assert
			Assert.Null(result);
			storageMock.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsNull_WhenTeamsUserIdIsEmpty()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<UserConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<UserConfigurationRepository>>();
			var repo = new UserConfigurationRepository(storageMock.Object, loggerMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync(string.Empty);

			// Assert
			Assert.Null(result);
			storageMock.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsNull_WhenTeamsUserIdIsWhitespace()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<UserConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<UserConfigurationRepository>>();
			var repo = new UserConfigurationRepository(storageMock.Object, loggerMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync("   ");

			// Assert
			Assert.Null(result);
			storageMock.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		#region Phase 2: SLA Registration Schema Extension Tests

		[Fact]
		public async Task GetAllWithSlaRegistrationAsync_ReturnsEmptyList()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<UserConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<UserConfigurationRepository>>();
			var repo = new UserConfigurationRepository(storageMock.Object, loggerMock.Object);

			// Act
			var result = await repo.GetAllWithSlaRegistrationAsync();

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
			// Storage client should not be called (placeholder implementation)
			storageMock.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task GetAllWithSlaRegistrationAsync_LogsWarning()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<UserConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<UserConfigurationRepository>>();
			var repo = new UserConfigurationRepository(storageMock.Object, loggerMock.Object);

			// Act
			var result = await repo.GetAllWithSlaRegistrationAsync();

			// Assert
			// Verify warning was logged about not implemented
			loggerMock.Verify(
				x => x.Log(
					LogLevel.Warning,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cross-partition query not yet implemented")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);
		}

		[Fact]
		public void WorkItemUpdateSlaRegistrationProfile_IsManager_TrueWhenHasDirectReports()
		{
			// Arrange & Act
			var profile = new WorkItemUpdateSlaRegistrationProfile
			{
				AzureDevOpsEmail = "manager@example.com",
				DirectReportEmails = new List<string>
				{
					"report1@example.com",
					"report2@example.com",
					"report3@example.com"
				}
			};

			// Assert
			Assert.True(profile.IsManager);
			Assert.Equal(3, profile.DirectReportEmails.Count);
		}

		[Fact]
		public void WorkItemUpdateSlaRegistrationProfile_IsManager_FalseWhenNoDirectReports()
		{
			// Arrange & Act
			var profile = new WorkItemUpdateSlaRegistrationProfile
			{
				AzureDevOpsEmail = "ic@example.com",
				DirectReportEmails = new List<string>() // Empty list = IC
			};

			// Assert
			Assert.False(profile.IsManager);
			Assert.Empty(profile.DirectReportEmails);
		}

		[Fact]
		public void UserConfigurationDocument_SlaRegistration_NullByDefault()
		{
			// Arrange & Act
			var document = new UserConfigurationDocument
			{
				Id = "user-123",
				PartitionKey = "user-123",
				TeamsUserId = "user-123"
			};

			// Assert
			Assert.Null(document.SlaRegistration); // Null = never registered for SLA notifications
			Assert.NotNull(document.Notifications); // Other properties initialize properly
			Assert.Equal("user-123", document.TeamsUserId);
		}

		[Fact]
		public void WorkItemUpdateSlaRegistrationProfile_DefaultValues_AreCorrect()
		{
			// Arrange & Act
			var profile = new WorkItemUpdateSlaRegistrationProfile();

			// Assert
			Assert.False(profile.IsRegistered); // Default to false (explicit opt-in)
			Assert.Empty(profile.AzureDevOpsEmail);
			Assert.NotNull(profile.DirectReportEmails);
			Assert.Empty(profile.DirectReportEmails);
			Assert.False(profile.IsManager); // Derived from empty DirectReportEmails
			Assert.Null(profile.DirectReportsLastRefreshedAt); // Nullable until first refresh
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_WithSlaRegistration_ReturnsCompleteDocument()
		{
			// Arrange
			var document = new UserConfigurationDocument
			{
				Id = "user-456",
				PartitionKey = "user-456",
				TeamsUserId = "user-456",
				Notifications = new NotificationPreferences
				{
					SlaViolationNotifications = true
				},
				SlaRegistration = new WorkItemUpdateSlaRegistrationProfile
				{
					IsRegistered = true,
					AzureDevOpsEmail = "manager@example.com",
					DirectReportEmails = new List<string>
					{
						"report1@example.com",
						"report2@example.com"
					},
					RegisteredAt = DateTime.UtcNow.AddDays(-7),
					DirectReportsLastRefreshedAt = DateTime.UtcNow.AddDays(-1)
				}
			};

			var storageMock = new Mock<IStorageClient<UserConfigurationDocument, string>>();
			storageMock
				.Setup(s => s.ReadAsync("user-456", "user-config:user-456"))
				.ReturnsAsync(document);

			var loggerMock = new Mock<ILogger<UserConfigurationRepository>>();
			var repo = new UserConfigurationRepository(storageMock.Object, loggerMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync("user-456");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.SlaRegistration);
			Assert.True(result.SlaRegistration.IsRegistered);
			Assert.Equal("manager@example.com", result.SlaRegistration.AzureDevOpsEmail);
			Assert.Equal(2, result.SlaRegistration.DirectReportEmails.Count);
			Assert.True(result.SlaRegistration.IsManager); // Computed property
		}

		#endregion
	}
}
