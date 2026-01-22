using Hermes.Storage.Core;
using Hermes.Storage.Repositories.ConversationReference;
using Moq;
using Xunit;

namespace Hermes.Tests.Storage.Repositories.ConversationReference
{
	public class ConversationReferenceRepositoryTests
	{
		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsNull_WhenNoDocumentExists()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			storageMock
				.Setup(s => s.ReadAllByPartitionKeyAsync(It.IsAny<string>()))
				.ReturnsAsync((List<ConversationReferenceDocument>?)null);

			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync("user-123");

			// Assert
			Assert.Null(result);
			storageMock.Verify(s => s.ReadAllByPartitionKeyAsync("conv:user-123"), Times.Once);
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsNull_WhenEmptyListReturned()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			storageMock
				.Setup(s => s.ReadAllByPartitionKeyAsync(It.IsAny<string>()))
				.ReturnsAsync(new List<ConversationReferenceDocument>());

			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync("user-123");

			// Assert
			Assert.Null(result);
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsMostRecentActiveConversation()
		{
			// Arrange
			var olderConversation = new ConversationReferenceDocument
			{
				Id = "conv-1",
				PartitionKey = "user-123",
				TeamsUserId = "user-123",
				ConversationId = "conv-1",
				ConversationReferenceJson = "{\"activityId\":\"123\"}",
				LastInteractionAt = DateTime.UtcNow.AddHours(-2),
				IsActive = true,
				ConsecutiveFailureCount = 0
			};

			var newerConversation = new ConversationReferenceDocument
			{
				Id = "conv-2",
				PartitionKey = "user-123",
				TeamsUserId = "user-123",
				ConversationId = "conv-2",
				ConversationReferenceJson = "{\"activityId\":\"456\"}",
				LastInteractionAt = DateTime.UtcNow.AddHours(-1),
				IsActive = true,
				ConsecutiveFailureCount = 0
			};

			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			storageMock
				.Setup(s => s.ReadAllByPartitionKeyAsync("conv:user-123"))
				.ReturnsAsync(new List<ConversationReferenceDocument> { olderConversation, newerConversation });

			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync("user-123");

			// Assert
			Assert.NotNull(result);
			Assert.Equal("conv-2", result.ConversationId); // Should return the newer one
			Assert.Equal(newerConversation.LastInteractionAt, result.LastInteractionAt);
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_IgnoresInactiveConversations()
		{
			// Arrange
			var inactiveConversation = new ConversationReferenceDocument
			{
				Id = "conv-1",
				PartitionKey = "user-123",
				TeamsUserId = "user-123",
				ConversationId = "conv-1",
				ConversationReferenceJson = "{\"activityId\":\"123\"}",
				LastInteractionAt = DateTime.UtcNow.AddHours(-1),
				IsActive = false, // Inactive
				ConsecutiveFailureCount = 5
			};

			var activeConversation = new ConversationReferenceDocument
			{
				Id = "conv-2",
				PartitionKey = "user-123",
				TeamsUserId = "user-123",
				ConversationId = "conv-2",
				ConversationReferenceJson = "{\"activityId\":\"456\"}",
				LastInteractionAt = DateTime.UtcNow.AddHours(-2),
				IsActive = true,
				ConsecutiveFailureCount = 0
			};

			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			storageMock
				.Setup(s => s.ReadAllByPartitionKeyAsync("conv:user-123"))
				.ReturnsAsync(new List<ConversationReferenceDocument> { inactiveConversation, activeConversation });

			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync("user-123");

			// Assert
			Assert.NotNull(result);
			Assert.Equal("conv-2", result.ConversationId); // Should return the active one, not the inactive one
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsNull_WhenTeamsUserIdIsNull()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync(null!);

			// Assert
			Assert.Null(result);
			storageMock.Verify(s => s.ReadAllByPartitionKeyAsync(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsNull_WhenTeamsUserIdIsEmpty()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync(string.Empty);

			// Assert
			Assert.Null(result);
			storageMock.Verify(s => s.ReadAllByPartitionKeyAsync(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task GetAllByTeamsUserIdAsync_ReturnsAllConversations()
		{
			// Arrange
			var conversation1 = new ConversationReferenceDocument
			{
				Id = "conv-1",
				PartitionKey = "user-123",
				TeamsUserId = "user-123",
				ConversationId = "conv-1",
				ConversationReferenceJson = "{\"activityId\":\"123\"}",
				IsActive = true
			};

			var conversation2 = new ConversationReferenceDocument
			{
				Id = "conv-2",
				PartitionKey = "user-123",
				TeamsUserId = "user-123",
				ConversationId = "conv-2",
				ConversationReferenceJson = "{\"activityId\":\"456\"}",
				IsActive = false
			};

			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			storageMock
				.Setup(s => s.ReadAllByPartitionKeyAsync("conv:user-123"))
				.ReturnsAsync(new List<ConversationReferenceDocument> { conversation1, conversation2 });

			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			var result = await repo.GetAllByTeamsUserIdAsync("user-123");

			// Assert
			Assert.NotNull(result);
			Assert.Equal(2, result.Count);
			Assert.Contains(result, c => c.ConversationId == "conv-1");
			Assert.Contains(result, c => c.ConversationId == "conv-2");
		}

		[Fact]
		public async Task GetAllByTeamsUserIdAsync_ReturnsEmptyList_WhenNoConversations()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			storageMock
				.Setup(s => s.ReadAllByPartitionKeyAsync(It.IsAny<string>()))
				.ReturnsAsync((List<ConversationReferenceDocument>?)null);

			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			var result = await repo.GetAllByTeamsUserIdAsync("user-123");

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
		}

		[Fact]
		public async Task GetByConversationIdAsync_ThrowsNotImplementedException()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act & Assert
			await Assert.ThrowsAsync<NotImplementedException>(
				() => repo.GetByConversationIdAsync("conv-123"));
		}

		[Fact]
		public async Task GetActiveReferencesAsync_ReturnsEmptyList()
		{
			// Arrange
			// Note: GetActiveReferencesAsync is a placeholder that returns empty list (requires cross-partition query)
			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			var result = await repo.GetActiveReferencesAsync();

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
		}

		[Fact]
		public void Constructor_InitializesWithStorage()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();

			// Act
			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Assert
			Assert.NotNull(repo);
		}
	}
}
