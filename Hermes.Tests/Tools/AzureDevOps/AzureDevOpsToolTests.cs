using System.Text.Json;
using Hermes.Tools.AzureDevOps;
using Integrations.AzureDevOps;
using Moq;
using Xunit;

namespace Hermes.Tests.Tools.AzureDevOps
{
	public class AzureDevOpsToolTests
	{
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
			var tool = new AzureDevOpsTool(mockClient.Object);

			Assert.Equal("AzureDevOpsTool", tool.Name);
			Assert.Contains("retrieving work item trees", tool.Description);
			Assert.Contains("GetWorkItemTree", tool.Capabilities);
			Assert.Contains("GetWorkItemsByAreaPath", tool.Capabilities);
			Assert.Contains("GetParentHierarchy", tool.Capabilities);
			Assert.Contains("GetFullHierarchy", tool.Capabilities);
			Assert.Contains("Capabilities: [GetWorkItemTree, GetWorkItemsByAreaPath, GetParentHierarchy, GetFullHierarchy]", tool.GetMetadata());
		}

		[Fact]
		public async Task ExecuteAsync_GetWorkItemTree_ReturnsTreeJson()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			// Simulate a root work item with one child relation
			string rootJson = "{ \"id\":1, \"fields\":{\"System.WorkItemType\":\"Feature\"}, \"relations\": [ { \"rel\": \"System.LinkTypes.Hierarchy-Forward\", \"url\": \"http://dev.azure.com/_apis/wit/workItems/2\", \"attributes\": { \"name\": \"Child\" } } ] }";
			string childJson = "{ \"id\":2, \"fields\":{\"System.WorkItemType\":\"Task\"} }";
			mockClient.Setup(x => x.GetWorkItemAsync(1)).ReturnsAsync(rootJson);
			mockClient.Setup(x => x.GetWorkItemAsync(1, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(rootJson);
			mockClient.Setup(x => x.GetWorkItemAsync(2)).ReturnsAsync(childJson);
			mockClient.Setup(x => x.GetWorkItemAsync(2, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(childJson);

			var tool = new AzureDevOpsTool(mockClient.Object);
			var input = JsonSerializer.Serialize(new { rootId = 1, depth = 1 });
			var result = await tool.ExecuteAsync("GetWorkItemTree", input);

			Assert.Contains("workItem", result);
			Assert.Contains("children", result);
			Assert.Contains("id", result);
		}

		[Fact]
		public async Task ExecuteAsync_UnsupportedOperation_Throws()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var tool = new AzureDevOpsTool(mockClient.Object);
			await Assert.ThrowsAsync<NotSupportedException>(() => tool.ExecuteAsync("UnknownOp", "{}"));
		}

		[Fact]
		public async Task ExecuteAsync_GetWorkItemTree_RequestsCorrectFieldsByType()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			// Setup work items with different types
			string featureJson = "{ \"id\":1, \"fields\":{\"System.WorkItemType\":\"Feature\"}, \"relations\": [ { \"rel\": \"System.LinkTypes.Hierarchy-Forward\", \"url\": \"http://dev.azure.com/_apis/wit/workItems/2\", \"attributes\": { \"name\": \"Child\" } } ] }";
			string userStoryJson = "{ \"id\":2, \"fields\":{\"System.WorkItemType\":\"User Story\"}, \"relations\": [ { \"rel\": \"System.LinkTypes.Hierarchy-Forward\", \"url\": \"http://dev.azure.com/_apis/wit/workItems/3\", \"attributes\": { \"name\": \"Child\" } } ] }";
			string taskJson = "{ \"id\":3, \"fields\":{\"System.WorkItemType\":\"Task\"} }";

			mockClient.Setup(x => x.GetWorkItemAsync(1)).ReturnsAsync(featureJson);
			mockClient.Setup(x => x.GetWorkItemAsync(2)).ReturnsAsync(userStoryJson);
			mockClient.Setup(x => x.GetWorkItemAsync(3)).ReturnsAsync(taskJson);

			var requestedFields = new Dictionary<int, IEnumerable<string>>();
			mockClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
				.Callback<int, IEnumerable<string>>((id, fields) => requestedFields[id] = (fields ?? Enumerable.Empty<string>()).ToList())
				.ReturnsAsync((int id, IEnumerable<string> fields) =>
				{
					if (id == 1) return featureJson;
					if (id == 2) return userStoryJson;
					if (id == 3) return taskJson;
					return "{}";
				});

			var tool = new AzureDevOpsTool(mockClient.Object);
			var input = JsonSerializer.Serialize(new { rootId = 1, depth = 2 });
			await tool.ExecuteAsync("GetWorkItemTree", input);

			// Validate correct fields requested for each type
			Assert.True(requestedFields.ContainsKey(1));
			Assert.True(requestedFields.ContainsKey(2));
			Assert.True(requestedFields.ContainsKey(3));

			var featureFields = requestedFields[1];
			var userStoryFields = requestedFields[2];
			var taskFields = requestedFields[3];

			Assert.Contains("System.WorkItemType", featureFields);
			Assert.Contains("System.Description", featureFields);
			Assert.Contains("Microsoft.VSTS.Scheduling.StartDate", featureFields);
			Assert.Contains("System.WorkItemType", userStoryFields);
			Assert.Contains("System.Description", userStoryFields);
			Assert.Contains("Microsoft.VSTS.Scheduling.TargetDate", userStoryFields);
			Assert.Contains("System.WorkItemType", taskFields);
			Assert.Contains("System.Description", taskFields);
			Assert.Contains("Microsoft.VSTS.Scheduling.FinishDate", taskFields);
		}

		[Fact]
		public async Task ExecuteAsync_GetWorkItemTree_FeatureFieldsReturned()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			string featureJson = "{ \"id\":1, \"fields\":{\"System.WorkItemType\":\"Feature\"} }";
			mockClient.Setup(x => x.GetWorkItemAsync(1)).ReturnsAsync(featureJson);
			mockClient.Setup(x => x.GetWorkItemAsync(1, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(featureJson);

			var requestedFields = new List<string>();
			mockClient.Setup(x => x.GetWorkItemAsync(1, It.IsAny<IEnumerable<string>>()))
				.Callback<int, IEnumerable<string>>((id, fields) => requestedFields = (fields ?? Enumerable.Empty<string>()).ToList())
				.ReturnsAsync(featureJson);

			var tool = new AzureDevOpsTool(mockClient.Object);
			var input = JsonSerializer.Serialize(new { rootId = 1, depth = 0 });
			await tool.ExecuteAsync("GetWorkItemTree", input);

			foreach (var field in FeatureFields)
			{
				Assert.Contains(field, requestedFields);
			}
		}

		[Fact]
		public async Task ExecuteAsync_GetWorkItemTree_UserStoryFieldsReturned()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			string userStoryJson = "{ \"id\":2, \"fields\":{\"System.WorkItemType\":\"User Story\"} }";
			mockClient.Setup(x => x.GetWorkItemAsync(2)).ReturnsAsync(userStoryJson);
			mockClient.Setup(x => x.GetWorkItemAsync(2, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(userStoryJson);

			var requestedFields = new List<string>();
			mockClient.Setup(x => x.GetWorkItemAsync(2, It.IsAny<IEnumerable<string>>()))
				.Callback<int, IEnumerable<string>>((id, fields) => requestedFields = (fields ?? Enumerable.Empty<string>()).ToList())
				.ReturnsAsync(userStoryJson);

			var tool = new AzureDevOpsTool(mockClient.Object);
			var input = JsonSerializer.Serialize(new { rootId = 2, depth = 0 });
			await tool.ExecuteAsync("GetWorkItemTree", input);

			foreach (var field in UserStoryFields)
			{
				Assert.Contains(field, requestedFields);
			}
		}

		[Fact]
		public async Task ExecuteAsync_GetWorkItemTree_TaskFieldsReturned()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			string taskJson = "{ \"id\":3, \"fields\":{\"System.WorkItemType\":\"Task\"} }";
			mockClient.Setup(x => x.GetWorkItemAsync(3)).ReturnsAsync(taskJson);
			mockClient.Setup(x => x.GetWorkItemAsync(3, It.IsAny<IEnumerable<string>>()))
				.ReturnsAsync(taskJson);

			var requestedFields = new List<string>();
			mockClient.Setup(x => x.GetWorkItemAsync(3, It.IsAny<IEnumerable<string>>()))
				.Callback<int, IEnumerable<string>>((id, fields) => requestedFields = (fields ?? Enumerable.Empty<string>()).ToList())
				.ReturnsAsync(taskJson);

			var tool = new AzureDevOpsTool(mockClient.Object);
			var input = JsonSerializer.Serialize(new { rootId = 3, depth = 0 });
			await tool.ExecuteAsync("GetWorkItemTree", input);

			foreach (var field in TaskFields)
			{
				Assert.Contains(field, requestedFields);
			}
		}

		[Fact]
		public async Task ExecuteAsync_GetWorkItemsByAreaPath_CallsClientWithParsedInputs()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			string expectedJson = "[{\"id\":1},{\"id\":2}]";

			IEnumerable<string>? capturedTypes = null;
			IEnumerable<string>? capturedFields = null;
			string? capturedAreaPath = null;

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
				})
				.ReturnsAsync(expectedJson);

