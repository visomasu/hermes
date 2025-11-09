using Xunit;
using Integrations.AzureDevOps;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hermes.Tests.Integrations.AzureDevOps
{
	public class IAzureDevOpsWorkItemClientTests
	{
		[Fact]
		public async Task GetWorkItemAsync_ReturnsJsonString()
		{
			var mock = new Mock<IAzureDevOpsWorkItemClient>();
			mock.Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), null)).ReturnsAsync("{\"id\":1}");

			var result = await mock.Object.GetWorkItemAsync(1, null);
			Assert.Contains("id", result);
		}

		[Fact]
		public async Task GetWorkItemsAsync_ReturnsJsonString()
		{
			var mock = new Mock<IAzureDevOpsWorkItemClient>();
			mock.Setup(x => x.GetWorkItemsAsync(It.IsAny<IEnumerable<int>>(), null)).ReturnsAsync("[{\"id\":1},{\"id\":2}]");

			var result = await mock.Object.GetWorkItemsAsync(new[] {1,2 }, null);
			Assert.StartsWith("[", result);
		}
	}
}
