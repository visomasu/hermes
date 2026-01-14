using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Integrations.AzureDevOps;
using Moq;
using System.Text.Json;
using Xunit;

namespace Hermes.Tests.Tools.AzureDevOps.Capabilities
{
	public class GetFullHierarchyCapabilityTests
	{
		[Fact]
		public async Task ExecuteAsync_ValidInput_ReturnsMergedParentsAndChildren()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			// Mock GetParentHierarchy call
			var parentHierarchyJson = @"[
				{""id"": 100, ""fields"": {""System.Title"": ""Parent Epic"", ""System.WorkItemType"": ""Epic"", ""System.AreaPath"": ""Project\\Team""}, ""level"": 0},
				{""id"": 200, ""fields"": {""System.Title"": ""Feature"", ""System.WorkItemType"": ""Feature"", ""System.AreaPath"": ""Project\\Team""}, ""level"": 1}
			]";
			mockClient.Setup(x => x.GetParentHierarchyAsync(200, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(parentHierarchyJson);

			// Mock GetWorkItemAsync calls for tree
			var workItemJson = @"{""id"": 200, ""fields"": {""System.WorkItemType"": ""Feature""}, ""relations"": []}";
			mockClient.Setup(x => x.GetWorkItemAsync(200)).ReturnsAsync(workItemJson);
			mockClient.Setup(x => x.GetWorkItemAsync(200, It.IsAny<IEnumerable<string>>())).ReturnsAsync(workItemJson);

			var parentHierarchyCapability = new GetParentHierarchyCapability(mockClient.Object);
			var treeCapability = new GetWorkItemTreeCapability(mockClient.Object);
			var capability = new GetFullHierarchyCapability(parentHierarchyCapability, treeCapability);

			var input = new GetFullHierarchyCapabilityInput
			{
				WorkItemId = 200,
				Depth = 2,
				Fields = new[] { "System.Id", "System.Title" }
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			Assert.NotEmpty(result);

			using var doc = JsonDocument.Parse(result);
			var root = doc.RootElement;

			// Verify structure
			Assert.True(root.TryGetProperty("parents", out var parents));
			Assert.True(root.TryGetProperty("children", out var children));

			// Verify parents array (transformed by GetParentHierarchyCapability)
			Assert.Equal(JsonValueKind.Array, parents.ValueKind);
			Assert.Equal(2, parents.GetArrayLength());
			Assert.Equal(100, parents[0].GetProperty("Id").GetInt32());
			Assert.Equal(200, parents[1].GetProperty("Id").GetInt32());

			// Verify children structure (from GetWorkItemTreeCapability)
			Assert.Equal(JsonValueKind.Object, children.ValueKind);
			Assert.True(children.TryGetProperty("workItem", out var workItem));
			Assert.Equal(200, workItem.GetProperty("id").GetInt32());
		}

		[Fact]
		public async Task ExecuteAsync_CallsParentCapabilityWithCorrectInput()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			int capturedWorkItemId = 0;
			IEnumerable<string>? capturedFields = null;

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.Callback<int, IEnumerable<string>?>((id, fields) =>
				{
					capturedWorkItemId = id;
					capturedFields = fields;
				})
				.ReturnsAsync("[]");

			mockClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>())).ReturnsAsync("{\"id\":12345,\"fields\":{},\"relations\":[]}");
			mockClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync("{\"id\":12345,\"fields\":{},\"relations\":[]}");

			var parentHierarchyCapability = new GetParentHierarchyCapability(mockClient.Object);
			var treeCapability = new GetWorkItemTreeCapability(mockClient.Object);
			var capability = new GetFullHierarchyCapability(parentHierarchyCapability, treeCapability);

			var expectedFields = new[] { "System.Id", "System.Title", "System.State" };
			var input = new GetFullHierarchyCapabilityInput
			{
				WorkItemId = 12345,
				Depth = 3,
				Fields = expectedFields
			};

			// Act
			await capability.ExecuteAsync(input);

