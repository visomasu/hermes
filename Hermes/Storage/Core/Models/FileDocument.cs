using Newtonsoft.Json;

namespace Hermes.Storage.Core.Models
{
    /// <summary>
    /// Represents a file-backed document whose content is stored as raw data.
    /// </summary>
    public class FileDocument : Document
    {
        /// <summary>
        /// Gets or sets the raw file data.
        /// </summary>
        [JsonProperty("data")]
        public byte[] Data { get; set; } = [];
    }
}