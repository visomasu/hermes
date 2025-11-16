using Hermes.Storage.Core.CosmosDB;
using Hermes.Storage.Core.Models;
using Microsoft.Azure.Cosmos;
using Moq;
using Xunit;

namespace Hermes.Tests.Storage.Core.CosmosDB
{
	public class CosmosDbStorageClientTests
	{
		public class TestDocument : Document {}

		[Fact]
		public void CanConstructWithConnectionString()
		{
			// Use a valid Base64 string for AccountKey
			var client = new CosmosDbStorageClient<TestDocument>("AccountEndpoint=https://localhost:8081/;AccountKey=MTIzNDU2Nzg5MA==;", "db", "container");
			Xunit.Assert.NotNull(client);
		}

		[Fact]
		public async Task CreateAsync_CreatesDocument()
		{
			var doc = new TestDocument { Id = "1", PartitionKey = "A" };
			var mockContainer = new Mock<Container>();
			mockContainer.Setup(c => c.CreateItemAsync(doc, It.Is<PartitionKey>(pk => pk.ToString() == $"[\"{doc.PartitionKey}\"]"), null, default))
				.ReturnsAsync(new Mock<ItemResponse<TestDocument>>().Object)
				.Verifiable();

			var client = new CosmosDbStorageClient<TestDocument>(mockContainer.Object);
			await client.CreateAsync(doc);

			mockContainer.Verify();
		}

		[Fact]
		public async Task ReadAsync_ReturnsDocument_WhenFound()
		{
			var doc = new TestDocument { Id = "1", PartitionKey = "A" };
			var mockResponse = new Mock<ItemResponse<TestDocument>>();
			mockResponse.Setup(r => r.Resource).Returns(doc);

			var mockContainer = new Mock<Container>();
			mockContainer.Setup(c => c.ReadItemAsync<TestDocument>(doc.Id, It.Is<PartitionKey>(pk => pk.ToString() == $"[\"{doc.PartitionKey}\"]"), null, default))
				.ReturnsAsync(mockResponse.Object);

			var client = new CosmosDbStorageClient<TestDocument>(mockContainer.Object);
			var result = await client.ReadAsync(doc.Id, doc.PartitionKey);

			Assert.NotNull(result);
			Assert.Equal(doc.Id, result.Id);
			Assert.Equal(doc.PartitionKey, result.PartitionKey);
		}

		[Fact]
		public async Task ReadAsync_ReturnsNull_WhenNotFound()
		{
			var mockContainer = new Mock<Container>();
			mockContainer.Setup(c => c.ReadItemAsync<TestDocument>(It.IsAny<string>(), It.IsAny<PartitionKey>(), null, default))
				.ThrowsAsync(new CosmosException("Not found", System.Net.HttpStatusCode.NotFound, 0, "", 0));

			var client = new CosmosDbStorageClient<TestDocument>(mockContainer.Object);
			var result = await client.ReadAsync("missing", "A");

			Assert.Null(result);
		}

		[Fact]
		public async Task UpdateAsync_UpsertsDocument()
		{
			var doc = new TestDocument { Id = "1", PartitionKey = "A" };
			var mockContainer = new Mock<Container>();
			mockContainer.Setup(c => c.UpsertItemAsync(doc, It.Is<PartitionKey>(pk => pk.ToString() == $"[\"{doc.PartitionKey}\"]"), null, default))
				.ReturnsAsync(new Mock<ItemResponse<TestDocument>>().Object)
				.Verifiable();

			var client = new CosmosDbStorageClient<TestDocument>(mockContainer.Object);
			await client.UpdateAsync(doc.Id, doc);

			mockContainer.Verify();
		}

		[Fact]
		public async Task DeleteAsync_DeletesDocument()
		{
			var doc = new TestDocument { Id = "1", PartitionKey = "A" };
			var mockContainer = new Mock<Container>();
			mockContainer.Setup(c => c.DeleteItemAsync<TestDocument>(doc.Id, It.Is<PartitionKey>(pk => pk.ToString() == $"[\"{doc.PartitionKey}\"]"), null, default))
				.ReturnsAsync(new Mock<ItemResponse<TestDocument>>().Object)
				.Verifiable();

			var client = new CosmosDbStorageClient<TestDocument>(mockContainer.Object);
			await client.DeleteAsync(doc.Id, doc.PartitionKey);

			mockContainer.Verify();
		}

        [Fact]
        public async Task CanRetrieveAllRecordsByPartitionKey()
        {
            // Arrange
            var docs = new List<TestDocument> {
                new TestDocument { Id = "1", PartitionKey = "A" },
                new TestDocument { Id = "2", PartitionKey = "A" },
                new TestDocument { Id = "3", PartitionKey = "B" }
            };

            // Helper to create a mock FeedResponse<T> that implements IEnumerable<T>
            Mock<FeedResponse<TestDocument>> CreateFeedResponse(IEnumerable<TestDocument> items)
            {
                var mock = new Mock<FeedResponse<TestDocument>>();
                mock.Setup(fr => fr.GetEnumerator()).Returns(items.GetEnumerator());
                mock.As<IEnumerable<TestDocument>>().Setup(m => m.GetEnumerator()).Returns(items.GetEnumerator());
                mock.Setup(fr => fr.Count).Returns(items.Count());
                return mock;
            }

            var feedResponsesA = CreateFeedResponse(docs.Where(d => d.PartitionKey == "A"));
            var feedResponsesB = CreateFeedResponse(docs.Where(d => d.PartitionKey == "B"));
            var emptyFeedResponse = CreateFeedResponse(Enumerable.Empty<TestDocument>());

            // PartitionKey A iterator
            var mockFeedIteratorA = new Mock<FeedIterator<TestDocument>>();
            mockFeedIteratorA.SetupSequence(fi => fi.HasMoreResults)
                .Returns(true)
                .Returns(false);
            mockFeedIteratorA.SetupSequence(fi => fi.ReadNextAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(feedResponsesA.Object)
                .ReturnsAsync(emptyFeedResponse.Object);

            // PartitionKey B iterator
            var mockFeedIteratorB = new Mock<FeedIterator<TestDocument>>();
            mockFeedIteratorB.SetupSequence(fi => fi.HasMoreResults)
                .Returns(true)
                .Returns(false);
            mockFeedIteratorB.SetupSequence(fi => fi.ReadNextAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(feedResponsesB.Object)
                .ReturnsAsync(emptyFeedResponse.Object);

            var mockContainer = new Mock<Container>();
            mockContainer.Setup(c => c.GetItemQueryIterator<TestDocument>(It.Is<QueryDefinition>(q => q.GetQueryParameters()[0].Value.ToString() == "A"), null, null))
                .Returns(mockFeedIteratorA.Object);
            mockContainer.Setup(c => c.GetItemQueryIterator<TestDocument>(It.Is<QueryDefinition>(q => q.GetQueryParameters()[0].Value.ToString() == "B"), null, null))
                .Returns(mockFeedIteratorB.Object);

            var client = new CosmosDbStorageClient<TestDocument>(mockContainer.Object);

            // Act
            var resultsA = await client.ReadAllByPartitionKeyAsync("A");
            var resultsB = await client.ReadAllByPartitionKeyAsync("B");

            // Assert
            Assert.Equal(2, resultsA?.Count);
            Assert.Single(resultsB ?? new List<TestDocument>());
            Assert.Contains(resultsA ?? new List<TestDocument>(), d => d.Id == "1");
            Assert.Contains(resultsA ?? new List<TestDocument>(), d => d.Id == "2");
            Assert.Contains(resultsB ?? new List<TestDocument>(), d => d.Id == "3");
        }
    }
}
