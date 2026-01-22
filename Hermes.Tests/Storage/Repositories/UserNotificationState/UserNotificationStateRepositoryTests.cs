using Hermes.Storage.Core;
using Hermes.Storage.Core.Exceptions;
using Hermes.Storage.Repositories.UserNotificationState;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.Tests.Storage.Repositories.UserNotificationState
{
	public class UserNotificationStateRepositoryTests
	{
		private readonly Mock<IStorageClient<UserNotificationStateDocument, string>> _mockStorageClient;
		private readonly Mock<ILogger<UserNotificationStateRepository>> _mockLogger;
		private readonly UserNotificationStateRepository _repository;

		public UserNotificationStateRepositoryTests()
		{
			_mockStorageClient = new Mock<IStorageClient<UserNotificationStateDocument, string>>();
			_mockLogger = new Mock<ILogger<UserNotificationStateRepository>>();
			_repository = new UserNotificationStateRepository(_mockStorageClient.Object, _mockLogger.Object);
		}

		[Fact]
		public async Task GetOrCreateAsync_ExistingDocument_ReturnsDocument()
		{
			// Arrange
			var teamsUserId = "user-123";
			var existingDoc = new UserNotificationStateDocument
			{
				Id = "user-123", // RepositoryBase prefixes with ObjectTypeCode
				PartitionKey = "user-notif-state:user-123",
				TeamsUserId = teamsUserId,
				RecentNotifications = new List<NotificationEvent>(),
				TotalNotificationsSent = 5
			};

			_mockStorageClient
				.Setup(c => c.ReadAsync("user-123", "user-notif-state:user-123"))
				.ReturnsAsync(existingDoc);

			// Act
			var result = await _repository.GetOrCreateAsync(teamsUserId);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(teamsUserId, result.TeamsUserId);
			Assert.Equal(5, result.TotalNotificationsSent);
			_mockStorageClient.Verify(c => c.CreateAsync(It.IsAny<UserNotificationStateDocument>()), Times.Never);
		}

		[Fact]
		public async Task GetOrCreateAsync_NonExistingDocument_CreatesAndReturnsNewDocument()
		{
			// Arrange
			var teamsUserId = "user-123";

			_mockStorageClient
				.Setup(c => c.ReadAsync("user-123", "user-notif-state:user-123"))
				.ReturnsAsync((UserNotificationStateDocument?)null);

			_mockStorageClient
				.Setup(c => c.CreateAsync(It.IsAny<UserNotificationStateDocument>()))
				.Returns(Task.CompletedTask);

			// Act
			var result = await _repository.GetOrCreateAsync(teamsUserId);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("user-123", result.Id); // RepositoryBase prefixes with ObjectTypeCode
			Assert.Equal("user-notif-state:user-123", result.PartitionKey);
			Assert.Equal(teamsUserId, result.TeamsUserId);
			Assert.Empty(result.RecentNotifications);
			Assert.Equal(0, result.TotalNotificationsSent);
			_mockStorageClient.Verify(c => c.CreateAsync(It.IsAny<UserNotificationStateDocument>()), Times.Once);
		}

		[Fact]
		public async Task GetOrCreateAsync_EmptyTeamsUserId_ThrowsStorageException()
		{
			// Act & Assert
			await Assert.ThrowsAsync<StorageException>(() => _repository.GetOrCreateAsync(""));
		}

		[Fact]
		public async Task RecordNotificationAsync_Success_AddsEventAndUpdatesDocument()
		{
			// Arrange
			var teamsUserId = "user-123";
			var notificationType = "SlaViolation";
			var deduplicationKey = "test-key";

			var existingDoc = new UserNotificationStateDocument
			{
				Id = "user-123",
				PartitionKey = "user-notif-state:user-123",
				TeamsUserId = teamsUserId,
				RecentNotifications = new List<NotificationEvent>(),
				TotalNotificationsSent = 0,
				Etag = "etag-123"
			};

			_mockStorageClient
				.Setup(c => c.ReadAsync("user-123", "user-notif-state:user-123"))
				.ReturnsAsync(existingDoc);

			_mockStorageClient
				.Setup(c => c.UpdateAsync("user-123", It.IsAny<UserNotificationStateDocument>()))
				.Returns(Task.CompletedTask);

			// Act
			await _repository.RecordNotificationAsync(
				teamsUserId,
				notificationType,
				deduplicationKey,
				workItemId: 123,
				areaPath: "Project\\Area");

			// Assert
			_mockStorageClient.Verify(
				c => c.UpdateAsync("user-123", It.Is<UserNotificationStateDocument>(doc =>
					doc.RecentNotifications.Count == 1 &&
					doc.RecentNotifications[0].NotificationType == notificationType &&
					doc.RecentNotifications[0].DeduplicationKey == deduplicationKey &&
					doc.RecentNotifications[0].WorkItemId == 123 &&
					doc.RecentNotifications[0].AreaPath == "Project\\Area" &&
					doc.TotalNotificationsSent == 1)),
				Times.Once);
		}

		[Fact]
		public async Task RecordNotificationAsync_CleansUpOldEvents()
		{
			// Arrange
			var teamsUserId = "user-123";
			var oldEvent = new NotificationEvent
			{
				SentAt = DateTime.UtcNow.AddDays(-2), // Older than 24 hours
				NotificationType = "OldType"
			};
			var recentEvent = new NotificationEvent
			{
				SentAt = DateTime.UtcNow.AddHours(-12), // Within 24 hours
				NotificationType = "RecentType"
			};

			var existingDoc = new UserNotificationStateDocument
			{
				Id = "user-123",
				PartitionKey = "user-notif-state:user-123",
				TeamsUserId = teamsUserId,
				RecentNotifications = new List<NotificationEvent> { oldEvent, recentEvent },
				TotalNotificationsSent = 2,
				Etag = "etag-123"
			};

			_mockStorageClient
				.Setup(c => c.ReadAsync("user-123", "user-notif-state:user-123"))
				.ReturnsAsync(existingDoc);

			_mockStorageClient
				.Setup(c => c.UpdateAsync("user-123", It.IsAny<UserNotificationStateDocument>()))
				.Returns(Task.CompletedTask);

			// Act
			await _repository.RecordNotificationAsync(teamsUserId, "NewType");

			// Assert
			_mockStorageClient.Verify(
				c => c.UpdateAsync("user-123", It.Is<UserNotificationStateDocument>(doc =>
					doc.RecentNotifications.Count == 2 && // Recent event + new event (old cleaned up)
					doc.RecentNotifications.All(e => e.SentAt >= DateTime.UtcNow.AddDays(-1)))),
				Times.Once);
		}


		[Fact]
		public async Task GetNotificationsSinceAsync_ReturnsFilteredNotifications()
		{
			// Arrange
			var teamsUserId = "user-123";
			var cutoff = DateTime.UtcNow.AddHours(-6);

			var oldEvent = new NotificationEvent
			{
				SentAt = cutoff.AddHours(-1),
				NotificationType = "OldType"
			};
			var recentEvent1 = new NotificationEvent
			{
				SentAt = cutoff.AddHours(1),
				NotificationType = "RecentType1"
			};
			var recentEvent2 = new NotificationEvent
			{
				SentAt = cutoff.AddHours(2),
				NotificationType = "RecentType2"
			};

			var doc = new UserNotificationStateDocument
			{
				Id = "user-123",
				PartitionKey = "user-notif-state:user-123",
				TeamsUserId = teamsUserId,
				RecentNotifications = new List<NotificationEvent> { oldEvent, recentEvent1, recentEvent2 },
				TotalNotificationsSent = 3
			};

			_mockStorageClient
				.Setup(c => c.ReadAsync("user-123", "user-notif-state:user-123"))
				.ReturnsAsync(doc);

			// Act
			var result = await _repository.GetNotificationsSinceAsync(teamsUserId, cutoff);

			// Assert
			Assert.Equal(2, result.Count);
			Assert.All(result, e => Assert.True(e.SentAt >= cutoff));
			Assert.Equal("RecentType2", result[0].NotificationType); // Should be ordered descending
			Assert.Equal("RecentType1", result[1].NotificationType);
		}

		[Fact]
		public async Task GetNotificationsSinceAsync_NoDocument_ReturnsEmptyList()
		{
			// Arrange
			var teamsUserId = "user-123";

			_mockStorageClient
				.Setup(c => c.ReadAsync("user-123", "user-notif-state:user-123"))
				.ReturnsAsync((UserNotificationStateDocument?)null);

			// Act
			var result = await _repository.GetNotificationsSinceAsync(teamsUserId, DateTime.UtcNow);

			// Assert
			Assert.Empty(result);
		}

		[Fact]
		public async Task GetNotificationsSinceAsync_EmptyUserId_ReturnsEmptyList()
		{
			// Act
			var result = await _repository.GetNotificationsSinceAsync("", DateTime.UtcNow);

			// Assert
			Assert.Empty(result);
			_mockStorageClient.Verify(c => c.ReadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}
	}
}
