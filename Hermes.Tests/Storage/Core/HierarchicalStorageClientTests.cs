using Hermes.Storage.Core;
using Hermes.Storage.Core.Models;
using Moq;
using Xunit;

namespace Hermes.Tests.Storage.Core
{
	public class HierarchicalStorageClientTests
	{
		public class TestDocument : Document {}

		[Fact]
		public async Task ReadAsync_ReturnsFromL1IfExists()
		{
			var l1 = new Mock<IStorageClient<TestDocument, string>>();
			var l2 = new Mock<IStorageClient<TestDocument, string>>();
			var doc = new TestDocument { Id = "id", PartitionKey = "pk" };
			l1.Setup(x => x.ReadAsync("id", "pk")).ReturnsAsync(doc);
			var client = new HierarchicalStorageClient<TestDocument>(l1.Object, l2.Object);
			var result = await client.ReadAsync("id", "pk");
			Xunit.Assert.Equal(doc, result);
		}
	}
}
