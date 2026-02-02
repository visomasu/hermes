using Xunit;
using Moq;
using Hermes.Controllers;
using Hermes.Tools.AzureDevOps;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Integrations.AzureDevOps;
using Hermes.Tools;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Hermes.Tools.AzureDevOps.Capabilities;
using Microsoft.Extensions.Logging;

namespace Hermes.Tests.Controllers
{
	public class ToolsControllerTests
	{
		private static Mock<AzureDevOpsTool> CreateMockTool()
		{
			var mockLogger = new Mock<ILogger<AzureDevOpsTool>>();
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var mockTreeCapability = new Mock<IAgentToolCapability<GetWorkItemTreeCapabilityInput>>();
			var mockAreaPathCapability = new Mock<IAgentToolCapability<GetWorkItemsByAreaPathCapabilityInput>>();
			var mockParentHierarchyCapability = new Mock<IAgentToolCapability<GetParentHierarchyCapabilityInput>>();
			var mockFullHierarchyCapability = new Mock<IAgentToolCapability<GetFullHierarchyCapabilityInput>>();
			var mockDiscoverUserActivityCapability = new Mock<IAgentToolCapability<DiscoverUserActivityCapabilityInput>>();
			var mockGenerateNewsletterCapability = new Mock<IAgentToolCapability<GenerateNewsletterCapabilityInput>>();

			return new Mock<AzureDevOpsTool>(MockBehavior.Default, mockLogger.Object, mockClient.Object, mockTreeCapability.Object, mockAreaPathCapability.Object, mockParentHierarchyCapability.Object, mockFullHierarchyCapability.Object, mockDiscoverUserActivityCapability.Object, mockGenerateNewsletterCapability.Object);
		}

		[Fact]
		public async Task GetWorkItemTree_ReturnsJsonResult()
		{
			// Arrange
			var mockTool = CreateMockTool();
			mockTool.Setup(x => x.ExecuteAsync("GetWorkItemTree", It.IsAny<string>()))
				.ReturnsAsync("{\"workItem\":{\"id\":1},\"children\":[]}");
			var controller = new ToolsController(mockTool.Object);

			// Act
			var result = await controller.GetWorkItemTree(1,1);

			// Assert
			var contentResult = Assert.IsType<ContentResult>(result);
			Assert.Equal("application/json", contentResult.ContentType);
			Assert.Contains("workItem", contentResult.Content);
		}

		[Fact]
		public async Task GetWorkItemTree_WhenToolThrows_ReturnsServerError()
		{
			// Arrange
			var mockTool = CreateMockTool();
			mockTool.Setup(x => x.ExecuteAsync("GetWorkItemTree", It.IsAny<string>()))
				.ThrowsAsync(new System.Exception("Tool error"));
			var controller = new ToolsController(mockTool.Object);

			// Act & Assert
			await Assert.ThrowsAsync<System.Exception>(() => controller.GetWorkItemTree(1,1));
		}
	}
}
