using Hermes.Storage.Repositories.ConversationHistory;
using System.Text.Json;
using Xunit;

namespace Hermes.Tests.Storage.Repositories.ConversationHistory
{
	/// <summary>
	/// Unit tests for ConversationMessage serialization/deserialization.
	/// Ensures backward compatibility with existing messages while supporting new Embedding properties.
	/// </summary>
	public class ConversationMessageTests
	{
		[Fact]
		public void Serialization_WithEmbedding_RoundTrips()
		{
			// Arrange
			var originalMessage = new ConversationMessage
			{
				Role = "user",
				Content = "Test message",
				Timestamp = DateTimeOffset.UtcNow,
				Embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f },
				EmbeddingGenerated = true
			};

			// Act
			var json = JsonSerializer.Serialize(originalMessage);
			var deserializedMessage = JsonSerializer.Deserialize<ConversationMessage>(json);

			// Assert
			Assert.NotNull(deserializedMessage);
			Assert.Equal(originalMessage.Role, deserializedMessage.Role);
			Assert.Equal(originalMessage.Content, deserializedMessage.Content);
			Assert.Equal(originalMessage.Timestamp, deserializedMessage.Timestamp);
			Assert.True(deserializedMessage.EmbeddingGenerated);
			Assert.NotNull(deserializedMessage.Embedding);
			Assert.Equal(5, deserializedMessage.Embedding.Length);
			Assert.Equal(0.1f, deserializedMessage.Embedding[0]);
			Assert.Equal(0.5f, deserializedMessage.Embedding[4]);
		}

		[Fact]
		public void Serialization_WithoutEmbedding_RoundTrips()
		{
			// Arrange
			var originalMessage = new ConversationMessage
			{
				Role = "assistant",
				Content = "Response message",
				Timestamp = DateTimeOffset.UtcNow,
				Embedding = null,
				EmbeddingGenerated = false
			};

			// Act
			var json = JsonSerializer.Serialize(originalMessage);
			var deserializedMessage = JsonSerializer.Deserialize<ConversationMessage>(json);

			// Assert
			Assert.NotNull(deserializedMessage);
			Assert.Equal(originalMessage.Role, deserializedMessage.Role);
			Assert.Equal(originalMessage.Content, deserializedMessage.Content);
			Assert.Equal(originalMessage.Timestamp, deserializedMessage.Timestamp);
			Assert.False(deserializedMessage.EmbeddingGenerated);
			Assert.Null(deserializedMessage.Embedding);
		}

		[Fact]
		public void Deserialization_LegacyFormat_HandlesNullEmbedding()
		{
			// Arrange - Legacy JSON without Embedding properties
			var legacyJson = @"{
				""Role"": ""user"",
				""Content"": ""Legacy message"",
				""Timestamp"": ""2025-01-15T10:00:00Z""
			}";

			// Act
			var deserializedMessage = JsonSerializer.Deserialize<ConversationMessage>(legacyJson);

			// Assert
			Assert.NotNull(deserializedMessage);
			Assert.Equal("user", deserializedMessage.Role);
			Assert.Equal("Legacy message", deserializedMessage.Content);
			Assert.Null(deserializedMessage.Embedding); // Should gracefully handle missing property
			Assert.False(deserializedMessage.EmbeddingGenerated); // Should default to false
		}

		[Fact]
		public void Serialization_EmptyEmbedding_Serializes()
		{
			// Arrange
			var message = new ConversationMessage
			{
				Role = "user",
				Content = "Message with empty embedding",
				Timestamp = DateTimeOffset.UtcNow,
				Embedding = new float[0],
				EmbeddingGenerated = true
			};

			// Act
			var json = JsonSerializer.Serialize(message);
			var deserializedMessage = JsonSerializer.Deserialize<ConversationMessage>(json);

			// Assert
			Assert.NotNull(deserializedMessage);
			Assert.NotNull(deserializedMessage.Embedding);
			Assert.Empty(deserializedMessage.Embedding);
			Assert.True(deserializedMessage.EmbeddingGenerated);
		}

		[Fact]
		public void Serialization_RealWorldEmbedding_RoundTrips()
		{
			// Arrange - Simulate a real 1536-dimensional embedding from text-embedding-3-small
			var embedding = new float[1536];
			for (int i = 0; i < embedding.Length; i++)
			{
				embedding[i] = (float)(Math.Sin(i) * 0.1);
			}

			var originalMessage = new ConversationMessage
			{
				Role = "user",
				Content = "Message with real-world embedding",
				Timestamp = DateTimeOffset.UtcNow,
				Embedding = embedding,
				EmbeddingGenerated = true
			};

			// Act
			var json = JsonSerializer.Serialize(originalMessage);
			var deserializedMessage = JsonSerializer.Deserialize<ConversationMessage>(json);

			// Assert
			Assert.NotNull(deserializedMessage);
			Assert.NotNull(deserializedMessage.Embedding);
			Assert.Equal(1536, deserializedMessage.Embedding.Length);
			Assert.True(deserializedMessage.EmbeddingGenerated);

			// Verify a few sample values
			Assert.Equal(embedding[0], deserializedMessage.Embedding[0]);
			Assert.Equal(embedding[500], deserializedMessage.Embedding[500]);
			Assert.Equal(embedding[1535], deserializedMessage.Embedding[1535]);
		}

		[Fact]
		public void DefaultValues_NewInstance_HasExpectedDefaults()
		{
			// Arrange & Act
			var message = new ConversationMessage();

			// Assert
			Assert.Equal(string.Empty, message.Role);
			Assert.Equal(string.Empty, message.Content);
			Assert.Null(message.Embedding);
			Assert.False(message.EmbeddingGenerated);
		}

		[Fact]
		public void EmbeddingGenerated_Flag_DistinguishesStates()
		{
			// Arrange
			var notGeneratedMessage = new ConversationMessage
			{
				Embedding = null,
				EmbeddingGenerated = false
			};

			var failedGenerationMessage = new ConversationMessage
			{
				Embedding = null,
				EmbeddingGenerated = true // Marked as attempted
			};

			var successfulGenerationMessage = new ConversationMessage
			{
				Embedding = new float[] { 0.1f, 0.2f, 0.3f },
				EmbeddingGenerated = true
			};

			// Act & Assert
			Assert.False(notGeneratedMessage.EmbeddingGenerated);
			Assert.Null(notGeneratedMessage.Embedding);

			Assert.True(failedGenerationMessage.EmbeddingGenerated);
			Assert.Null(failedGenerationMessage.Embedding);

			Assert.True(successfulGenerationMessage.EmbeddingGenerated);
			Assert.NotNull(successfulGenerationMessage.Embedding);
		}
	}
}
