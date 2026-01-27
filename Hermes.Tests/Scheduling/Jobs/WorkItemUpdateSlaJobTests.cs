using Hermes.Domain.WorkItemSla;
using Hermes.Notifications.WorkItemSla.Models;
using Hermes.Scheduling.Jobs;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Hermes.Tests.Scheduling.Jobs
{
	public class WorkItemUpdateSlaJobTests
	{
		private readonly Mock<IWorkItemUpdateSlaEvaluator> _evaluatorMock;
		private readonly Mock<ILogger<WorkItemUpdateSlaJob>> _loggerMock;
		private readonly Mock<IJobExecutionContext> _contextMock;

		public WorkItemUpdateSlaJobTests()
		{
			_evaluatorMock = new Mock<IWorkItemUpdateSlaEvaluator>();
			_loggerMock = new Mock<ILogger<WorkItemUpdateSlaJob>>();
			_contextMock = new Mock<IJobExecutionContext>();
			_contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
		}

		[Fact]
		public async Task Execute_CallsEvaluator()
		{
			// Arrange
			var summary = new SlaNotificationRunSummary
			{
				UsersProcessed = 10,
				ViolationsDetected = 5,
				NotificationsSent = 3,
				NotificationsBlocked = 2,
				Errors = 0,
				Duration = TimeSpan.FromMinutes(2)
			};

			_evaluatorMock
				.Setup(e => e.EvaluateAndNotifyAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(summary);

			var job = new WorkItemUpdateSlaJob(_evaluatorMock.Object, _loggerMock.Object);

			// Act
			await job.Execute(_contextMock.Object);

			// Assert
			_evaluatorMock.Verify(
				e => e.EvaluateAndNotifyAsync(It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task Execute_LogsStartAndCompletion()
		{
			// Arrange
			var summary = new SlaNotificationRunSummary
			{
				UsersProcessed = 10,
				ViolationsDetected = 5,
				NotificationsSent = 3,
				NotificationsBlocked = 2,
				Errors = 0,
				Duration = TimeSpan.FromMinutes(2)
			};

			_evaluatorMock
				.Setup(e => e.EvaluateAndNotifyAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(summary);

			var job = new WorkItemUpdateSlaJob(_evaluatorMock.Object, _loggerMock.Object);

			// Act
			await job.Execute(_contextMock.Object);

			// Assert
			_loggerMock.Verify(
				x => x.Log(
					LogLevel.Information,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);

			_loggerMock.Verify(
				x => x.Log(
					LogLevel.Information,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);
		}

		[Fact]
		public async Task Execute_LogsSummaryStatistics()
		{
			// Arrange
			var summary = new SlaNotificationRunSummary
			{
				UsersProcessed = 10,
				ViolationsDetected = 5,
				NotificationsSent = 3,
				NotificationsBlocked = 2,
				Errors = 0,
				Duration = TimeSpan.FromMinutes(2)
			};

			_evaluatorMock
				.Setup(e => e.EvaluateAndNotifyAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(summary);

			var job = new WorkItemUpdateSlaJob(_evaluatorMock.Object, _loggerMock.Object);

			// Act
			await job.Execute(_contextMock.Object);

			// Assert
			_loggerMock.Verify(
				x => x.Log(
					LogLevel.Information,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processed: 10")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);

			_loggerMock.Verify(
				x => x.Log(
					LogLevel.Information,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Violations: 5")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);
		}

		[Fact]
		public async Task Execute_WhenEvaluatorThrows_LogsErrorAndRethrows()
		{
			// Arrange
			_evaluatorMock
				.Setup(e => e.EvaluateAndNotifyAsync(It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("Test exception"));

			var job = new WorkItemUpdateSlaJob(_evaluatorMock.Object, _loggerMock.Object);

			// Act & Assert
			await Assert.ThrowsAsync<Exception>(async () => await job.Execute(_contextMock.Object));

			_loggerMock.Verify(
				x => x.Log(
					LogLevel.Error,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);
		}

		[Fact]
		public async Task Execute_CompletesSuccessfully()
		{
			// Arrange
			var summary = new SlaNotificationRunSummary
			{
				UsersProcessed = 1,
				ViolationsDetected = 1,
				NotificationsSent = 1,
				NotificationsBlocked = 0,
				Errors = 0,
				Duration = TimeSpan.FromSeconds(30)
			};

			_evaluatorMock
				.Setup(e => e.EvaluateAndNotifyAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(summary);

			var job = new WorkItemUpdateSlaJob(_evaluatorMock.Object, _loggerMock.Object);

			// Act
			var task = job.Execute(_contextMock.Object);

			// Assert
			Assert.NotNull(task);
			await task; // Should not throw
			Assert.True(task.IsCompletedSuccessfully);
		}
	}
}
