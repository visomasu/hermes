using Hermes.Controllers;
using Integrations.AzureDevOps;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Hermes.Tests.Controllers
{
	public class AzureDevOpsControllerTests
	{
		private readonly Mock<IAzureDevOpsWorkItemClient> _mockClient;
		private readonly Mock<ILogger<AzureDevOpsController>> _mockLogger;
		private readonly AzureDevOpsController _controller;

		public AzureDevOpsControllerTests()
		{
			_mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			_mockLogger = new Mock<ILogger<AzureDevOpsController>>();
			_controller = new AzureDevOpsController(_mockClient.Object, _mockLogger.Object);
		}

		[Fact]
		public async Task GetWorkItem_ReturnsJson()
		{
			var workItemJson = "{\"id\":1}";
			_mockClient.Setup(x => x.GetWorkItemAsync(1, It.IsAny<IEnumerable<string>>())).ReturnsAsync(workItemJson);
			var result = await _controller.GetWorkItem(1);
			var okResult = Assert.IsType<OkObjectResult>(result.Result);
			Assert.Contains("id", okResult?.Value?.ToString());
		}

		[Fact]
		public async Task GetWorkItem_ReturnsNotFoundJson()
		{
			_mockClient.Setup(x => x.GetWorkItemAsync(2, It.IsAny<IEnumerable<string>>())).ReturnsAsync((string?)null!);
			var result = await _controller.GetWorkItem(2);
			Assert.IsType<NotFoundResult>(result.Result);
		}

		[Fact]
		public async Task GetWorkItem_ReturnsServerError_OnException()
		{
			// Arrange
			_mockClient.Setup(x => x.GetWorkItemAsync(3, It.IsAny<IEnumerable<string>>())).ThrowsAsync(new Exception("fail"));

			// Act
			var result = await _controller.GetWorkItem(3);

			// Assert
			var serverError = Assert.IsType<ObjectResult>(result.Result);
			Assert.Equal(500, serverError.StatusCode);
		}
	}
}
