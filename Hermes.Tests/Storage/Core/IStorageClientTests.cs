using Xunit;
using Hermes.Storage.Core;
using Hermes.Storage.Core.Models;
using Moq;
using System.Threading.Tasks;
using Hermes.Tests.Storage.Data;

namespace Hermes.Tests.Storage.Core
{
	public class IStorageClientTests
	{
		[Fact]
		public async Task Interface_Crud_Methods_CanBeCalled()
		{
			var mock = new Mock<IStorageClient<TestDocument, string>>();
			await mock.Object.CreateAsync(new TestDocument());
			await mock.Object.ReadAsync("id", "pk");
			await mock.Object.UpdateAsync("id", new TestDocument());
			await mock.Object.DeleteAsync("id", "pk");
			Xunit.Assert.True(true);
		}
	}
}
