using Hermes.Tools.AzureDevOps;
using Integrations.AzureDevOps;
using Moq;
using System.Text.Json;
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
			Assert.Contains("Capabilities: [GetWorkItemTree]", tool.GetMetadata());
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
			var input = JsonSerializer.Serialize(new { rootId =1, depth =1 });
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
					if (id ==1) return featureJson;
					if (id ==2) return userStoryJson;
					if (id ==3) return taskJson;
					return "{}";
				});

			var tool = new AzureDevOpsTool(mockClient.Object);
			var input = JsonSerializer.Serialize(new { rootId =1, depth =2 });
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
			var input = JsonSerializer.Serialize(new { rootId =1, depth =0 });
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
			var input = JsonSerializer.Serialize(new { rootId =2, depth =0 });
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
			var input = JsonSerializer.Serialize(new { rootId =3, depth =0 });
			await tool.ExecuteAsync("GetWorkItemTree", input);

			foreach (var field in TaskFields)
			{
				Assert.Contains(field, requestedFields);
			}
		}
	}
}
