using Hermes.Storage.Core;
using Hermes.Storage.Core.Models;

namespace Hermes.Storage.Repositories.Sample
{
	/// <summary>
	/// Sample repository for SampleRepositoryModel entities.
	/// </summary>
	public class SampleRepository : RepositoryBase<SampleRepositoryModel>
	{
		/// <inheritdoc/>
		protected override string ObjectTypeCode => "sample";

		/// <summary>
		/// Initializes a new instance of the <see cref="SampleRepository"/> class.
		/// </summary>
		/// <param name="storage">The storage client to use.</param>
		public SampleRepository(IStorageClient<SampleRepositoryModel, string> storage)
			: base(storage)
		{
		}
	}
}