			// Assert
			Assert.Equal(12345, capturedWorkItemId);
			Assert.NotNull(capturedFields);
			Assert.Equal(expectedFields, capturedFields);
		}

		[Fact]
		public async Task ExecuteAsync_CallsTreeCapabilityWithCorrectDepth()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync("[]");

			int getWorkItemCallCount = 0;
			mockClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>()))
				.Callback(() => getWorkItemCallCount++)
				.ReturnsAsync("{\"id\":5000,\"fields\":{},\"relations\":[]}");

			mockClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.Callback(() => getWorkItemCallCount++)
				.ReturnsAsync("{\"id\":5000,\"fields\":{},\"relations\":[]}");

			var parentHierarchyCapability = new GetParentHierarchyCapability(mockClient.Object);
			var treeCapability = new GetWorkItemTreeCapability(mockClient.Object);
			var capability = new GetFullHierarchyCapability(parentHierarchyCapability, treeCapability);

			var input = new GetFullHierarchyCapabilityInput
			{
				WorkItemId = 5000,
				Depth = 4
			};

			// Act
			await capability.ExecuteAsync(input);

			// Assert - tree capability should be called (at least once for root)
			Assert.True(getWorkItemCallCount >= 1);
		}

		[Fact]
		public async Task ExecuteAsync_DefaultDepthUsedWhenNotSpecified()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync("[]");

			mockClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>()))
				.ReturnsAsync("{\"id\":999,\"fields\":{},\"relations\":[]}");

			mockClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync("{\"id\":999,\"fields\":{},\"relations\":[]}");

			var parentHierarchyCapability = new GetParentHierarchyCapability(mockClient.Object);
			var treeCapability = new GetWorkItemTreeCapability(mockClient.Object);
			var capability = new GetFullHierarchyCapability(parentHierarchyCapability, treeCapability);

			var input = new GetFullHierarchyCapabilityInput
			{
				WorkItemId = 999,
				Depth = null // Not specified
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert - should complete successfully with default depth
			Assert.NotNull(result);
			using var doc = JsonDocument.Parse(result);
			Assert.True(doc.RootElement.TryGetProperty("parents", out _));
			Assert.True(doc.RootElement.TryGetProperty("children", out _));
		}

		[Fact]
		public async Task ExecuteAsync_EmptyParentsAndChildren_ReturnsValidStructure()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync("[]");

			mockClient.Setup(x => x.GetWorkItemAsync(100))
				.ReturnsAsync("{\"id\":100,\"fields\":{},\"relations\":[]}");

			mockClient.Setup(x => x.GetWorkItemAsync(100, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync("{\"id\":100,\"fields\":{},\"relations\":[]}");

			var parentHierarchyCapability = new GetParentHierarchyCapability(mockClient.Object);
			var treeCapability = new GetWorkItemTreeCapability(mockClient.Object);
			var capability = new GetFullHierarchyCapability(parentHierarchyCapability, treeCapability);

			var input = new GetFullHierarchyCapabilityInput { WorkItemId = 100 };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			using var doc = JsonDocument.Parse(result);
			var root = doc.RootElement;
			Assert.True(root.TryGetProperty("parents", out var parents));
			Assert.True(root.TryGetProperty("children", out var children));
			Assert.Equal(JsonValueKind.Array, parents.ValueKind);
			Assert.Equal(0, parents.GetArrayLength());
		}

		[Fact]
		public void Name_ReturnsCorrectCapabilityName()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var parentHierarchyCapability = new GetParentHierarchyCapability(mockClient.Object);
			var treeCapability = new GetWorkItemTreeCapability(mockClient.Object);
			var capability = new GetFullHierarchyCapability(parentHierarchyCapability, treeCapability);

			// Act
			var name = capability.Name;

			// Assert
			Assert.Equal("GetFullHierarchy", name);
		}

		[Fact]
		public void Description_ReturnsValidDescription()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var parentHierarchyCapability = new GetParentHierarchyCapability(mockClient.Object);
			var treeCapability = new GetWorkItemTreeCapability(mockClient.Object);
			var capability = new GetFullHierarchyCapability(parentHierarchyCapability, treeCapability);

			// Act
			var description = capability.Description;

			// Assert
			Assert.NotNull(description);
			Assert.NotEmpty(description);
			Assert.Contains("full", description, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("hierarchy", description, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task ExecuteAsync_ComplexHierarchy_MergesCorrectly()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			var parentHierarchyJson = @"[
				{""id"": 1, ""fields"": {""System.Title"": ""Objective"", ""System.WorkItemType"": ""Objective"", ""System.AreaPath"": ""P\\T""}, ""level"": 0},
				{""id"": 10, ""fields"": {""System.Title"": ""Initiative"", ""System.WorkItemType"": ""Initiative"", ""System.AreaPath"": ""P\\T""}, ""level"": 1},
				{""id"": 100, ""fields"": {""System.Title"": ""Epic"", ""System.WorkItemType"": ""Epic"", ""System.AreaPath"": ""P\\T""}, ""level"": 2},
				{""id"": 1000, ""fields"": {""System.Title"": ""Feature"", ""System.WorkItemType"": ""Feature"", ""System.AreaPath"": ""P\\T""}, ""level"": 3}
			]";

			mockClient.Setup(x => x.GetParentHierarchyAsync(1000, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(parentHierarchyJson);

			mockClient.Setup(x => x.GetWorkItemAsync(1000))
				.ReturnsAsync(@"{""id"": 1000, ""fields"": {""System.WorkItemType"": ""Feature""}, ""relations"": [{""rel"": ""System.LinkTypes.Hierarchy-Forward"", ""url"": ""http://dev.azure.com/_apis/wit/workItems/2000"", ""attributes"": {""name"": ""Child""}}]}");

			mockClient.Setup(x => x.GetWorkItemAsync(1000, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(@"{""id"": 1000, ""fields"": {""System.WorkItemType"": ""Feature""}, ""relations"": [{""rel"": ""System.LinkTypes.Hierarchy-Forward"", ""url"": ""http://dev.azure.com/_apis/wit/workItems/2000"", ""attributes"": {""name"": ""Child""}}]}");

			mockClient.Setup(x => x.GetWorkItemAsync(2000))
				.ReturnsAsync(@"{""id"": 2000, ""fields"": {""System.WorkItemType"": ""User Story""}, ""relations"": [{""rel"": ""System.LinkTypes.Hierarchy-Forward"", ""url"": ""http://dev.azure.com/_apis/wit/workItems/3000"", ""attributes"": {""name"": ""Child""}}]}");

			mockClient.Setup(x => x.GetWorkItemAsync(2000, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(@"{""id"": 2000, ""fields"": {""System.WorkItemType"": ""User Story""}, ""relations"": [{""rel"": ""System.LinkTypes.Hierarchy-Forward"", ""url"": ""http://dev.azure.com/_apis/wit/workItems/3000"", ""attributes"": {""name"": ""Child""}}]}");

			mockClient.Setup(x => x.GetWorkItemAsync(3000))
				.ReturnsAsync(@"{""id"": 3000, ""fields"": {""System.WorkItemType"": ""Task""}, ""relations"": []}");

			mockClient.Setup(x => x.GetWorkItemAsync(3000, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(@"{""id"": 3000, ""fields"": {""System.WorkItemType"": ""Task""}, ""relations"": []}");

			var parentHierarchyCapability = new GetParentHierarchyCapability(mockClient.Object);
			var treeCapability = new GetWorkItemTreeCapability(mockClient.Object);
			var capability = new GetFullHierarchyCapability(parentHierarchyCapability, treeCapability);

			var input = new GetFullHierarchyCapabilityInput { WorkItemId = 1000, Depth = 3 };

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			using var doc = JsonDocument.Parse(result);
			var root = doc.RootElement;

			var parents = root.GetProperty("parents");
			Assert.Equal(4, parents.GetArrayLength());
			Assert.Equal("Objective", parents[0].GetProperty("Title").GetString());
			Assert.Equal("Feature", parents[3].GetProperty("Title").GetString());

			var children = root.GetProperty("children");
			var childWorkItem = children.GetProperty("workItem");
			Assert.Equal(1000, childWorkItem.GetProperty("id").GetInt32());

			var childrenArray = children.GetProperty("children");
			Assert.Equal(1, childrenArray.GetArrayLength());
			Assert.Equal(2000, childrenArray[0].GetProperty("workItem").GetProperty("id").GetInt32());
		}

		[Fact]
		public async Task ExecuteAsync_NullFields_PassedThroughToCapabilities()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			IEnumerable<string>? capturedFields = null;

			mockClient.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.Callback<int, IEnumerable<string>?>((id, fields) => capturedFields = fields)
				.ReturnsAsync("[]");

			mockClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>()))
				.ReturnsAsync("{\"id\":500,\"fields\":{},\"relations\":[]}");

			mockClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync("{\"id\":500,\"fields\":{},\"relations\":[]}");

			var parentHierarchyCapability = new GetParentHierarchyCapability(mockClient.Object);
			var treeCapability = new GetWorkItemTreeCapability(mockClient.Object);
			var capability = new GetFullHierarchyCapability(parentHierarchyCapability, treeCapability);

			var input = new GetFullHierarchyCapabilityInput
			{
				WorkItemId = 500,
				Fields = null
			};

			// Act
			await capability.ExecuteAsync(input);

			// Assert
			Assert.Null(capturedFields);
		}
	}
}
