using System.Text.Json;
using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Integrations.AzureDevOps;
using Moq;
using Xunit;

namespace Hermes.Tests.Tools.AzureDevOps.Capabilities
{
	public class GetWorkItemsByAreaPathCapabilityTests
	{
		[Fact]
		public async Task ExecuteAsync_CallsClientWithParsedInputs_AndReturnsArray()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			const string expectedJson = "[{\"id\":1},{\"id\":2}]";

			IEnumerable<string>? capturedTypes = null;
			IEnumerable<string>? capturedFields = null;
			string? capturedAreaPath = null;
			int capturedPageNumber = 0;
			int capturedPageSize = 0;

			mockClient
				.Setup(x => x.GetWorkItemsByAreaPathAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<int>(),
					It.IsAny<int>()))
				.Callback<string, IEnumerable<string>?, IEnumerable<string>?, int, int>((area, types, fields, pageNumber, pageSize) =>
				{
					capturedAreaPath = area;
					capturedTypes = types;
					capturedFields = fields;
					capturedPageNumber = pageNumber;
					capturedPageSize = pageSize;
				})
				.ReturnsAsync(expectedJson);

			var capability = new GetWorkItemsByAreaPathCapability(mockClient.Object);
			var input = new GetWorkItemsByAreaPathCapabilityInput
			{
				AreaPath = "Project\\Team\\Area",
				WorkItemTypes = new[] { "Feature", "User Story" },
				Fields = new[] { "System.Id", "System.Title" }
			};

			var result = await capability.ExecuteAsync(input);

			Assert.Equal("Project\\Team\\Area", capturedAreaPath);
			Assert.NotNull(capturedTypes);
			Assert.Contains("Feature", capturedTypes!);
			Assert.Contains("User Story", capturedTypes!);
			Assert.NotNull(capturedFields);
			Assert.Contains("System.Id", capturedFields!);
			Assert.Contains("System.Title", capturedFields!);
			Assert.Equal(1, capturedPageNumber);
			Assert.Equal(5, capturedPageSize);

			using var doc = JsonDocument.Parse(result);
			var root = doc.RootElement;
			Assert.Equal(JsonValueKind.Array, root.ValueKind);
		}

		[Fact]
		public async Task ExecuteAsync_ThrowsIfAreaPathMissingOrInvalid()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var capability = new GetWorkItemsByAreaPathCapability(mockClient.Object);

			await Assert.ThrowsAsync<ArgumentException>(async () =>
			{
				var badInput = new GetWorkItemsByAreaPathCapabilityInput { AreaPath = "" };
				await capability.ExecuteAsync(badInput);
			});
		}

		[Fact]
		public async Task ExecuteAsync_PassesPagingAndReturnsMinimalFields()
		{
			var clientMock = new Mock<IAzureDevOpsWorkItemClient>(MockBehavior.Strict);
			var capability = new GetWorkItemsByAreaPathCapability(clientMock.Object);

			var input = new GetWorkItemsByAreaPathCapabilityInput
			{
				AreaPath = "proj/team/area",
				WorkItemTypes = new[] { "Feature" },
				Fields = new[] { "System.Id", "System.Title", "System.WorkItemType", "System.AreaPath" },
				PageNumber = 2,
				PageSize = 5
			};

			var clientObjects = new[]
			{
				new
				{
					id = 10,
					fields = new Dictionary<string, object?>
					{
						["System.Title"] = "Item 10",
						["System.WorkItemType"] = "Feature",
						["System.AreaPath"] = "proj/team/area"
					},
					relations = Array.Empty<object>()
				},
				new
				{
					id = 11,
					fields = new Dictionary<string, object?>
					{
						["System.Title"] = "Item 11",
						["System.WorkItemType"] = "Feature",
						["System.AreaPath"] = "proj/team/area"
					},
					relations = Array.Empty<object>()
				}
			};
			var clientResult = JsonSerializer.Serialize(clientObjects);

			clientMock
				.Setup(c => c.GetWorkItemsByAreaPathAsync(
					"proj/team/area",
					It.Is<IEnumerable<string>>(t => t.Single() == "Feature"),
					It.Is<IEnumerable<string>>(f => f.Contains("System.Id") && f.Contains("System.Title")),
					2,
					5))
				.ReturnsAsync(clientResult);

			var json = await capability.ExecuteAsync(input);

			clientMock.VerifyAll();

			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			Assert.Equal(JsonValueKind.Array, root.ValueKind);
			Assert.Equal(2, root.GetArrayLength());

			var first = root[0];
			Assert.True(first.TryGetProperty("Id", out var idProp));
			Assert.Equal(10, idProp.GetInt32());
			Assert.True(first.TryGetProperty("Title", out var titleProp));
			Assert.Equal("Item 10", titleProp.GetString());
			Assert.True(first.TryGetProperty("WorkItemType", out var typeProp));
			Assert.Equal("Feature", typeProp.GetString());
			Assert.True(first.TryGetProperty("AreaPath", out var areaProp));
			Assert.Equal("proj/team/area", areaProp.GetString());

			Assert.False(first.TryGetProperty("relations", out _));
		}
	}
}
