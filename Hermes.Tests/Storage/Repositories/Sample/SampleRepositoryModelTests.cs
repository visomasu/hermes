using Xunit;
using Hermes.Storage.Repositories.Sample;

namespace Hermes.Tests.Storage.Repositories.Sample
{
	public class SampleRepositoryModelTests
	{
		[Fact]
		public void DataProperty_DefaultsToEmptyString()
		{
			var model = new SampleRepositoryModel();
			Xunit.Assert.Equal(string.Empty, model.Data);
		}
	}
}
