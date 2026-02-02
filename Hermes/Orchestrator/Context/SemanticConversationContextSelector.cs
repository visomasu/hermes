using Hermes.Integrations.AzureOpenAI;
using Hermes.Orchestrator.Models;
using Hermes.Storage.Repositories.ConversationHistory;
using Microsoft.Extensions.Logging;

namespace Hermes.Orchestrator.Context
{
	/// <summary>
	/// Semantic context selector that uses embedding-based similarity scoring
	/// to select relevant conversation messages.
	/// </summary>
	public class SemanticConversationContextSelector : IConversationContextSelector
	{
		private readonly IAzureOpenAIEmbeddingClient _embeddingClient;
		private readonly ConversationContextConfig _config;
		private readonly ILogger<SemanticConversationContextSelector> _logger;

		/// <summary>
		/// Initializes a new instance of the SemanticConversationContextSelector.
		/// </summary>
		/// <param name="embeddingClient">Client for generating embeddings.</param>
		/// <param name="config">Configuration for context selection.</param>
		/// <param name="logger">Logger instance.</param>
		public SemanticConversationContextSelector(
			IAzureOpenAIEmbeddingClient embeddingClient,
			ConversationContextConfig config,
			ILogger<SemanticConversationContextSelector> logger)
		{
			_embeddingClient = embeddingClient;
			_config = config;
			_logger = logger;
		}

		/// <inheritdoc/>
		public async Task<List<ConversationMessage>> SelectRelevantContextAsync(
			string currentQuery,
			List<ConversationMessage> conversationHistory,
			CancellationToken cancellationToken = default)
		{
			// Step 1: Handle edge cases
			if (string.IsNullOrWhiteSpace(currentQuery))
			{
				_logger.LogWarning("Current query is empty, returning empty context");
				return new List<ConversationMessage>();
			}

			if (conversationHistory == null || conversationHistory.Count == 0)
			{
				_logger.LogDebug("No conversation history available");
				return new List<ConversationMessage>();
			}

			// Step 2: If semantic filtering disabled, fall back to time-based
			if (!_config.EnableSemanticFiltering)
			{
				_logger.LogInformation("Semantic filtering disabled, using time-based selection");
				return _GetRecentMessages(conversationHistory, _config.MaxContextTurns);
			}

			// Step 2.5: Detect pronouns and adjust minimum recent context
			var minRecentTurns = _config.MinRecentTurns;
			if (_ContainsPronounsOrDemonstratives(currentQuery))
			{
				// Ensure we have at least 4 recent messages for pronoun resolution
				// (previous user query + assistant response + current context)
				minRecentTurns = Math.Max(minRecentTurns, 4);
				_logger.LogDebug("Pronoun detected in query, increasing MinRecentTurns to {MinTurns}", minRecentTurns);
			}

			// Step 3: Generate embedding for current query
			float[] queryEmbedding;
			try
			{
				queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(currentQuery, cancellationToken);
				_logger.LogDebug("Generated query embedding ({Dimensions} dimensions)", queryEmbedding.Length);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to generate query embedding, falling back to time-based selection");
				return _GetRecentMessages(conversationHistory, _config.MaxContextTurns);
			}

			// Step 4: Generate embeddings for messages without them (batch for efficiency)
			await _EnsureMessageEmbeddingsAsync(conversationHistory, cancellationToken);

			// Step 5: Sort messages by timestamp (chronological order)
			var orderedMessages = conversationHistory
				.OrderBy(m => m.Timestamp)
				.ToList();

			// Step 6: Separate recent vs older messages
			var recentMessages = orderedMessages
				.Skip(Math.Max(0, orderedMessages.Count - minRecentTurns))
				.ToList();

			var olderMessages = orderedMessages
				.Take(Math.Max(0, orderedMessages.Count - minRecentTurns))
				.ToList();

			// Step 7: Score older messages by semantic similarity
			var scoredOlderMessages = olderMessages
				.Where(m => m.Embedding != null)
				.Select(m => new
				{
					Message = m,
					Similarity = _CosineSimilarity(queryEmbedding, m.Embedding!)
				})
				.Where(x => x.Similarity >= _config.RelevanceThreshold)
				.OrderByDescending(x => x.Similarity)
				.Take(_config.MaxContextTurns - recentMessages.Count)
				.Select(x => x.Message)
				.ToList();

			// Step 8: Combine and sort chronologically
			var selectedMessages = scoredOlderMessages
				.Concat(recentMessages)
				.OrderBy(m => m.Timestamp)
				.Take(_config.MaxContextTurns)
				.ToList();

			// Step 9: Deduplicate similar user queries (optional optimization)
			var deduplicatedMessages = _DeduplicateSimilarQueries(selectedMessages);

			_logger.LogInformation(
				"Selected {SelectedCount} messages from {TotalCount} total ({RecentCount} recent + {RelevantCount} relevant older, {DeduplicatedCount} after deduplication)",
				deduplicatedMessages.Count,
				conversationHistory.Count,
				recentMessages.Count,
				scoredOlderMessages.Count,
				deduplicatedMessages.Count);

			return deduplicatedMessages;
		}

