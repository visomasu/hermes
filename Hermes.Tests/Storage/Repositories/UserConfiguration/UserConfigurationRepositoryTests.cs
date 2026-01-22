using Hermes.Storage.Core;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;
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

			var repo = new UserConfigurationRepository(storageMock.Object);

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

			var repo = new UserConfigurationRepository(storageMock.Object);

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
			var repo = new UserConfigurationRepository(storageMock.Object);

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
			var repo = new UserConfigurationRepository(storageMock.Object);

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
			var repo = new UserConfigurationRepository(storageMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync("   ");

			// Assert
			Assert.Null(result);
			storageMock.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}
	}
}
