using Hermes.Storage.Core;
using Hermes.Storage.Core.Models;
using Hermes.Storage.Repositories;
using Moq;
using Xunit;
using Hermes.Storage.Core.Exceptions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hermes.Tests.Storage.Repositories
{
	public class RepositoryBaseTests
	{
		private class TestRepository : RepositoryBase<TestDocument>
		{
			public TestRepository(IStorageClient<TestDocument, string> storage) : base(storage) {}
		}

		[Fact]
		public async Task CanCallCrudMethods()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			var doc = new TestDocument { Id = "test-id" };
			await repo.CreateAsync(doc);
			await repo.ReadAsync("id", "partitionKey");
			await repo.UpdateAsync("id", new TestDocument { Id = "test-id" });
			await repo.DeleteAsync("id", "partitionKey");
			Assert.True(true);
		}

		[Fact]
		public async Task CreateAsync_CallsStorageWithValidEntity()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			var doc = new TestDocument { Id = "id" };
			await repo.CreateAsync(doc);
			mock.Verify(s => s.CreateAsync(doc), Times.Once);
		}

		[Fact]
		public async Task CreateAsync_ThrowsIfEntityInvalid()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			await Assert.ThrowsAsync<StorageException>(() => repo.CreateAsync((TestDocument?)null!));
			await Assert.ThrowsAsync<StorageException>(() => repo.CreateAsync(new TestDocument { Id = "" }));
		}

		[Fact]
		public async Task ReadAsync_CallsStorageWithCorrectKey()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			await repo.ReadAsync("mykey", "mypartition");
			mock.Verify(s => s.ReadAsync("mykey", "mypartition"), Times.Once);
		}

		[Fact]
		public async Task ReadAsync_ThrowsIfKeyInvalid()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			await Assert.ThrowsAsync<StorageException>(() => repo.ReadAsync((string?)null!, "partitionKey"));
			await Assert.ThrowsAsync<StorageException>(() => repo.ReadAsync("", "partitionKey"));
			await Assert.ThrowsAsync<StorageException>(() => repo.ReadAsync("id", (string?)null!));
			await Assert.ThrowsAsync<StorageException>(() => repo.ReadAsync("id", ""));
		}

		[Fact]
		public async Task UpdateAsync_CallsStorageWithCorrectArgs()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			var doc = new TestDocument { Id = "id" };
			await repo.UpdateAsync("id", doc);
			mock.Verify(s => s.UpdateAsync("id", doc), Times.Once);
		}

		[Fact]
		public async Task UpdateAsync_ThrowsIfKeyOrEntityInvalid()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			await Assert.ThrowsAsync<StorageException>(() => repo.UpdateAsync((string?)null!, new TestDocument { Id = "id" }));
			await Assert.ThrowsAsync<StorageException>(() => repo.UpdateAsync("", new TestDocument { Id = "id" }));
			await Assert.ThrowsAsync<StorageException>(() => repo.UpdateAsync("id", (TestDocument?)null!));
			await Assert.ThrowsAsync<StorageException>(() => repo.UpdateAsync("id", new TestDocument { Id = "" }));
		}

		[Fact]
		public async Task DeleteAsync_CallsStorageWithCorrectArgs()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			await repo.DeleteAsync("id", "partitionKey");
			mock.Verify(s => s.DeleteAsync("id", "partitionKey"), Times.Once);
		}

		[Fact]
		public async Task DeleteAsync_ThrowsIfKeyInvalid()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			await Assert.ThrowsAsync<StorageException>(() => repo.DeleteAsync((string?)null!, "partitionKey"));
			await Assert.ThrowsAsync<StorageException>(() => repo.DeleteAsync("", "partitionKey"));
			await Assert.ThrowsAsync<StorageException>(() => repo.DeleteAsync("id", (string?)null!));
			await Assert.ThrowsAsync<StorageException>(() => repo.DeleteAsync("id", ""));
		}

		[Fact]
		public async Task ReadAllByPartitionKeyAsync_CallsStorageWithCorrectArg()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			await repo.ReadAllByPartitionKeyAsync("partkey");
			mock.Verify(s => s.ReadAllByPartitionKeyAsync("partkey"), Times.Once);
		}

		[Fact]
		public async Task ReadAllByPartitionKeyAsync_ThrowsIfPartitionKeyInvalid()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			var repo = new TestRepository(mock.Object);
			await Assert.ThrowsAsync<StorageException>(() => repo.ReadAllByPartitionKeyAsync((string?)null!));
			await Assert.ThrowsAsync<StorageException>(() => repo.ReadAllByPartitionKeyAsync(""));
		}
	}
}
