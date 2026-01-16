namespace Hermes.Integrations.AzureOpenAI
{
	/// <summary>
	/// Client for generating text embeddings using Azure OpenAI embedding models.
	/// </summary>
	public interface IAzureOpenAIEmbeddingClient
	{
		/// <summary>
		/// Generates embedding vector for given text.
		/// </summary>
		/// <param name="text">Text to embed.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Embedding vector as float array (1536 dimensions for text-embedding-3-small).</returns>
		Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

		/// <summary>
		/// Generates embeddings for multiple texts in a single batch API call.
		/// More cost-efficient than individual calls when processing multiple texts.
		/// </summary>
		/// <param name="texts">Collection of texts to embed.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Dictionary mapping each text to its embedding vector.</returns>
		Task<Dictionary<string, float[]>> GenerateBatchEmbeddingsAsync(
			IEnumerable<string> texts,
			CancellationToken cancellationToken = default);
	}
}
