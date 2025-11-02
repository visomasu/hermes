using Xunit;
using Hermes.Storage.Core.CosmosDB;
using Hermes.Storage.Core.Models;
using System.Threading.Tasks;

namespace Hermes.Tests.Storage.Core.CosmosDB
{
	public class CosmosDbStorageClientTests
	{
		private class TestDocument : Document {}

		[Fact]
		public void CanConstructWithConnectionString()
		{
			// Use a valid Base64 string for AccountKey
			var client = new CosmosDbStorageClient<TestDocument>("AccountEndpoint=https://localhost:8081/;AccountKey=MTIzNDU2Nzg5MA==;", "db", "container");
			Xunit.Assert.NotNull(client);
		}
	}
}
