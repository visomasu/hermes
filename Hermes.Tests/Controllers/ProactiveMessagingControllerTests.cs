using Hermes.Controllers;
using Hermes.Services.Notifications;
using Hermes.Services.Notifications.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.Tests.Controllers
{
	public class ProactiveMessagingControllerTests
	{
		private readonly Mock<IProactiveMessenger> _messengerMock;
		private readonly Mock<ILogger<ProactiveMessagingController>> _loggerMock;

		public ProactiveMessagingControllerTests()
		{
			_messengerMock = new Mock<IProactiveMessenger>();
			_loggerMock = new Mock<ILogger<ProactiveMessagingController>>();
		}

		[Fact]
		public void Constructor_InitializesWithDependencies()
		{
			// Act
			var controller = new ProactiveMessagingController(
				_messengerMock.Object,
				_loggerMock.Object);

			// Assert
			Assert.NotNull(controller);
		}

		[Fact]
		public async Task SendProactiveMessageByTeamsId_ReturnsOk_WhenSuccessful()
		{
			// Arrange
			var result = new ProactiveMessageResult
			{
				Success = true,
				SentAt = DateTime.UtcNow
			};

			_messengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync("user-123", "Test message", default))
				.ReturnsAsync(result);

			var controller = new ProactiveMessagingController(
				_messengerMock.Object,
				_loggerMock.Object);

			var request = new SendProactiveMessageByTeamsIdRequest
			{
				TeamsUserId = "user-123",
				Message = "Test message"
			};

			// Act
			var actionResult = await controller.SendProactiveMessageByTeamsId(request);

			// Assert
			var okResult = Assert.IsType<OkObjectResult>(actionResult);
			Assert.NotNull(okResult.Value);

			var response = okResult.Value as dynamic;
			Assert.NotNull(response);
		}

		[Fact]
		public async Task SendProactiveMessageByTeamsId_ReturnsBadRequest_WhenFails()
		{
			// Arrange
			var result = new ProactiveMessageResult
			{
				Success = false,
				ErrorMessage = "Test error"
			};

			_messengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync("user-123", "Test message", default))
				.ReturnsAsync(result);

			var controller = new ProactiveMessagingController(
				_messengerMock.Object,
				_loggerMock.Object);

			var request = new SendProactiveMessageByTeamsIdRequest
			{
				TeamsUserId = "user-123",
				Message = "Test message"
			};

			// Act
			var actionResult = await controller.SendProactiveMessageByTeamsId(request);

			// Assert
			var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
			Assert.NotNull(badRequestResult.Value);
		}

		[Fact]
		public async Task SendProactiveMessageByTeamsId_Returns500_WhenExceptionThrown()
		{
			// Arrange
			_messengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<string>(), default))
				.ThrowsAsync(new Exception("Test exception"));

			var controller = new ProactiveMessagingController(
				_messengerMock.Object,
				_loggerMock.Object);

			var request = new SendProactiveMessageByTeamsIdRequest
			{
				TeamsUserId = "user-123",
				Message = "Test message"
			};

			// Act
			var actionResult = await controller.SendProactiveMessageByTeamsId(request);

			// Assert
			var statusCodeResult = Assert.IsType<ObjectResult>(actionResult);
			Assert.Equal(500, statusCodeResult.StatusCode);
		}

		[Fact]
		public async Task SendProactiveMessageByTeamsId_CallsMessengerWithCorrectParameters()
		{
			// Arrange
			var result = new ProactiveMessageResult
			{
				Success = true,
				SentAt = DateTime.UtcNow
			};

			_messengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync("user-456", "Hello World", default))
				.ReturnsAsync(result);

			var controller = new ProactiveMessagingController(
				_messengerMock.Object,
				_loggerMock.Object);

			var request = new SendProactiveMessageByTeamsIdRequest
			{
				TeamsUserId = "user-456",
				Message = "Hello World"
			};

			// Act
			await controller.SendProactiveMessageByTeamsId(request);

			// Assert
			_messengerMock.Verify(
				m => m.SendMessageByTeamsUserIdAsync("user-456", "Hello World", default),
				Times.Once);
		}

		[Fact]
		public async Task SendProactiveMessageByTeamsId_LogsError_WhenExceptionThrown()
		{
			// Arrange
			_messengerMock
				.Setup(m => m.SendMessageByTeamsUserIdAsync(It.IsAny<string>(), It.IsAny<string>(), default))
				.ThrowsAsync(new Exception("Test exception"));

			var controller = new ProactiveMessagingController(
				_messengerMock.Object,
				_loggerMock.Object);

			var request = new SendProactiveMessageByTeamsIdRequest
			{
				TeamsUserId = "user-123",
				Message = "Test message"
			};

			// Act
			await controller.SendProactiveMessageByTeamsId(request);

			// Assert
			_loggerMock.Verify(
				x => x.Log(
					LogLevel.Error,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => true),
					It.IsAny<Exception>(),
					It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
				Times.Once);
		}
	}
}
