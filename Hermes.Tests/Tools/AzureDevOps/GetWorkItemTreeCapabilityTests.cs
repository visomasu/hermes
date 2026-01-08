using System.Text.Json;
using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Integrations.AzureDevOps;
using Moq;
using Xunit;

namespace Hermes.Tests.Tools.AzureDevOps
{
	public class GetWorkItemTreeCapabilityTests
	{
		[Fact]
		public async Task ExecuteAsync_ReturnsTreeJson()
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

			var capability = new GetWorkItemTreeCapability(mockClient.Object);
			var input = new GetWorkItemTreeCapabilityInput { WorkItemId = 1, Depth = 1 };
			var result = await capability.ExecuteAsync(input);

			Assert.Contains("workItem", result);
			Assert.Contains("children", result);
			Assert.Contains("id", result);
		}

		[Fact]
		public async Task ExecuteAsync_RequestsCorrectFieldsByType()
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

			var capability = new GetWorkItemTreeCapability(mockClient.Object);
			var input = new GetWorkItemTreeCapabilityInput { WorkItemId = 1, Depth = 2 };
			await capability.ExecuteAsync(input);

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
		public async Task ExecuteAsync_FeatureFieldsReturned()
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

			var capability = new GetWorkItemTreeCapability(mockClient.Object);
			var input = new GetWorkItemTreeCapabilityInput { WorkItemId = 1, Depth = 0 };
			await capability.ExecuteAsync(input);

			Assert.Contains("System.Id", requestedFields);
			Assert.Contains("System.Title", requestedFields);
		}

		[Fact]
		public async Task ExecuteAsync_UserStoryFieldsReturned()
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

			var capability = new GetWorkItemTreeCapability(mockClient.Object);
			var input = new GetWorkItemTreeCapabilityInput { WorkItemId = 2, Depth = 0 };
			await capability.ExecuteAsync(input);

			Assert.Contains("System.Id", requestedFields);
			Assert.Contains("System.Title", requestedFields);
		}

		[Fact]
		public async Task ExecuteAsync_TaskFieldsReturned()
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

			var capability = new GetWorkItemTreeCapability(mockClient.Object);
			var input = new GetWorkItemTreeCapabilityInput { WorkItemId = 3, Depth = 0 };
			await capability.ExecuteAsync(input);

			Assert.Contains("System.Id", requestedFields);
			Assert.Contains("System.Title", requestedFields);
		}
	}
}
