using Hermes.Orchestrator.Context;
using Hermes.Storage.Repositories.ConversationHistory;
using Xunit;

namespace Hermes.Tests.Orchestrator.Context
{
	/// <summary>
	/// Unit tests for TimeBasedConversationContextSelector.
	/// </summary>
	public class TimeBasedConversationContextSelectorTests
	{
		[Fact]
		public async Task SelectRelevantContextAsync_EmptyHistory_ReturnsEmpty()
		{
			// Arrange
			var selector = new TimeBasedConversationContextSelector(maxTurns: 5);
			var emptyHistory = new List<ConversationMessage>();

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", emptyHistory);

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_NullHistory_ReturnsEmpty()
		{
			// Arrange
			var selector = new TimeBasedConversationContextSelector(maxTurns: 5);

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", null!);

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_FewerMessagesThanMax_ReturnsAll()
		{
			// Arrange
			var selector = new TimeBasedConversationContextSelector(maxTurns: 5);
			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 1",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-3)
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Response 1",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2)
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 2",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(3, result.Count);
			Assert.Equal("Message 1", result[0].Content);
			Assert.Equal("Response 1", result[1].Content);
			Assert.Equal("Message 2", result[2].Content);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_MoreMessagesThanMax_ReturnsLastN()
		{
			// Arrange
			var selector = new TimeBasedConversationContextSelector(maxTurns: 3);
			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 1",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Response 1",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4)
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 2",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-3)
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Response 2",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2)
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 3",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(3, result.Count);
			Assert.Equal("Message 2", result[0].Content);
			Assert.Equal("Response 2", result[1].Content);
			Assert.Equal("Message 3", result[2].Content);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_UnorderedHistory_ReturnsSortedByTimestamp()
		{
			// Arrange
			var selector = new TimeBasedConversationContextSelector(maxTurns: 3);
			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 2",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2)
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 1",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-3)
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 3",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(3, result.Count);
			Assert.Equal("Message 1", result[0].Content);
			Assert.Equal("Message 2", result[1].Content);
			Assert.Equal("Message 3", result[2].Content);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_IgnoresQueryParameter()
		{
			// Arrange
			var selector = new TimeBasedConversationContextSelector(maxTurns: 2);
			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Completely unrelated message",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2)
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Response",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
				}
			};

			// Act - The query should not affect the result (time-based selection only)
			var result = await selector.SelectRelevantContextAsync("specific query about newsletters", history);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(2, result.Count);
			Assert.Equal("Completely unrelated message", result[0].Content);
		}
	}
}
