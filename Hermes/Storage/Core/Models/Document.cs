using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Hermes.Storage.Core.Models
{
	/// <summary>
	/// Base class for all non-relational data objects stored in NoSQL databases.
	/// </summary>
	public abstract class Document
	{
		/// <summary>
		/// Gets or sets the unique identifier for the document.
		/// </summary>
		[JsonPropertyName("id")]
		[JsonProperty("id")]
		public string Id { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the partition key for the document.
		/// </summary>
		[JsonPropertyName("partitionkey")]
		[JsonProperty("partitionkey")]
		public string PartitionKey { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the entity tag used for concurrency control.
		/// </summary>
		[JsonPropertyName("etag")]
		[JsonProperty("etag")]
		public string? Etag { get; set; }

		/// <summary>
		/// Gets or sets the time-to-live (TTL) in seconds for the document. Null means infinite. Default is 8 hours (28,800 seconds).
		/// </summary>
		[JsonPropertyName("ttl")]
		[JsonProperty("ttl")]
		public int? TTL { get; set; } = 28800;
	}
}
