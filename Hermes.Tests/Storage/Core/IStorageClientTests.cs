using Hermes.Storage.Core;
using Hermes.Tests.Storage.Data;
using Moq;
using Xunit;

namespace Hermes.Tests.Storage.Core
{
	public class IStorageClientTests
	{
		[Fact]
		public async Task Interface_Crud_Methods_CanBeCalled()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			await mock.Object.CreateAsync(new TestDocument());
			await mock.Object.ReadAsync("id", "pk");
			await mock.Object.UpdateAsync("id", new TestDocument());
			await mock.Object.DeleteAsync("id", "pk");
			Xunit.Assert.True(true);
		}

		[Fact]
		public async Task Interface_CanRetrieveAllRecordsByPartitionKey()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			mock.Setup(x => x.ReadAllByPartitionKeyAsync("A")).ReturnsAsync(new List<TestDocument> { new TestDocument { Id = "1", PartitionKey = "A" }, new TestDocument { Id = "2", PartitionKey = "A" } });
			var results = await mock.Object.ReadAllByPartitionKeyAsync("A");
			Assert.NotNull(results);
			Assert.Equal(2, results.Count);
			Assert.All(results, d => Assert.Equal("A", d.PartitionKey));
		}
	}
}
