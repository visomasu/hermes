using Hermes.Channels.Teams;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Hermes.Tests.Channels.Teams
{
	public class PeriodicTypingIndicatorTests
	{
		[Fact]
		public async Task PeriodicTypingIndicator_SendsTypingActivitiesPeriodically()
		{
			// Arrange
			var turnContextMock = new Mock<ITurnContext>();
			var sentActivities = new List<IActivity>();

			turnContextMock
				.Setup(tc => tc.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
				.Callback<IActivity, CancellationToken>((activity, _) => sentActivities.Add(activity))
				.ReturnsAsync(new ResourceResponse { Id = "activity-id" });

			var phrase = "brilliant-dancing-thought";

			// Act - create indicator and let it run for ~7 seconds
			using (var indicator = new PeriodicTypingIndicator(turnContextMock.Object, phrase))
			{
				await Task.Delay(7000); // Should send ~2-3 typing activities
				await indicator.StopAsync();
			}

			// Assert - should have sent at least 2 typing activities
			Assert.True(sentActivities.Count >= 2, $"Expected at least 2 typing activities, got {sentActivities.Count}");
			Assert.All(sentActivities, activity =>
			{
				Assert.Equal(ActivityTypes.Typing, activity.Type);
				Assert.Equal(phrase, activity.Value);
			});
		}

		[Fact]
		public async Task PeriodicTypingIndicator_StopsCleanly()
		{
			// Arrange
			var turnContextMock = new Mock<ITurnContext>();
			var sentActivitiesCount = 0;

			turnContextMock
				.Setup(tc => tc.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
				.Callback(() => Interlocked.Increment(ref sentActivitiesCount))
				.ReturnsAsync(new ResourceResponse { Id = "activity-id" });

			var phrase = "curious-wandering-mind";

			// Act - create indicator, let it run briefly, then stop
			var indicator = new PeriodicTypingIndicator(turnContextMock.Object, phrase);
			await Task.Delay(3000); // Let it send some activities
			var countBeforeStop = sentActivitiesCount;

			await indicator.StopAsync();

			// Wait a bit more to ensure no more activities are sent
			await Task.Delay(3000);
			var countAfterStop = sentActivitiesCount;

			// Assert - no new activities should be sent after stop
			Assert.Equal(countBeforeStop, countAfterStop);
			indicator.Dispose();
		}

		[Fact]
		public void PeriodicTypingIndicator_DisposesCorrectly()
		{
			// Arrange
			var turnContextMock = new Mock<ITurnContext>();
			turnContextMock
				.Setup(tc => tc.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new ResourceResponse { Id = "activity-id" });

			var phrase = "splendid-soaring-sketch";
			var indicator = new PeriodicTypingIndicator(turnContextMock.Object, phrase);

			// Act - dispose without explicitly calling StopAsync
			indicator.Dispose();

			// Assert - should not throw exception (disposal should be safe)
			// If we get here without exception, the test passes
			Assert.True(true);
		}

		[Fact]
		public async Task PeriodicTypingIndicator_HandlesNullPhrase()
		{
			// Arrange
			var turnContextMock = new Mock<ITurnContext>();
			IActivity? capturedActivity = null;

			turnContextMock
				.Setup(tc => tc.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
				.Callback<IActivity, CancellationToken>((activity, _) => capturedActivity = activity)
				.ReturnsAsync(new ResourceResponse { Id = "activity-id" });

			// Act - create indicator with null phrase
			using (var indicator = new PeriodicTypingIndicator(turnContextMock.Object, null!))
			{
				await Task.Delay(3000); // Let it send at least one activity
				await indicator.StopAsync();
			}

			// Assert - should handle null phrase gracefully (convert to empty string)
			Assert.NotNull(capturedActivity);
			Assert.Equal(ActivityTypes.Typing, capturedActivity!.Type);
			Assert.Equal(string.Empty, capturedActivity.Value);
		}

		[Fact]
		public async Task PeriodicTypingIndicator_HandlesExceptionsDuringSendActivity()
		{
			// Arrange
			var turnContextMock = new Mock<ITurnContext>();
			turnContextMock
				.Setup(tc => tc.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("Network error"));

			var phrase = "magnificent-flowing-vision";

			// Act & Assert - should not throw exception even when SendActivityAsync fails
			using (var indicator = new PeriodicTypingIndicator(turnContextMock.Object, phrase))
			{
				await Task.Delay(3000); // Let it attempt to send activities
				await indicator.StopAsync(); // Should complete without throwing
			}

			// If we get here without exception, the test passes
			Assert.True(true);
		}
	}
}
