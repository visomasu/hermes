using Xunit;
using Moq;
using Hermes.Tools.AzureDevOps;
using Integrations.AzureDevOps;
using System.Text.Json;

namespace Hermes.Tests.Tools.AzureDevOps
{
	public class AzureDevOpsToolTests
	{
		[Fact]
		public void Properties_ShouldExposeCapabilitiesAndMetadata()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var tool = new AzureDevOpsTool(mockClient.Object);

			Assert.Equal("AzureDevOpsTool", tool.Name);
			Assert.Contains("retrieving work item trees", tool.Description);
			Assert.Contains("GetWorkItemTree", tool.Capabilities);
			Assert.Contains("Capabilities: [GetWorkItemTree]", tool.GetMetadata());
		}

		[Fact]
		public async Task ExecuteAsync_GetWorkItemTree_ReturnsTreeJson()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			// Simulate a root work item with one child relation
			string rootJson = "{ \"id\":1, \"relations\": [ { \"rel\": \"System.LinkTypes.Hierarchy-Forward\", \"url\": \"http://dev.azure.com/_apis/wit/workItems/2\", \"attributes\": { \"name\": \"Child\" } } ] }";
			string childJson = "{ \"id\":2 }";
			mockClient.Setup(x => x.GetWorkItemAsync(1)).ReturnsAsync(rootJson);
			mockClient.Setup(x => x.GetWorkItemAsync(2)).ReturnsAsync(childJson);

			var tool = new AzureDevOpsTool(mockClient.Object);
			var input = JsonSerializer.Serialize(new { rootId =1, depth =1 });
			var result = await tool.ExecuteAsync("GetWorkItemTree", input);

			Assert.Contains("workItem", result);
			Assert.Contains("children", result);
			Assert.Contains("id", result);
		}

		[Fact]
		public async Task ExecuteAsync_UnsupportedOperation_Throws()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var tool = new AzureDevOpsTool(mockClient.Object);
			await Assert.ThrowsAsync<NotSupportedException>(() => tool.ExecuteAsync("UnknownOp", "{}"));
		}
	}
}
