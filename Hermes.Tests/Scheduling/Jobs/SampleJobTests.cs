using Hermes.Scheduling.Jobs;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Hermes.Tests.Scheduling.Jobs
{
	public class SampleJobTests
	{
		[Fact]
		public async Task Execute_LogsMessage()
		{
			// Arrange
			var mockLogger = new Mock<ILogger<SampleJob>>();
			var mockContext = new Mock<IJobExecutionContext>();
			var job = new SampleJob(mockLogger.Object);

			// Act
			await job.Execute(mockContext.Object);

			// Assert
			mockLogger.Verify(
				x => x.Log(
					LogLevel.Information,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SampleJob executed")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);
		}

		[Fact]
		public async Task Execute_CompletesSuccessfully()
		{
			// Arrange
			var mockLogger = new Mock<ILogger<SampleJob>>();
			var mockContext = new Mock<IJobExecutionContext>();
			var job = new SampleJob(mockLogger.Object);

			// Act
			var task = job.Execute(mockContext.Object);

			// Assert
			Assert.NotNull(task);
			await task; // Should not throw
			Assert.True(task.IsCompletedSuccessfully);
		}

		[Fact]
		public async Task Execute_LogsUtcTime()
		{
			// Arrange
			var mockLogger = new Mock<ILogger<SampleJob>>();
			var mockContext = new Mock<IJobExecutionContext>();
			var job = new SampleJob(mockLogger.Object);

			// Act
			await job.Execute(mockContext.Object);

			// Assert
			mockLogger.Verify(
				x => x.Log(
					LogLevel.Information,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UTC")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);
		}
	}
}
