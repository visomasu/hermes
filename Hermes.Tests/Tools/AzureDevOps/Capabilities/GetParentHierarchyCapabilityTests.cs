using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Integrations.AzureDevOps;
using Moq;
using System.Text.Json;
using Xunit;

namespace Hermes.Tests.Tools.AzureDevOps.Capabilities
{
	public class GetParentHierarchyCapabilityTests
	{
		[Fact]
		public async Task ExecuteAsync_ValidInput_ReturnsMinimalHierarchyJson()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var hierarchyJson = @"[
				{
					""id"": 1001,
					""fields"": {
						""System.Title"": ""Objective 1"",
						""System.WorkItemType"": ""Objective"",
						""System.AreaPath"": ""Project\\Team""
					},
					""level"": 0
				},
				{
					""id"": 1010,
					""fields"": {
						""System.Title"": ""Initiative 1"",
						""System.WorkItemType"": ""Initiative"",
						""System.AreaPath"": ""Project\\Team""
					},
					""level"": 1
				},
				{
					""id"": 1020,
					""fields"": {
						""System.Title"": ""Epic 1"",
						""System.WorkItemType"": ""Epic"",
						""System.AreaPath"": ""Project\\Team""
					},
					""level"": 2
				}
			]";

			mockClient.Setup(x => x.GetParentHierarchyAsync(1020, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(hierarchyJson);

			var capability = new GetParentHierarchyCapability(mockClient.Object);
			var input = new GetParentHierarchyCapabilityInput
			{
				WorkItemId = 1020,
				Fields = new[] { "System.Id", "System.Title", "System.WorkItemType", "System.AreaPath" }
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			Assert.NotEmpty(result);

			// Verify JSON structure
			using var doc = JsonDocument.Parse(result);
			var items = doc.RootElement;
			Assert.Equal(JsonValueKind.Array, items.ValueKind);
			Assert.Equal(3, items.GetArrayLength());

			// Verify first item has minimal fields
			var firstItem = items[0];
			Assert.True(firstItem.TryGetProperty("Id", out var idProp));
			Assert.Equal(1001, idProp.GetInt32());
			Assert.True(firstItem.TryGetProperty("Title", out var titleProp));
			Assert.Equal("Objective 1", titleProp.GetString());
			Assert.True(firstItem.TryGetProperty("WorkItemType", out var typeProp));
			Assert.Equal("Objective", typeProp.GetString());
			Assert.True(firstItem.TryGetProperty("AreaPath", out var areaProp));
			Assert.Equal("Project\\Team", areaProp.GetString());
			Assert.True(firstItem.TryGetProperty("Level", out var levelProp));
			Assert.Equal(0, levelProp.GetInt32());
		}

		[Fact]
		public async Task ExecuteAsync_CallsClientWithCorrectParameters()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var hierarchyJson = "[]";

			int capturedWorkItemId = 0;
			IEnumerable<string>? capturedFields = null;

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.Callback<int, IEnumerable<string>?>((id, fields) =>
				{
					capturedWorkItemId = id;
					capturedFields = fields;
				})
				.ReturnsAsync(hierarchyJson);

			var capability = new GetParentHierarchyCapability(mockClient.Object);
			var expectedFields = new[] { "System.Id", "System.Title", "System.State" };
			var input = new GetParentHierarchyCapabilityInput
			{
				WorkItemId = 12345,
				Fields = expectedFields
			};

			// Act
			await capability.ExecuteAsync(input);

			// Assert
			Assert.Equal(12345, capturedWorkItemId);
			Assert.NotNull(capturedFields);
			Assert.Equal(expectedFields, capturedFields);
			mockClient.Verify(x => x.GetParentHierarchyAsync(12345, expectedFields), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_WithoutFields_PassesNullToClient()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var hierarchyJson = "[]";

			IEnumerable<string>? capturedFields = null;

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.Callback<int, IEnumerable<string>?>((id, fields) => capturedFields = fields)
				.ReturnsAsync(hierarchyJson);

			var capability = new GetParentHierarchyCapability(mockClient.Object);
			var input = new GetParentHierarchyCapabilityInput
			{
				WorkItemId = 99999,
				Fields = null
			};

			// Act
			await capability.ExecuteAsync(input);

			// Assert
			Assert.Null(capturedFields);
		}

		[Fact]
		public async Task ExecuteAsync_EmptyHierarchy_ReturnsEmptyArray()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var hierarchyJson = "[]";

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(hierarchyJson);

			var capability = new GetParentHierarchyCapability(mockClient.Object);
			var input = new GetParentHierarchyCapabilityInput { WorkItemId = 5000 };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			using var doc = JsonDocument.Parse(result);
			Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
			Assert.Equal(0, doc.RootElement.GetArrayLength());
		}

		[Fact]
		public async Task ExecuteAsync_NonArrayResponse_ReturnsAsIs()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var nonArrayJson = @"{""error"":""Something went wrong""}";

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(nonArrayJson);

			var capability = new GetParentHierarchyCapability(mockClient.Object);
			var input = new GetParentHierarchyCapabilityInput { WorkItemId = 1000 };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.Equal(nonArrayJson, result);
		}

