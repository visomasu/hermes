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
			storageMock.Verify(s => s.ReadAllByPartitionKeyAsync("user-123"), Times.Once);
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_ReturnsDocument_WhenExists()
		{
			// Arrange
			var document = new ConversationReferenceDocument
			{
				Id = "user-123",
				PartitionKey = "user-123",
				TeamsUserId = "user-123",
				ConversationReferenceJson = "{\"activityId\":\"123\"}",
				IsActive = true,
				ConsecutiveFailureCount = 0
			};

			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			storageMock
				.Setup(s => s.ReadAllByPartitionKeyAsync("user-123"))
				.ReturnsAsync(new List<ConversationReferenceDocument> { document });

			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			var result = await repo.GetByTeamsUserIdAsync("user-123");

			// Assert
			Assert.NotNull(result);
			Assert.Equal("user-123", result.TeamsUserId);
			Assert.Equal("{\"activityId\":\"123\"}", result.ConversationReferenceJson);
			Assert.True(result.IsActive);
			Assert.Equal(0, result.ConsecutiveFailureCount);
		}

		[Fact]
		public async Task GetByTeamsUserIdAsync_PassesCorrectPartitionKey()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<ConversationReferenceDocument, string>>();
			storageMock
				.Setup(s => s.ReadAllByPartitionKeyAsync(It.IsAny<string>()))
				.ReturnsAsync((List<ConversationReferenceDocument>?)null);

			var repo = new ConversationReferenceRepository(storageMock.Object);

			// Act
			await repo.GetByTeamsUserIdAsync("user-456");

			// Assert
			storageMock.Verify(s => s.ReadAllByPartitionKeyAsync("user-456"), Times.Once);
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
