using Hermes.Storage.Core;
using Hermes.Storage.Repositories.ConversationHistory;
using Moq;
using Xunit;

namespace Hermes.Tests.Storage.ConversationHistory
{
    public class ConversationHistoryRepositoryTests
    {
        [Fact]
        public async Task GetConversationHistoryAsync_ReturnsNull_WhenNoDocumentExists()
        {
            // Arrange
            var storageMock = new Mock<IStorageClient<ConversationHistoryDocument, string>>();
            storageMock
                .Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((ConversationHistoryDocument?)null);

            var repo = new ConversationHistoryRepository(storageMock.Object);

            // Act
            var result = await repo.GetConversationHistoryAsync("conv-1");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetConversationHistoryAsync_ReturnsSerializedHistory_WhenDocumentExists()
        {
            // Arrange
            var document = new ConversationHistoryDocument
            {
                Id = "conv-1",
                PartitionKey = "conv-1",
                History = new List<ConversationMessage>
                {
                    new ConversationMessage { Role = "user", Content = "hello" },
                    new ConversationMessage { Role = "assistant", Content = "hi" }
                }
            };

            var storageMock = new Mock<IStorageClient<ConversationHistoryDocument, string>>();
            // Mock expects prefixed partition key from storage layer
            storageMock
                .Setup(s => s.ReadAsync("conv-1", "conv-hist:conv-1"))
                .ReturnsAsync(document);

            var repo = new ConversationHistoryRepository(storageMock.Object);

            // Act
            var result = await repo.GetConversationHistoryAsync("conv-1");

            // Assert
            Assert.NotNull(result);
            Assert.Contains("hello", result!);
            Assert.Contains("assistant", result!);
        }

        [Fact]
        public async Task WriteConversationHistoryAsync_CreatesNewDocument_WhenNoneExists()
        {
            // Arrange
            var storageMock = new Mock<IStorageClient<ConversationHistoryDocument, string>>();
            // Mock ReadAsync expects prefixed partition key
            storageMock
                .Setup(s => s.ReadAsync("conv-1", "conv-hist:conv-1"))
                .ReturnsAsync((ConversationHistoryDocument?)null);

            ConversationHistoryDocument? created = null;
            storageMock
                .Setup(s => s.CreateAsync(It.IsAny<ConversationHistoryDocument>()))
                .Callback<ConversationHistoryDocument>(d => created = d)
                .Returns(Task.CompletedTask);

            var repo = new ConversationHistoryRepository(storageMock.Object);

            var entries = new List<ConversationMessage>
            {
                new ConversationMessage { Role = "user", Content = "msg1" },
                new ConversationMessage { Role = "assistant", Content = "msg2" }
            };

            // Act
            await repo.WriteConversationHistoryAsync("conv-1", entries);

            // Assert
            Assert.NotNull(created);
            Assert.Equal("conv-1", created!.Id);
            // PartitionKey should be prefixed after CreateAsync in RepositoryBase
            Assert.Equal("conv-hist:conv-1", created.PartitionKey);
            Assert.Equal(2, created.History.Count);
            Assert.Equal("msg1", created.History[0].Content);
            Assert.Equal("msg2", created.History[1].Content);
        }

        [Fact]
        public async Task WriteConversationHistoryAsync_AppendsToExistingHistory()
        {
            // Arrange
            var existingDocument = new ConversationHistoryDocument
            {
                Id = "conv-1",
                PartitionKey = "conv-1",
                History = new List<ConversationMessage>
                {
                    new ConversationMessage { Role = "user", Content = "existing" }
                }
            };

            var storageMock = new Mock<IStorageClient<ConversationHistoryDocument, string>>();
            // Mock ReadAsync expects prefixed partition key
            storageMock
                .Setup(s => s.ReadAsync("conv-1", "conv-hist:conv-1"))
                .ReturnsAsync(existingDocument);

            ConversationHistoryDocument? updated = null;
            storageMock
                .Setup(s => s.UpdateAsync("conv-1", It.IsAny<ConversationHistoryDocument>()))
                .Callback<string, ConversationHistoryDocument>((_, d) => updated = d)
                .Returns(Task.CompletedTask);

            var repo = new ConversationHistoryRepository(storageMock.Object);

            var newEntries = new List<ConversationMessage>
            {
                new ConversationMessage { Role = "assistant", Content = "new1" },
                new ConversationMessage { Role = "user", Content = "new2" }
            };

            // Act
            await repo.WriteConversationHistoryAsync("conv-1", newEntries);

            // Assert
            Assert.NotNull(updated);
            Assert.Equal(3, updated!.History.Count);
            Assert.Equal("existing", updated.History[0].Content);
            Assert.Equal("new1", updated.History[1].Content);
            Assert.Equal("new2", updated.History[2].Content);
        }
    }
}
