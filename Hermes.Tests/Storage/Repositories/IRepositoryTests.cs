using Xunit;
using Hermes.Storage.Repositories;
using Hermes.Storage.Core.Models;
using Moq;
using System.Threading.Tasks;
using Hermes.Tests.Storage.Data;

namespace Hermes.Tests.Storage.Repositories
{
	public class IRepositoryTests
	{
		[Fact]
		public async Task Interface_Crud_Methods_CanBeCalled()
		{
			var mock = new Mock<IRepository<TestDocument>>();
			await mock.Object.CreateAsync(new TestDocument());
			await mock.Object.ReadAsync("id", "partitionKey");
			await mock.Object.UpdateAsync("id", new TestDocument());
			await mock.Object.DeleteAsync("id", "partitionKey");
			Assert.True(true);
		}
	}
}
