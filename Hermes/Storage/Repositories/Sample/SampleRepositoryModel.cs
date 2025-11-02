
using Hermes.Storage.Core.Models;

namespace Hermes.Storage.Repositories.Sample
{
    /// <summary>
    /// Sample repository model class.
    /// </summary>
    public class SampleRepositoryModel : Document
    {
        /// <summary>
        /// Data property.
        /// </summary>
        public string Data { get; set; } = string.Empty;
    }
}
