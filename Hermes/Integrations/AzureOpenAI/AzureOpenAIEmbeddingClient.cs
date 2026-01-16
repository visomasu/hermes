using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace Hermes.Integrations.AzureOpenAI
{
	/// <summary>
	/// Implementation of embedding client using Azure OpenAI.
	/// </summary>
	public class AzureOpenAIEmbeddingClient : IAzureOpenAIEmbeddingClient
	{
		private readonly AzureOpenAIClient _client;
		private readonly string _embeddingModel;
		private readonly ILogger<AzureOpenAIEmbeddingClient> _logger;

		/// <summary>
		/// Initializes a new instance of the AzureOpenAIEmbeddingClient.
		/// </summary>
		/// <param name="endpoint">Azure OpenAI endpoint URL.</param>
		/// <param name="embeddingModel">Embedding model deployment name (e.g., "text-embedding-3-small").</param>
		/// <param name="logger">Logger instance.</param>
		public AzureOpenAIEmbeddingClient(
			string endpoint,
			string embeddingModel,
			ILogger<AzureOpenAIEmbeddingClient> logger)
		{
			_client = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential());
			_embeddingModel = embeddingModel;
			_logger = logger;
		}

		/// <inheritdoc/>
		public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new ArgumentException("Text cannot be null or empty", nameof(text));
			}

			try
			{
				var embeddingClient = _client.GetEmbeddingClient(_embeddingModel);
				var result = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);

				return result.Value.ToFloats().ToArray();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to generate embedding for text of length {Length}", text.Length);
				throw;
			}
		}

		/// <inheritdoc/>
		public async Task<Dictionary<string, float[]>> GenerateBatchEmbeddingsAsync(
			IEnumerable<string> texts,
			CancellationToken cancellationToken = default)
		{
			var textList = texts.ToList();

			if (textList.Count == 0)
			{
				return new Dictionary<string, float[]>();
			}

			var result = new Dictionary<string, float[]>();

			try
			{
				var embeddingClient = _client.GetEmbeddingClient(_embeddingModel);
				var embeddings = await embeddingClient.GenerateEmbeddingsAsync(textList, cancellationToken: cancellationToken);

				for (int i = 0; i < textList.Count; i++)
				{
					result[textList[i]] = embeddings.Value[i].ToFloats().ToArray();
				}

				_logger.LogInformation("Successfully generated {Count} embeddings in batch", textList.Count);

				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to generate batch embeddings for {Count} texts", textList.Count);
				throw;
			}
		}
	}
}
