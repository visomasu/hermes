using System.Text.Json;
using Hermes.Tools;
using Hermes.Tools.AzureDevOps;
using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Integrations.AzureDevOps;
using Moq;
using Xunit;

namespace Hermes.Tests.Tools.AzureDevOps
{
	public class AzureDevOpsToolTests
	{
		private static AzureDevOpsTool CreateTool(Mock<IAzureDevOpsWorkItemClient> clientMock)
		{
			var treeCapability = new GetWorkItemTreeCapability(clientMock.Object);
			var areaPathCapability = new GetWorkItemsByAreaPathCapability(clientMock.Object);
			return new AzureDevOpsTool(clientMock.Object, treeCapability, areaPathCapability);
		}

		private static readonly List<string> FeatureFields = new() {
			"System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "Custom.PrivatePreviewDate", "Custom.PublicPreviewDate", "Custom.GAdate", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate", "Custom.CurrentStatus", "Custom.RiskAssessmentComment"
		};
		private static readonly List<string> UserStoryFields = new() {
			"System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "Custom.RiskAssessmentComment", "Custom.StoryField1", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate"
		};
		private static readonly List<string> TaskFields = new() {
			"System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "System.AssignedTo", "Custom.TaskField1", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate"
		};

		[Fact]
		public void Properties_ShouldExposeCapabilitiesAndMetadata()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var tool = CreateTool(mockClient);

			Assert.Equal("AzureDevOpsTool", tool.Name);
			Assert.Contains("retrieving work item trees", tool.Description);
			Assert.Contains("GetWorkItemTree", tool.Capabilities);
			Assert.Contains("GetWorkItemsByAreaPath", tool.Capabilities);
			Assert.Contains("GetParentHierarchy", tool.Capabilities);
			Assert.Contains("GetFullHierarchy", tool.Capabilities);
			Assert.Contains("Capabilities: [GetWorkItemTree, GetWorkItemsByAreaPath, GetParentHierarchy, GetFullHierarchy]", tool.GetMetadata());
		}

