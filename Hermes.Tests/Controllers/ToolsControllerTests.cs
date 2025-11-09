using Xunit;
using Moq;
using Hermes.Controllers;
using Hermes.Tools.AzureDevOps;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Integrations.AzureDevOps;

namespace Hermes.Tests.Controllers
{
	public class ToolsControllerTests
	{
		[Fact]
		public async Task GetWorkItemTree_ReturnsJsonResult()
		{
			// Arrange
			var mockTool = new Mock<AzureDevOpsTool>(MockBehavior.Default, new Mock<IAzureDevOpsWorkItemClient>().Object);
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
			var mockTool = new Mock<AzureDevOpsTool>(MockBehavior.Default, new Mock<IAzureDevOpsWorkItemClient>().Object);
			mockTool.Setup(x => x.ExecuteAsync("GetWorkItemTree", It.IsAny<string>()))
				.ThrowsAsync(new System.Exception("Tool error"));
			var controller = new ToolsController(mockTool.Object);

			// Act & Assert
			await Assert.ThrowsAsync<System.Exception>(() => controller.GetWorkItemTree(1,1));
		}
	}
}
