using Xunit;
using Hermes.Controllers;
using Hermes.Controllers.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Hermes.Tests.Controllers
{
	public class HermesControllerTests
	{
		[Fact]
		public void CanConstructHermesController()
		{
			var logger = new Mock<ILogger<HermesController>>();
			var controller = new HermesController(logger.Object);
			Xunit.Assert.NotNull(controller);
		}

		[Fact]
		public void Chat_ReturnsOk()
		{
			var logger = new Mock<ILogger<HermesController>>();
			var controller = new HermesController(logger.Object);
			var input = new ChatInput(text: "Hello");
			var result = controller.Chat("corr-id", input);
			Xunit.Assert.IsType<OkObjectResult>(result);
		}

		[Fact]
		public async Task WebSocketEndpoint_ReturnsActionResult()
		{
			var logger = new Mock<ILogger<HermesController>>();
			var controller = new HermesController(logger.Object);
			controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
			var result = await controller.WebSocketEndpoint();
			Xunit.Assert.IsAssignableFrom<IActionResult>(result);
		}
	}
}
