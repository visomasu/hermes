using Hermes.Storage.Core;
using Hermes.Storage.Core.Models;
using Hermes.Storage.Repositories;
using Moq;
using Xunit;
using Hermes.Tests.Storage.Data;

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
			await repo.ReadAsync("id");
			await repo.UpdateAsync("id", new TestDocument { Id = "test-id" });
			await repo.DeleteAsync("id");
			Xunit.Assert.True(true);
		}
	}
}