		[Fact]
		public async Task ExecuteAsync_MissingFields_HandlesGracefully()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var hierarchyJson = @"[
				{
					""id"": 100,
					""fields"": {}
				},
				{
					""id"": 200
				}
			]";

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(hierarchyJson);

			var capability = new GetParentHierarchyCapability(mockClient.Object);
			var input = new GetParentHierarchyCapabilityInput { WorkItemId = 100 };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			using var doc = JsonDocument.Parse(result);
			var items = doc.RootElement;
			Assert.Equal(2, items.GetArrayLength());

			// First item should have Id but null for other fields
			var firstItem = items[0];
			Assert.True(firstItem.TryGetProperty("Id", out var idProp));
			Assert.Equal(100, idProp.GetInt32());
			Assert.True(firstItem.TryGetProperty("Title", out var titleProp));
			Assert.Equal(JsonValueKind.Null, titleProp.ValueKind);
		}

		[Fact]
		public void Name_ReturnsCorrectCapabilityName()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var capability = new GetParentHierarchyCapability(mockClient.Object);

			// Act
			var name = capability.Name;

			// Assert
			Assert.Equal("GetParentHierarchy", name);
		}

		[Fact]
		public void Description_ReturnsValidDescription()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var capability = new GetParentHierarchyCapability(mockClient.Object);

			// Act
			var description = capability.Description;

			// Assert
			Assert.NotNull(description);
			Assert.NotEmpty(description);
			Assert.Contains("parent", description, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("hierarchy", description, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task ExecuteAsync_ComplexHierarchy_ExtractsAllMinimalFields()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var hierarchyJson = @"[
				{
					""id"": 1,
					""fields"": {
						""System.Id"": 1,
						""System.Title"": ""Root Objective"",
						""System.WorkItemType"": ""Objective"",
						""System.AreaPath"": ""ProjectA\\TeamB"",
						""System.State"": ""Active"",
						""Custom.ExtraField"": ""ShouldNotAppear""
					},
					""level"": 0
				},
				{
					""id"": 2,
					""fields"": {
						""System.Title"": ""Child Feature"",
						""System.WorkItemType"": ""Feature"",
						""System.AreaPath"": ""ProjectA\\TeamB""
					},
					""level"": 1
				}
			]";

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(hierarchyJson);

			var capability = new GetParentHierarchyCapability(mockClient.Object);
			var input = new GetParentHierarchyCapabilityInput { WorkItemId = 2 };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			using var doc = JsonDocument.Parse(result);
			var items = doc.RootElement;

			// Verify only minimal fields are present (Id, Title, WorkItemType, AreaPath, Level)
			var firstItem = items[0];
			var propertyCount = 0;
			foreach (var property in firstItem.EnumerateObject())
			{
				propertyCount++;
				Assert.Contains(property.Name, new[] { "Id", "Title", "WorkItemType", "AreaPath", "Level" });
			}

			// Should have exactly 5 properties
			Assert.Equal(5, propertyCount);

			// Verify System.State and Custom.ExtraField are not included
			Assert.False(firstItem.TryGetProperty("State", out _));
			Assert.False(firstItem.TryGetProperty("ExtraField", out _));
		}
	}
}