		/// <summary>
		/// Ensures all messages have embeddings generated.
		/// Generates embeddings in batch for messages that don't have them.
		/// </summary>
		private async Task _EnsureMessageEmbeddingsAsync(
			List<ConversationMessage> messages,
			CancellationToken cancellationToken)
		{
			var messagesNeedingEmbeddings = messages
				.Where(m => !m.EmbeddingGenerated && !string.IsNullOrWhiteSpace(m.Content))
				.ToList();

			if (messagesNeedingEmbeddings.Count == 0)
			{
				return;
			}

			_logger.LogDebug("Generating embeddings for {Count} messages", messagesNeedingEmbeddings.Count);

			try
			{
				var textsToEmbed = messagesNeedingEmbeddings
					.Select(m => m.Content)
					.ToList();

				var embeddings = await _embeddingClient.GenerateBatchEmbeddingsAsync(textsToEmbed, cancellationToken);

				foreach (var message in messagesNeedingEmbeddings)
				{
					if (embeddings.TryGetValue(message.Content, out var embedding))
					{
						message.Embedding = embedding;
						message.EmbeddingGenerated = true;
					}
				}

				_logger.LogInformation("Successfully generated {Count} embeddings", messagesNeedingEmbeddings.Count);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to generate embeddings for {Count} messages", messagesNeedingEmbeddings.Count);

				// Mark as generated (even though failed) to avoid repeated attempts
				foreach (var message in messagesNeedingEmbeddings)
				{
					message.EmbeddingGenerated = true;
				}
			}
		}

		/// <summary>
		/// Calculates cosine similarity between two vectors.
		/// Returns a value between -1 and 1, where 1 means identical direction.
		/// </summary>
		private double _CosineSimilarity(float[] vectorA, float[] vectorB)
		{
			if (vectorA.Length != vectorB.Length)
			{
				throw new ArgumentException($"Vectors must have same length (got {vectorA.Length} and {vectorB.Length})");
			}

			double dotProduct = 0;
			double magnitudeA = 0;
			double magnitudeB = 0;

			for (int i = 0; i < vectorA.Length; i++)
			{
				dotProduct += vectorA[i] * vectorB[i];
				magnitudeA += vectorA[i] * vectorA[i];
				magnitudeB += vectorB[i] * vectorB[i];
			}

			magnitudeA = Math.Sqrt(magnitudeA);
			magnitudeB = Math.Sqrt(magnitudeB);

			if (magnitudeA == 0 || magnitudeB == 0)
			{
				return 0;
			}

			return dotProduct / (magnitudeA * magnitudeB);
		}

		/// <summary>
		/// Simple time-based selection (fallback method).
		/// </summary>
		private List<ConversationMessage> _GetRecentMessages(List<ConversationMessage> messages, int count)
		{
			return messages
				.OrderBy(m => m.Timestamp)
				.Skip(Math.Max(0, messages.Count - count))
				.ToList();
		}

