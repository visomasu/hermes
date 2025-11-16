using Xunit;
using Hermes.Storage.Core.InMemory;
using Hermes.Storage.Core.Models;
using Hermes.Storage.Core.Exceptions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hermes.Tests.Storage.Core.InMemory
{
	public class BitFasterStorageClientTests
	{
		private class TestDocument : Document {}

		[Fact]
		public void CanConstructWithCapacity()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			Xunit.Assert.NotNull(client);
		}

		[Fact]
		public async Task CreateAsync_AddsDocument()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			var doc = new TestDocument { Id = "1", PartitionKey = "A" };
			await client.CreateAsync(doc);
			var result = await client.ReadAsync("1", "A");
			Assert.NotNull(result);
			Assert.Equal("1", result.Id);
			Assert.Equal("A", result.PartitionKey);
		}

		[Fact]
		public async Task CreateAsync_ThrowsIfNullOrEmptyId()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			await Assert.ThrowsAsync<StorageException>(() => client.CreateAsync((TestDocument?)null!));
			await Assert.ThrowsAsync<StorageException>(() => client.CreateAsync(new TestDocument { Id = "", PartitionKey = "A" }));
		}

		[Fact]
		public async Task ReadAsync_ReturnsNullIfNotFound()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			var result = await client.ReadAsync("missing", "A");
			Assert.Null(result);
		}

		[Fact]
		public async Task ReadAsync_ThrowsIfKeyInvalid()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			await Assert.ThrowsAsync<StorageException>(() => client.ReadAsync((string?)null!, "A"));
			await Assert.ThrowsAsync<StorageException>(() => client.ReadAsync("", "A"));
		}

		[Fact]
		public async Task UpdateAsync_UpdatesDocument()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			var doc = new TestDocument { Id = "1", PartitionKey = "A" };
			await client.CreateAsync(doc);
			var updated = new TestDocument { Id = "1", PartitionKey = "A", Etag = "new" };
			await client.UpdateAsync("1", updated);
			var result = await client.ReadAsync("1", "A");
			Assert.Equal("new", result?.Etag);
		}

		[Fact]
		public async Task UpdateAsync_ThrowsIfKeyOrItemInvalid()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			await Assert.ThrowsAsync<StorageException>(() => client.UpdateAsync((string?)null!, new TestDocument { Id = "1", PartitionKey = "A" }));
			await Assert.ThrowsAsync<StorageException>(() => client.UpdateAsync("", new TestDocument { Id = "1", PartitionKey = "A" }));
			await Assert.ThrowsAsync<StorageException>(() => client.UpdateAsync("1", (TestDocument?)null!));
			await Assert.ThrowsAsync<StorageException>(() => client.UpdateAsync("1", new TestDocument { Id = "", PartitionKey = "A" }));
		}

		[Fact]
		public async Task DeleteAsync_RemovesDocument()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			var doc = new TestDocument { Id = "1", PartitionKey = "A" };
			await client.CreateAsync(doc);
			await client.DeleteAsync("1", "A");
			var result = await client.ReadAsync("1", "A");
			Assert.Null(result);
		}

		[Fact]
		public async Task DeleteAsync_ThrowsIfKeyInvalid()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			await Assert.ThrowsAsync<StorageException>(() => client.DeleteAsync((string?)null!, "A"));
			await Assert.ThrowsAsync<StorageException>(() => client.DeleteAsync("", "A"));
		}

		[Fact]
		public async Task ReadAllByPartitionKeyAsync_ReturnsAllForPartitionKey()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			var doc1 = new TestDocument { Id = "1", PartitionKey = "A" };
			var doc2 = new TestDocument { Id = "2", PartitionKey = "A" };
			var doc3 = new TestDocument { Id = "3", PartitionKey = "B" };
			await client.CreateAsync(doc1);
			await client.CreateAsync(doc2);
			await client.CreateAsync(doc3);
			var resultsA = await client.ReadAllByPartitionKeyAsync("A") ?? new List<TestDocument>();
			var resultsB = await client.ReadAllByPartitionKeyAsync("B") ?? new List<TestDocument>();
			Assert.Equal(2, resultsA.Count);
			Assert.Single(resultsB);
			Assert.Contains(resultsA, d => d.Id == "1");
			Assert.Contains(resultsA, d => d.Id == "2");
			Assert.Contains(resultsB, d => d.Id == "3");
		}

		[Fact]
		public async Task ReadAllByPartitionKeyAsync_ReturnsEmptyIfNoneFound()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			var results = await client.ReadAllByPartitionKeyAsync("Z") ?? new List<TestDocument>();
			Assert.Empty(results);
		}

		[Fact]
		public async Task ReadAllByPartitionKeyAsync_ThrowsIfPartitionKeyInvalid()
		{
			var client = new BitFasterStorageClient<TestDocument>(100);
			await Assert.ThrowsAsync<StorageException>(() => client.ReadAllByPartitionKeyAsync((string?)null!));
			await Assert.ThrowsAsync<StorageException>(() => client.ReadAllByPartitionKeyAsync(""));
		}
	}
}
