using Hermes.Integrations.AzureOpenAI;
using Hermes.Orchestrator.Context;
using Hermes.Orchestrator.Models;
using Hermes.Storage.Repositories.ConversationHistory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.Tests.Orchestrator.Context
{
	/// <summary>
	/// Unit tests for SemanticConversationContextSelector.
	/// </summary>
	public class SemanticConversationContextSelectorTests
	{
		private readonly Mock<IAzureOpenAIEmbeddingClient> _mockEmbeddingClient;
		private readonly Mock<ILogger<SemanticConversationContextSelector>> _mockLogger;
		private readonly ConversationContextConfig _config;

		public SemanticConversationContextSelectorTests()
		{
			_mockEmbeddingClient = new Mock<IAzureOpenAIEmbeddingClient>();
			_mockLogger = new Mock<ILogger<SemanticConversationContextSelector>>();
			_config = new ConversationContextConfig
			{
				RelevanceThreshold = 0.70,
				MaxContextTurns = 10,
				MinRecentTurns = 1,
				EnableSemanticFiltering = true,
				EnableQueryDeduplication = false // Disabled by default for existing tests
			};
		}

		[Fact]
		public async Task SelectRelevantContextAsync_EmptyQuery_ReturnsEmpty()
		{
			// Arrange
			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				_config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage { Role = "user", Content = "Test", Timestamp = DateTimeOffset.UtcNow }
			};

			// Act
			var result = await selector.SelectRelevantContextAsync(string.Empty, history);

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_EmptyHistory_ReturnsEmpty()
		{
			// Arrange
			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				_config,
				_mockLogger.Object);

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", new List<ConversationMessage>());

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_SemanticFilteringDisabled_UsesTimeBasedFallback()
		{
			// Arrange
			var config = new ConversationContextConfig
			{
				EnableSemanticFiltering = false,
				MaxContextTurns = 2
			};

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				config,
				_mockLogger.Object);

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
					Role = "user",
					Content = "Message 2",
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
			Assert.Equal(2, result.Count);
			Assert.Equal("Message 2", result[0].Content);
			Assert.Equal("Message 3", result[1].Content);

			// Verify embedding client was never called
			_mockEmbeddingClient.Verify(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_EmbeddingGenerationFails_FallsBackToTimeBased()
		{
			// Arrange
			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("API error"));

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				_config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 1",
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
			Assert.Equal(2, result.Count); // Falls back to time-based selection
		}

		[Fact]
		public async Task SelectRelevantContextAsync_AllMessagesRelevant_ReturnsAllUpToMax()
		{
			// Arrange
			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var message1Embedding = CreateEmbedding(0.9f, 0.1f, 0.0f); // High similarity
			var message2Embedding = CreateEmbedding(0.95f, 0.05f, 0.0f); // High similarity

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			var config = new ConversationContextConfig
			{
				RelevanceThreshold = 0.70,
				MaxContextTurns = 2,
				MinRecentTurns = 1,
				EnableSemanticFiltering = true,
				EnableQueryDeduplication = false // Disabled for existing tests
			};

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 1",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2),
					Embedding = message1Embedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 2",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
					Embedding = message2Embedding,
					EmbeddingGenerated = true
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(2, result.Count);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_NoneRelevant_ReturnsMinRecentOnly()
		{
			// Arrange
			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var message1Embedding = CreateEmbedding(0.0f, 1.0f, 0.0f); // Orthogonal (similarity = 0)
			var message2Embedding = CreateEmbedding(0.1f, 0.9f, 0.0f); // Low similarity

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				_config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Irrelevant message",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2),
					Embedding = message1Embedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Recent message",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
					Embedding = message2Embedding,
					EmbeddingGenerated = true
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(1, result.Count); // Only MinRecentTurns (1) is included
			Assert.Equal("Recent message", result[0].Content);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_MixedRelevance_ReturnsHybridSet()
		{
			// Arrange
			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var relevantEmbedding = CreateEmbedding(0.9f, 0.1f, 0.0f); // High similarity
			var irrelevantEmbedding = CreateEmbedding(0.0f, 1.0f, 0.0f); // Orthogonal
			var recentEmbedding = CreateEmbedding(0.5f, 0.5f, 0.0f); // Medium similarity

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				_config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Relevant old message",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
					Embedding = relevantEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Irrelevant old message",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4),
					Embedding = irrelevantEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Recent message",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
					Embedding = recentEmbedding,
					EmbeddingGenerated = true
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			Assert.Contains(result, m => m.Content == "Relevant old message"); // High similarity
			Assert.Contains(result, m => m.Content == "Recent message"); // MinRecentTurns
			Assert.DoesNotContain(result, m => m.Content == "Irrelevant old message"); // Low similarity
		}

		[Fact]
		public async Task SelectRelevantContextAsync_NoEmbeddings_GeneratesEmbeddings()
		{
			// Arrange
			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var messageEmbedding = CreateEmbedding(0.9f, 0.1f, 0.0f);

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			_mockEmbeddingClient
				.Setup(x => x.GenerateBatchEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((List<string> texts, CancellationToken _) =>
				{
					return texts.ToDictionary(t => t, t => messageEmbedding);
				});

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				_config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Message without embedding",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
					Embedding = null,
					EmbeddingGenerated = false
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			_mockEmbeddingClient.Verify(
				x => x.GenerateBatchEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
				Times.Once);

			Assert.NotNull(result);
			Assert.True(history[0].EmbeddingGenerated);
			Assert.NotNull(history[0].Embedding);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_RespectsMaxContextTurns()
		{
			// Arrange
			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var relevantEmbedding = CreateEmbedding(0.95f, 0.05f, 0.0f); // All highly similar

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			var config = new ConversationContextConfig
			{
				RelevanceThreshold = 0.70,
				MaxContextTurns = 2,
				MinRecentTurns = 1,
				EnableSemanticFiltering = true,
				EnableQueryDeduplication = false // Disabled for existing tests
			};

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 1",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4),
					Embedding = relevantEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 2",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-3),
					Embedding = relevantEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Message 3",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2),
					Embedding = relevantEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Recent message",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
					Embedding = relevantEmbedding,
					EmbeddingGenerated = true
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			Assert.True(result.Count <= config.MaxContextTurns); // Should not exceed MaxContextTurns
		}

		[Fact]
		public async Task SelectRelevantContextAsync_ReturnsMessagesInChronologicalOrder()
		{
			// Arrange
			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var relevantEmbedding = CreateEmbedding(0.9f, 0.1f, 0.0f);

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				_config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Third",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
					Embedding = relevantEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "First",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-3),
					Embedding = relevantEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "Second",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2),
					Embedding = relevantEmbedding,
					EmbeddingGenerated = true
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("First", result[0].Content);
			Assert.Equal("Second", result[1].Content);
			Assert.Equal("Third", result[2].Content);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_WithDuplicateQueries_RemovesOlderDuplicates()
		{
			// Arrange
			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var duplicateQueryEmbedding = CreateEmbedding(0.98f, 0.02f, 0.0f); // Very similar (0.99 similarity)

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			var config = new ConversationContextConfig
			{
				RelevanceThreshold = 0.70,
				MaxContextTurns = 10,
				MinRecentTurns = 4, // Keep all as recent to test deduplication only
				EnableSemanticFiltering = true,
				EnableQueryDeduplication = true // Enabled for this test
			};

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "What is the status of feature X?",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
					Embedding = duplicateQueryEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Feature X is in development",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-9),
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "What is the status of feature X?", // Duplicate query (latest)
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
					Embedding = duplicateQueryEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Feature X was completed yesterday",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-0),
					EmbeddingGenerated = true
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			// Should only keep the latest duplicate pair (turn 3-4), removing the older one (turn 1-2)
			Assert.Equal(2, result.Count);
			// Verify we have the latest duplicate query
			var keptQuery = result.FirstOrDefault(m => m.Content == "What is the status of feature X?");
			Assert.NotNull(keptQuery);
			Assert.True(keptQuery.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-2), "Should keep the most recent duplicate");
			// Verify we have the latest response
			Assert.Contains(result, m => m.Content == "Feature X was completed yesterday");
			// Verify the older response was removed
			Assert.DoesNotContain(result, m => m.Content == "Feature X is in development");
		}

		[Fact]
		public async Task SelectRelevantContextAsync_WithDeduplicationDisabled_KeepsAllQueries()
		{
			// Arrange
			var config = new ConversationContextConfig
			{
				RelevanceThreshold = 0.70,
				MaxContextTurns = 10,
				MinRecentTurns = 4, // Keep all messages as "recent" to avoid semantic filtering
				EnableSemanticFiltering = true,
				EnableQueryDeduplication = false // Disabled
			};

			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var duplicateQueryEmbedding = CreateEmbedding(0.98f, 0.02f, 0.0f);

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "What is the status?",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
					Embedding = duplicateQueryEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "In development",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-9),
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "What is the status?",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
					Embedding = duplicateQueryEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Completed",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-0),
					EmbeddingGenerated = true
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			// Should keep all messages when deduplication is disabled
			Assert.Equal(4, result.Count);
		}

		[Fact]
		public async Task SelectRelevantContextAsync_WithSlightlyDifferentQueries_KeepsBoth()
		{
			// Arrange
			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var query1Embedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var query2Embedding = CreateEmbedding(0.8f, 0.6f, 0.0f); // Different enough (0.8 similarity)

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			var config = new ConversationContextConfig
			{
				RelevanceThreshold = 0.70,
				MaxContextTurns = 10,
				MinRecentTurns = 4, // Keep all as recent to test deduplication
				EnableSemanticFiltering = true,
				EnableQueryDeduplication = true // Enabled for this test
			};

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "What is the status of feature X?",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
					Embedding = query1Embedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "In development",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-9),
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "user",
					Content = "What are the risks of feature X?", // Different question
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
					Embedding = query2Embedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "The risks are minimal",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-0),
					EmbeddingGenerated = true
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			// Should keep both queries since they're different enough
			Assert.Equal(4, result.Count);
			Assert.Contains(result, m => m.Content == "What is the status of feature X?");
			Assert.Contains(result, m => m.Content == "What are the risks of feature X?");
		}

		[Fact]
		public async Task SelectRelevantContextAsync_WithMultipleDuplicateGroups_RemovesAllOlderDuplicates()
		{
			// Arrange
			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);
			var groupAEmbedding = CreateEmbedding(0.98f, 0.02f, 0.0f);
			var groupBEmbedding = CreateEmbedding(0.0f, 0.98f, 0.02f);

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			var config = new ConversationContextConfig
			{
				RelevanceThreshold = 0.70,
				MaxContextTurns = 10,
				MinRecentTurns = 8, // Keep all 8 messages as recent to test deduplication
				EnableSemanticFiltering = true,
				EnableQueryDeduplication = true // Enabled for this test
			};

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				// First duplicate group (Query A)
				new ConversationMessage
				{
					Role = "user",
					Content = "Query A first time",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-20),
					Embedding = groupAEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Response A1",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-19),
					EmbeddingGenerated = true
				},
				// Second duplicate group (Query B)
				new ConversationMessage
				{
					Role = "user",
					Content = "Query B first time",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
					Embedding = groupBEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Response B1",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-9),
					EmbeddingGenerated = true
				},
				// Query A again (should keep this)
				new ConversationMessage
				{
					Role = "user",
					Content = "Query A second time",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
					Embedding = groupAEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Response A2",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4),
					EmbeddingGenerated = true
				},
				// Query B again (should keep this)
				new ConversationMessage
				{
					Role = "user",
					Content = "Query B second time",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2),
					Embedding = groupBEmbedding,
					EmbeddingGenerated = true
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Response B2",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
					EmbeddingGenerated = true
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			// Should keep only the latest from each duplicate group (4 messages total)
			Assert.Equal(4, result.Count);
			Assert.Contains(result, m => m.Content == "Query A second time");
			Assert.Contains(result, m => m.Content == "Response A2");
			Assert.Contains(result, m => m.Content == "Query B second time");
			Assert.Contains(result, m => m.Content == "Response B2");
			Assert.DoesNotContain(result, m => m.Content == "Response A1");
			Assert.DoesNotContain(result, m => m.Content == "Response B1");
		}

		[Fact]
		public async Task SelectRelevantContextAsync_WithNoEmbeddings_SkipsDeduplication()
		{
			// Arrange
			var queryEmbedding = CreateEmbedding(1.0f, 0.0f, 0.0f);

			_mockEmbeddingClient
				.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryEmbedding);

			_mockEmbeddingClient
				.Setup(x => x.GenerateBatchEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("Embedding generation failed"));

			var selector = new SemanticConversationContextSelector(
				_mockEmbeddingClient.Object,
				_config,
				_mockLogger.Object);

			var history = new List<ConversationMessage>
			{
				new ConversationMessage
				{
					Role = "user",
					Content = "Query without embedding",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
					Embedding = null,
					EmbeddingGenerated = false
				},
				new ConversationMessage
				{
					Role = "assistant",
					Content = "Response",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-9),
					EmbeddingGenerated = false
				}
			};

			// Act
			var result = await selector.SelectRelevantContextAsync("test query", history);

			// Assert
			Assert.NotNull(result);
			// Should not crash and should return messages (deduplication skipped due to missing embeddings)
		}

		/// <summary>
		/// Helper method to create a normalized embedding vector.
		/// </summary>
		private float[] CreateEmbedding(float x, float y, float z)
		{
			// Create a simple 3D embedding for testing
			// In reality, embeddings are 1536-dimensional, but we use 3D for simplicity
			var magnitude = Math.Sqrt(x * x + y * y + z * z);
			return new[]
			{
				(float)(x / magnitude),
				(float)(y / magnitude),
				(float)(z / magnitude)
			};
		}
	}
}
