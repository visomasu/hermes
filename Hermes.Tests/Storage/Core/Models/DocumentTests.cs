using Xunit;
using Hermes.Storage.Core.Models;
using Hermes.Tests.Storage.Data;

namespace Hermes.Tests.Storage.Core.Models
{
	public class DocumentTests
	{
		[Fact]
		public void Document_DefaultTTL_IsEightHours()
		{
			var doc = new TestDocument();
			Xunit.Assert.Equal(28800, doc.TTL);
		}
	}
}
