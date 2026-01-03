using Hermes.Controllers;
using Hermes.Controllers.Models;
using Hermes.Controllers.Models.Instructions;
using Hermes.Orchestrator;
using Hermes.Storage.Repositories.HermesInstructions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.Tests.Controllers
{
	public class HermesControllerTests
	{
		private static HermesController CreateController(Mock<IHermesInstructionsRepository>? instructionsRepoMock = null)
		{
			var logger = new Mock<ILogger<HermesController>>();
			var orchestrator = new Mock<IAgentOrchestrator>().Object;
			var instructionsRepo = instructionsRepoMock?.Object ?? new Mock<IHermesInstructionsRepository>().Object;
			return new HermesController(logger.Object, orchestrator, instructionsRepo);
		}

		[Fact]
		public async Task Chat_ReturnsOk()
		{
			// Arrange
			var logger = new Mock<ILogger<HermesController>>();
			var orchestratorMock = new Mock<IAgentOrchestrator>();
			var instructionsRepo = new Mock<IHermesInstructionsRepository>().Object;
			orchestratorMock.Setup(o => o.OrchestrateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("mock-response");
			var controller = new HermesController(logger.Object, orchestratorMock.Object, instructionsRepo);
			var input = new ChatInput(text: "Hello");

			// Act
			var result = await controller.Chat("corr-id", input);

			// Assert
			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal("mock-response", okResult.Value);
		}

		[Fact]
		public void CanConstructHermesController()
		{
			// Arrange & Act
			var controller = CreateController();

			// Assert
			Xunit.Assert.NotNull(controller);
		}

		[Fact]
		public async Task WebSocketEndpoint_ReturnsActionResult()
		{
			// Arrange
			var controller = CreateController();
			controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

			// Act
			var result = await controller.WebSocketEndpoint();

			// Assert
			Xunit.Assert.IsAssignableFrom<IActionResult>(result);
		}

		[Fact]
		public async Task CreateInstruction_ReturnsOk()
		{
			// Arrange
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			instructionsRepoMock.Setup(r => r.CreateInstructionAsync("test", HermesInstructionType.ProjectAssistant,1)).Returns(Task.CompletedTask);
			var controller = CreateController(instructionsRepoMock);
			var request = new CreateInstructionRequest { Instruction = "test", InstructionType = HermesInstructionType.ProjectAssistant, Version =1 };

			// Act
			var result = await controller.CreateInstruction(request);

			// Assert
			instructionsRepoMock.Verify(r => r.CreateInstructionAsync("test", HermesInstructionType.ProjectAssistant,1), Times.Once);
			Assert.IsType<OkResult>(result);
		}

		[Fact]
		public async Task UpdateInstruction_ReturnsOk()
		{
			// Arrange
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			instructionsRepoMock.Setup(r => r.UpdateInstructionAsync(HermesInstructionType.ProjectAssistant, "updated",2)).Returns(Task.CompletedTask);
			var controller = CreateController(instructionsRepoMock);
			var request = new UpdateInstructionRequest { InstructionType = HermesInstructionType.ProjectAssistant, NewInstruction = "updated", Version =2 };

			// Act
			var result = await controller.UpdateInstruction(request);

			// Assert
			instructionsRepoMock.Verify(r => r.UpdateInstructionAsync(HermesInstructionType.ProjectAssistant, "updated",2), Times.Once);
			Assert.IsType<OkResult>(result);
		}

		[Fact]
		public async Task GetInstruction_ReturnsOkWithInstruction()
		{
			// Arrange
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			var instruction = new HermesInstructions("test", HermesInstructionType.ProjectAssistant,1);
			instructionsRepoMock.Setup(r => r.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant,1)).ReturnsAsync(instruction);
			var controller = CreateController(instructionsRepoMock);

			// Act
			var result = await controller.GetInstruction(HermesInstructionType.ProjectAssistant,1);

			// Assert
			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(instruction, okResult.Value);
		}

		[Fact]
		public async Task GetInstruction_ReturnsNotFoundIfNull()
		{
			// Arrange
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			instructionsRepoMock.Setup(r => r.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant,1)).ReturnsAsync((HermesInstructions?)null);
			var controller = CreateController(instructionsRepoMock);

			// Act
			var result = await controller.GetInstruction(HermesInstructionType.ProjectAssistant,1);

			// Assert
			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public async Task DeleteInstruction_ReturnsOk()
		{
			// Arrange
			var instructionsRepoMock = new Mock<IHermesInstructionsRepository>();
			instructionsRepoMock.Setup(r => r.DeleteInstructionAsync(HermesInstructionType.ProjectAssistant,1)).Returns(Task.CompletedTask);
			var controller = CreateController(instructionsRepoMock);

			// Act
			var result = await controller.DeleteInstruction(HermesInstructionType.ProjectAssistant,1);

			// Assert
			instructionsRepoMock.Verify(r => r.DeleteInstructionAsync(HermesInstructionType.ProjectAssistant,1), Times.Once);
			Assert.IsType<OkResult>(result);
		}
	}
}
