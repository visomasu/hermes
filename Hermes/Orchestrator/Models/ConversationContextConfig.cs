namespace Hermes.Orchestrator.Models
{
	/// <summary>
	/// Configuration settings for conversation context selection and semantic filtering.
	/// </summary>
	public class ConversationContextConfig
	{
		/// <summary>
		/// Similarity threshold (0.0-1.0) for including messages in context.
		/// Messages with cosine similarity below this value are excluded.
		/// Default: 0.70 (balanced filtering).
		/// </summary>
		public double RelevanceThreshold { get; set; } = 0.70;

		/// <summary>
		/// Maximum number of turns to include in context window.
		/// Prevents unbounded context growth.
		/// Default: 10 turns.
		/// </summary>
		public int MaxContextTurns { get; set; } = 10;

		/// <summary>
		/// Minimum number of recent turns to always include (hybrid approach).
		/// Guarantees conversational continuity regardless of relevance score.
		/// Default: 1 turn (most recent exchange).
		/// </summary>
		public int MinRecentTurns { get; set; } = 1;

		/// <summary>
		/// Enable or disable semantic filtering.
		/// When false, falls back to time-based selection (last N turns).
		/// Default: true.
		/// </summary>
		public bool EnableSemanticFiltering { get; set; } = true;

		/// <summary>
		/// Azure OpenAI embedding model to use.
		/// Default: "text-embedding-3-small" (1536 dimensions, cost-effective).
		/// </summary>
		public string EmbeddingModel { get; set; } = "text-embedding-3-small";

		/// <summary>
		/// Enable query deduplication to remove duplicate or highly similar user queries.
		/// When enabled, only the latest response to similar queries is kept.
		/// Provides additional token savings on repetitive queries.
		/// Default: true.
		/// </summary>
		public bool EnableQueryDeduplication { get; set; } = true;

		/// <summary>
		/// Similarity threshold (0.0-1.0) for considering queries as duplicates.
		/// Queries with cosine similarity above this value are treated as duplicates.
		/// Higher values = stricter duplicate detection (only near-identical queries).
		/// Default: 0.95 (very high similarity required).
		/// </summary>
		public double QueryDuplicationThreshold { get; set; } = 0.95;
	}
}