		/// <summary>
		/// Deduplicates similar user queries, keeping only the latest response for each duplicate group.
		/// This optimization reduces token usage when users ask the same question multiple times.
		/// </summary>
		/// <param name="selectedMessages">Messages to deduplicate (already sorted chronologically).</param>
		/// <returns>Deduplicated list with only the latest response to similar queries.</returns>
		private List<ConversationMessage> _DeduplicateSimilarQueries(List<ConversationMessage> selectedMessages)
		{
			// If deduplication is disabled, return as-is
			if (!_config.EnableQueryDeduplication)
			{
				return selectedMessages;
			}

			// If too few messages, no deduplication needed
			if (selectedMessages.Count < 2)
			{
				return selectedMessages;
			}

			// Extract user queries with their embeddings
			var userQueries = selectedMessages
				.Select((msg, index) => new { Message = msg, Index = index })
				.Where(x => x.Message.Role == "user" && x.Message.Embedding != null)
				.ToList();

			// If no user queries with embeddings, return as-is
			if (userQueries.Count < 2)
			{
				return selectedMessages;
			}

			// Track which message indices to exclude (duplicates)
			var indicesToExclude = new HashSet<int>();

			// Compare each user query with later queries
			for (int i = 0; i < userQueries.Count - 1; i++)
			{
				// Skip if already marked as duplicate
				if (indicesToExclude.Contains(userQueries[i].Index))
				{
					continue;
				}

				var currentQuery = userQueries[i];

				for (int j = i + 1; j < userQueries.Count; j++)
				{
					var laterQuery = userQueries[j];

					// Skip if already marked as duplicate
					if (indicesToExclude.Contains(laterQuery.Index))
					{
						continue;
					}

					// Calculate similarity between the two queries
					var similarity = _CosineSimilarity(currentQuery.Message.Embedding!, laterQuery.Message.Embedding!);

					// If similar enough, mark the older query (and its response) for exclusion
					if (similarity >= _config.QueryDuplicationThreshold)
					{
						var olderQueryPreview = currentQuery.Message.Content?.Length > 50
							? currentQuery.Message.Content.Substring(0, 50)
							: currentQuery.Message.Content ?? "";
						var newerQueryPreview = laterQuery.Message.Content?.Length > 50
							? laterQuery.Message.Content.Substring(0, 50)
							: laterQuery.Message.Content ?? "";

						_logger.LogDebug(
							"Detected duplicate query (similarity: {Similarity:F3}): '{OlderQuery}' vs '{NewerQuery}'",
							similarity,
							olderQueryPreview,
							newerQueryPreview);

						// Mark older query for exclusion
						indicesToExclude.Add(currentQuery.Index);

						// Also exclude the assistant response immediately following the older query (if exists)
						if (currentQuery.Index + 1 < selectedMessages.Count &&
							selectedMessages[currentQuery.Index + 1].Role == "assistant")
						{
							indicesToExclude.Add(currentQuery.Index + 1);
						}

						// Break inner loop since current query is now marked as duplicate
						break;
					}
				}
			}

			// If no duplicates found, return original list
			if (indicesToExclude.Count == 0)
			{
				return selectedMessages;
			}

			// Filter out excluded messages
			var deduplicated = selectedMessages
				.Where((msg, index) => !indicesToExclude.Contains(index))
				.ToList();

			_logger.LogInformation(
				"Query deduplication removed {RemovedCount} messages ({QueryCount} duplicate queries + responses)",
				indicesToExclude.Count,
				indicesToExclude.Count / 2); // Approximate number of duplicate query pairs

			return deduplicated;
		}

		/// <summary>
		/// Detects if the query contains pronouns or demonstratives that require context resolution.
		/// </summary>
		/// <param name="query">User query to analyze.</param>
		/// <returns>True if the query likely needs context for pronoun resolution.</returns>
		private bool _ContainsPronounsOrDemonstratives(string query)
		{
			if (string.IsNullOrWhiteSpace(query))
			{
				return false;
			}

			var lowerQuery = query.ToLowerInvariant();

			// Demonstratives and pronouns that reference previous context
			var contextReferencePatterns = new[]
			{
				"that item", "that feature", "that epic", "that work item",
				"that one", "that id", "the same", "this item", "this feature",
				"it", "its", "them", "their", "those",
				"the previous", "the last", "the above"
			};

			return contextReferencePatterns.Any(pattern => lowerQuery.Contains(pattern));
		}
	}
}