			var tool = new AzureDevOpsTool(mockClient.Object);
			var input = JsonSerializer.Serialize(new
			{
				areaPath = "Project\\Team\\Area",
				workItemTypes = new[] { "Feature", "User Story" },
				fields = new[] { "System.Id", "System.Title" }
			});

			var result = await tool.ExecuteAsync("GetWorkItemsByAreaPath", input);

			// Assert that the client was called with correctly parsed inputs
			Assert.Equal("Project\\Team\\Area", capturedAreaPath);
			Assert.NotNull(capturedTypes);
			Assert.Contains("Feature", capturedTypes!);
			Assert.Contains("User Story", capturedTypes!);
			Assert.NotNull(capturedFields);
			Assert.Contains("System.Id", capturedFields!);
			Assert.Contains("System.Title", capturedFields!);

			// Optionally assert that the tool returned a JSON array
			using var doc = JsonDocument.Parse(result);
			var root = doc.RootElement;
			Assert.Equal(JsonValueKind.Array, root.ValueKind);
		}

		[Fact]
		public async Task ExecuteAsync_GetWorkItemsByAreaPath_ThrowsIfAreaPathMissingOrInvalid()
		{
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var tool = new AzureDevOpsTool(mockClient.Object);

			await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync("GetWorkItemsByAreaPath", "{}"));

			var nonStringAreaPath = JsonSerializer.Serialize(new { areaPath = 123 });
			await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync("GetWorkItemsByAreaPath", nonStringAreaPath));
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

			var tool = new AzureDevOpsTool(mockClient.Object);
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
			var tool = new AzureDevOpsTool(mockClient.Object);

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

			var tool = new AzureDevOpsTool(mockClient.Object);
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
			var tool = new AzureDevOpsTool(mockClient.Object);

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
			var tool = new AzureDevOpsTool(clientMock.Object);

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
			var tool = new AzureDevOpsTool(clientMock.Object);

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
