using Hermes.Storage.Core;
using Hermes.Storage.Repositories.Sample;
using Xunit;

namespace Hermes.Tests.Storage.Repositories.Sample
{
	public class SampleRepositoryTests
	{
		[Fact]
		public void CanConstructWithStorageClient()
		{
			var mock = new Moq.Mock<IStorageClient<SampleRepositoryModel, string>>();
			var repo = new SampleRepository(mock.Object);
			Xunit.Assert.NotNull(repo);
		}
	}
}
