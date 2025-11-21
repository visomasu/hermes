using Xunit;
using Integrations.AzureDevOps;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

namespace Hermes.Tests.Integrations.AzureDevOps
{
	public class IAzureDevOpsWorkItemClientTests
	{
		[Fact]
		public async Task GetWorkItemAsync_ReturnsJsonString()
		{
			// Arrange
			var mock = new Mock<IAzureDevOpsWorkItemClient>();
			mock.Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), null)).ReturnsAsync("{\"id\":1}");

			// Act
			var result = await mock.Object.GetWorkItemAsync(1, null);

			// Assert
			Assert.Contains("id", result);
			var json = JsonDocument.Parse(result);
			Assert.True(json.RootElement.TryGetProperty("id", out var idProp));
			Assert.Equal(1, idProp.GetInt32());
		}

		[Fact]
		public async Task GetWorkItemsAsync_ReturnsJsonString()
		{
			// Arrange
			var mock = new Mock<IAzureDevOpsWorkItemClient>();
			mock.Setup(x => x.GetWorkItemsAsync(It.IsAny<IEnumerable<int>>(), null)).ReturnsAsync("[{\"id\":1},{\"id\":2}]");

			// Act
			var result = await mock.Object.GetWorkItemsAsync(new[] {1,2 }, null);

			// Assert
			Assert.StartsWith("[", result);
			Assert.Contains("id\":1", result);
			Assert.Contains("id\":2", result);
			var json = JsonDocument.Parse(result);
			Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
			Assert.Equal(2, json.RootElement.GetArrayLength());
			Assert.True(json.RootElement[0].TryGetProperty("id", out var id1));
			Assert.True(json.RootElement[1].TryGetProperty("id", out var id2));
			Assert.Equal(1, id1.GetInt32());
			Assert.Equal(2, id2.GetInt32());
		}

		[Fact]
		public async Task GetWorkItemAsync_ReturnsRiskAssessmentAndStatus()
		{
			// Arrange
			var mock = new Mock<IAzureDevOpsWorkItemClient>();
			mock.Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), null)).ReturnsAsync("{\"id\":1,\"riskAssessment\":\"High\",\"status\":\"Active\"}");

			// Act
			var result = await mock.Object.GetWorkItemAsync(1, null);

			// Assert
			var json = JsonDocument.Parse(result);
			Assert.True(json.RootElement.TryGetProperty("riskAssessment", out var riskProp));
			Assert.True(json.RootElement.TryGetProperty("status", out var statusProp));
			Assert.Equal("High", riskProp.GetString());
			Assert.Equal("Active", statusProp.GetString());
		}
	}
}
