using Xunit;
using Hermes.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Hermes.Tests.Controllers
{
	public class HealthControllerTests
	{
		[Fact]
		public void CanConstructHealthController()
		{
			var logger = new Mock<ILogger<HealthController>>();
			var controller = new HealthController(logger.Object);
			Xunit.Assert.NotNull(controller);
		}

		[Fact]
		public void GetHealth_ReturnsOk()
		{
			var logger = new Mock<ILogger<HealthController>>();
			var controller = new HealthController(logger.Object);
			var result = controller.Health();
			Xunit.Assert.IsType<OkObjectResult>(result);
		}
	}
}
