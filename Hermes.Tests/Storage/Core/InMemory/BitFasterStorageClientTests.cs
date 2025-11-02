using Xunit;
using Hermes.Storage.Core.InMemory;
using Hermes.Storage.Core.Models;

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
	}
}