		[Fact]
		public async Task ExecuteAsync_UnsupportedOperation_Throws()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var tool = CreateTool(mockClient);
			await Assert.ThrowsAsync<NotSupportedException>(() => tool.ExecuteAsync("UnknownOp", "{}"));
		}

		[Fact]
		public async Task ExecuteAsync_GetWorkItemsByAreaPath_DelegatesToCapability()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var treeCapability = new GetWorkItemTreeCapability(mockClient.Object);
			var areaPathCapabilityMock = new Mock<IAgentToolCapability<GetWorkItemsByAreaPathCapabilityInput>>();

			var tool = new AzureDevOpsTool(mockClient.Object, treeCapability, areaPathCapabilityMock.Object);
			var inputJson = JsonSerializer.Serialize(new { areaPath = "Project\\Team\\Area" });
			var expectedResult = "[]";

			areaPathCapabilityMock
				.Setup(c => c.ExecuteAsync(It.IsAny<GetWorkItemsByAreaPathCapabilityInput>()))
				.ReturnsAsync(expectedResult);

			var result = await tool.ExecuteAsync("GetWorkItemsByAreaPath", inputJson);

			Assert.Equal(expectedResult, result);
			areaPathCapabilityMock.Verify(c => c.ExecuteAsync(It.IsAny<GetWorkItemsByAreaPathCapabilityInput>()), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_GetParentHierarchy_CallsClientWithParsedInputs()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			var clientObjects = new[]
			{
				new
				{
					id = 1,
					fields = new Dictionary<string, object?>
					{
						["System.Title"] = "Parent",
						["System.WorkItemType"] = "Epic",
						["System.AreaPath"] = "proj/team"
					},
					level = 0
				},
				new
				{
					id = 42,
					fields = new Dictionary<string, object?>
					{
						["System.Title"] = "Child",
						["System.WorkItemType"] = "Feature",
						["System.AreaPath"] = "proj/team/area"
					},
					level = 1
				}
			};
			var clientResult = JsonSerializer.Serialize(clientObjects);

			int capturedId = 0;
			IEnumerable<string>? capturedFields = null;

			mockClient
				.Setup(x => x.GetParentHierarchyAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.Callback<int, IEnumerable<string>?>((id, fields) =>
				{
					capturedId = id;
					capturedFields = fields;
				})
				.ReturnsAsync(clientResult);

			var tool = CreateTool(mockClient);
			var input = JsonSerializer.Serialize(new
			{
				workItemId = 42,
				fields = new[] { "System.Id", "System.Title" }
			});

			var result = await tool.ExecuteAsync("GetParentHierarchy", input);

			// Assert inputs to client
			Assert.Equal(42, capturedId);
			Assert.NotNull(capturedFields);
			Assert.Contains("System.Id", capturedFields!);
			Assert.Contains("System.Title", capturedFields!);

			// Assert transformed minimal output
			using var doc = JsonDocument.Parse(result);
			var root = doc.RootElement;
			Assert.Equal(JsonValueKind.Array, root.ValueKind);
			Assert.Equal(2, root.GetArrayLength());

			var parent = root[0];
			Assert.Equal(1, parent.GetProperty("Id").GetInt32());
			Assert.Equal("Parent", parent.GetProperty("Title").GetString());
			Assert.Equal("Epic", parent.GetProperty("WorkItemType").GetString());
			Assert.Equal("proj/team", parent.GetProperty("AreaPath").GetString());
			Assert.Equal(0, parent.GetProperty("Level").GetInt32());

			var child = root[1];
			Assert.Equal(42, child.GetProperty("Id").GetInt32());
			Assert.Equal("Child", child.GetProperty("Title").GetString());
			Assert.Equal("Feature", child.GetProperty("WorkItemType").GetString());
			Assert.Equal("proj/team/area", child.GetProperty("AreaPath").GetString());
			Assert.Equal(1, child.GetProperty("Level").GetInt32());
		}

		[Fact]
		public async Task ExecuteAsync_GetParentHierarchy_ThrowsIfWorkItemIdMissingOrInvalid()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var tool = CreateTool(mockClient);

			// Missing workItemId
			await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync("GetParentHierarchy", "{}"));

			// Non-convertible string
			var nonIntId = JsonSerializer.Serialize(new { workItemId = "abc" });
			await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync("GetParentHierarchy", nonIntId));

			// Wrong type (e.g., object)
			var wrongTypeId = JsonSerializer.Serialize(new { workItemId = new { id = 1 } });
			await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync("GetParentHierarchy", wrongTypeId));
		}

		[Fact]
		public async Task ExecuteAsync_GetFullHierarchy_CallsParentAndBuildsSubtree()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			// Parent chain JSON (already in the final serialized form from client)
			var parentJson = "[{\"id\":10,\"fields\":{\"System.Id\":10}},{\"id\":20,\"fields\":{\"System.Id\":20}}]";

			mockClient
				.Setup(c => c.GetParentHierarchyAsync(42, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(parentJson);

			// For the subtree, AzureDevOpsTool will call GetWorkItemAsync on the client.
			// We simulate a simple task with no children.
			var workItemJson = "{\"id\":42,\"fields\":{\"System.WorkItemType\":\"Task\"}}";
			mockClient
				.Setup(c => c.GetWorkItemAsync(42))
				.ReturnsAsync(workItemJson);
			mockClient
				.Setup(c => c.GetWorkItemAsync(42, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(workItemJson);

			var tool = CreateTool(mockClient);
			var input = JsonSerializer.Serialize(new
			{
				workItemId = 42,
				depth = 0,
				fields = new[] { "System.Id" }
			});

			var result = await tool.ExecuteAsync("GetFullHierarchy", input);

			// Result should contain both parents and children keys.
			Assert.Contains("\"parents\"", result);
			Assert.Contains("\"children\"", result);
			Assert.Contains("\"id\":10", result);
			Assert.Contains("\"id\":42", result);

			mockClient.Verify(c => c.GetParentHierarchyAsync(42, It.IsAny<IEnumerable<string>>()), Times.Once);
			mockClient.Verify(c => c.GetWorkItemAsync(42), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_GetFullHierarchy_ThrowsIfWorkItemIdMissingOrInvalid()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var tool = CreateTool(mockClient);

			// Missing workItemId
			await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync("GetFullHierarchy", "{}"));

			// Non-convertible workItemId
			var nonIntId = JsonSerializer.Serialize(new { workItemId = "abc" });
			await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync("GetFullHierarchy", nonIntId));

			// Wrong type for workItemId
			var wrongTypeId = JsonSerializer.Serialize(new { workItemId = new { id = 1 } });
			await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync("GetFullHierarchy", wrongTypeId));
		}

		[Fact]
		public async Task Execute_GetWorkItemsByAreaPath_PassesPagingAndReturnsMinimalFields()
		{
			// Arrange
			var clientMock = new Mock<IAzureDevOpsWorkItemClient>(MockBehavior.Strict);
			var tool = CreateTool(clientMock);

			var input = JsonSerializer.Serialize(new
			{
				areaPath = "proj/team/area",
				workItemTypes = new[] { "Feature" },
				fields = new[] { "System.Id", "System.Title", "System.WorkItemType", "System.AreaPath" },
				pageNumber = 2,
				pageSize = 5
			});

			// Client returns full shape from Azure DevOps
			var clientObjects = new []
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

			// Act
			var json = await tool.ExecuteAsync("GetWorkItemsByAreaPath", input);

			// Assert: ensure we requested with correct paging and that output is minimal
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

			// Should not surface relations from the client response
			Assert.False(first.TryGetProperty("relations", out _));
		}

		[Fact]
		public async Task Execute_GetParentHierarchy_ReturnsMinimalFieldsPerNode()
		{
			// Arrange
			var clientMock = new Mock<IAzureDevOpsWorkItemClient>(MockBehavior.Strict);
			var tool = CreateTool(clientMock);

			var input = JsonSerializer.Serialize(new
			{
				workItemId = 42,
				fields = new[] { "System.Id", "System.Title", "System.WorkItemType", "System.AreaPath" }
			});

			var parentChildObjects = new []
			{
				new
				{
					id = 1,
					fields = new Dictionary<string, object?>
					{
						["System.Title"] = "Parent",
						["System.WorkItemType"] = "Epic",
						["System.AreaPath"] = "proj/team"
					},
					level = 0
				},
				new
				{
					id = 42,
					fields = new Dictionary<string, object?>
					{
						["System.Title"] = "Child",
						["System.WorkItemType"] = "Feature",
						["System.AreaPath"] = "proj/team/area"
					},
					level = 1
				}
			};
			var clientResult = JsonSerializer.Serialize(parentChildObjects);

			clientMock
				.Setup(c => c.GetParentHierarchyAsync(
					42,
					It.Is<IEnumerable<string>>(f => f.Contains("System.Id") && f.Contains("System.Title"))))
				.ReturnsAsync(clientResult);

			// Act
			var json = await tool.ExecuteAsync("GetParentHierarchy", input);

			// Assert
			clientMock.VerifyAll();

			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			Assert.Equal(JsonValueKind.Array, root.ValueKind);
			Assert.Equal(2, root.GetArrayLength());

			var parent = root[0];
			Assert.Equal(1, parent.GetProperty("Id").GetInt32());
			Assert.Equal("Parent", parent.GetProperty("Title").GetString());
			Assert.Equal("Epic", parent.GetProperty("WorkItemType").GetString());
			Assert.Equal("proj/team", parent.GetProperty("AreaPath").GetString());
			Assert.Equal(0, parent.GetProperty("Level").GetInt32());

			var child = root[1];
			Assert.Equal(42, child.GetProperty("Id").GetInt32());
			Assert.Equal("Child", child.GetProperty("Title").GetString());
			Assert.Equal("Feature", child.GetProperty("WorkItemType").GetString());
			Assert.Equal("proj/team/area", child.GetProperty("AreaPath").GetString());
			Assert.Equal(1, child.GetProperty("Level").GetInt32());
		}
	}
}
