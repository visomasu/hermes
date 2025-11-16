using Hermes.Storage.Core;
using Hermes.Storage.Core.Models;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;

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

		[Fact]
		public async Task CanRetrieveAllRecordsByPartitionKey()
		{
			var l1 = new Mock<IStorageClient<TestDocument, string>>();
			var l2 = new Mock<IStorageClient<TestDocument, string>>();
			var docs = new List<TestDocument> {
				new TestDocument { Id = "1", PartitionKey = "A" },
				new TestDocument { Id = "2", PartitionKey = "A" }
			};
			l1.Setup(x => x.ReadAllByPartitionKeyAsync("A")).ReturnsAsync(docs);
			var client = new HierarchicalStorageClient<TestDocument>(l1.Object, l2.Object);
			var results = await client.ReadAllByPartitionKeyAsync("A");
			Assert.Equal(2, results?.Count ?? 0);
			Assert.All(results ?? new List<TestDocument>(), d => Assert.Equal("A", d.PartitionKey));
		}
	}
}
