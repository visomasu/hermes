using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Integrations.AzureDevOps;
using Moq;
using System.Text.Json;
using Xunit;

namespace Hermes.Tests.Tools.AzureDevOps.Capabilities
{
	public class DiscoverUserActivityCapabilityTests
	{
		private const string TestUserEmail = "test.user@example.com";

		private static string CreateWorkItemsJson(int[] ids, string workItemType = "Task")
		{
			var items = ids.Select(id => new
			{
				id,
				fields = new Dictionary<string, object?>
				{
					["System.Title"] = $"Work Item {id}",
					["System.State"] = "Active",
					["System.WorkItemType"] = workItemType,
					["System.AreaPath"] = "Project\\Team",
					["System.AssignedTo"] = TestUserEmail,
					["System.CreatedBy"] = TestUserEmail,
					["System.CreatedDate"] = "2026-01-20T10:00:00Z",
					["System.ChangedBy"] = TestUserEmail,
					["System.ChangedDate"] = "2026-01-23T15:30:00Z"
				}
			});
			return JsonSerializer.Serialize(new { count = ids.Length, value = items });
		}

		[Fact]
		public void Name_ReturnsCorrectCapabilityName()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var capability = new DiscoverUserActivityCapability(mockClient.Object);

			// Act
			var name = capability.Name;

			// Assert
			Assert.Equal("DiscoverUserActivity", name);
		}

		[Fact]
		public void Description_ReturnsNonEmptyDescription()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var capability = new DiscoverUserActivityCapability(mockClient.Object);

			// Act
			var description = capability.Description;

			// Assert
			Assert.NotNull(description);
			Assert.NotEmpty(description);
			Assert.Contains("activity", description, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task ExecuteAsync_ValidInput_ReturnsGroupedResults()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			mockClient.Setup(x => x.GetWorkItemsByAssignedUserAsync(
					TestUserEmail,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					null,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateWorkItemsJson(new[] { 1, 2 }));

			mockClient.Setup(x => x.GetWorkItemsChangedByUserAsync(
					TestUserEmail,
					It.IsAny<int>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateWorkItemsJson(new[] { 3, 4, 5 }));

			mockClient.Setup(x => x.GetWorkItemsCreatedByUserAsync(
					TestUserEmail,
					It.IsAny<int>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateWorkItemsJson(new[] { 6 }));

			var capability = new DiscoverUserActivityCapability(mockClient.Object);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				DaysBack = 7,
				ActivityTypes = UserActivityType.AllWorkItems
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(result);
			using var doc = JsonDocument.Parse(result);
			var root = doc.RootElement;

			Assert.Equal(TestUserEmail, root.GetProperty("userEmail").GetString());
			Assert.Equal(7, root.GetProperty("periodDays").GetInt32());

			var workItems = root.GetProperty("workItems");
			Assert.Equal(2, workItems.GetProperty("assigned").GetArrayLength());
			Assert.Equal(3, workItems.GetProperty("changed").GetArrayLength());
			Assert.Equal(1, workItems.GetProperty("created").GetArrayLength());
		}

		[Fact]
		public async Task ExecuteAsync_EmptyUserEmail_ThrowsArgumentException()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var capability = new DiscoverUserActivityCapability(mockClient.Object);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = "",
				DaysBack = 7
			};

			// Act & Assert
			await Assert.ThrowsAsync<ArgumentException>(() => capability.ExecuteAsync(input));
		}

		[Fact]
		public async Task ExecuteAsync_DefaultDaysBack_UsesSevenDays()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			int capturedDaysBack = 0;

			mockClient.Setup(x => x.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					null,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateWorkItemsJson(Array.Empty<int>()));

			mockClient.Setup(x => x.GetWorkItemsChangedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.Callback<string, int, IEnumerable<string>?, IEnumerable<string>?, IEnumerable<string>?, CancellationToken>(
					(_, days, _, _, _, _) => capturedDaysBack = days)
				.ReturnsAsync(CreateWorkItemsJson(Array.Empty<int>()));

			mockClient.Setup(x => x.GetWorkItemsCreatedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateWorkItemsJson(Array.Empty<int>()));

			var capability = new DiscoverUserActivityCapability(mockClient.Object);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				DaysBack = 0 // Should default to 7
			};

			// Act
			await capability.ExecuteAsync(input);

			// Assert
			Assert.Equal(7, capturedDaysBack);
		}

		[Fact]
		public async Task ExecuteAsync_OnlyAssignedActivityType_CallsOnlyAssignedClient()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			mockClient.Setup(x => x.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					null,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateWorkItemsJson(new[] { 1 }));

			var capability = new DiscoverUserActivityCapability(mockClient.Object);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				ActivityTypes = UserActivityType.WorkItemsAssigned
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			mockClient.Verify(x => x.GetWorkItemsByAssignedUserAsync(
				It.IsAny<string>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<IEnumerable<string>?>(),
				null,
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<CancellationToken>()), Times.Once);

			mockClient.Verify(x => x.GetWorkItemsChangedByUserAsync(
				It.IsAny<string>(),
				It.IsAny<int>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<CancellationToken>()), Times.Never);

			mockClient.Verify(x => x.GetWorkItemsCreatedByUserAsync(
				It.IsAny<string>(),
				It.IsAny<int>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<CancellationToken>()), Times.Never);

			using var doc = JsonDocument.Parse(result);
			var workItems = doc.RootElement.GetProperty("workItems");
			Assert.Equal(1, workItems.GetProperty("assigned").GetArrayLength());
			Assert.Equal(JsonValueKind.Null, workItems.GetProperty("changed").ValueKind);
			Assert.Equal(JsonValueKind.Null, workItems.GetProperty("created").ValueKind);
		}

		[Fact]
		public async Task ExecuteAsync_NoResultsFound_ReturnsEmptyArrays()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			var emptyResult = JsonSerializer.Serialize(new { count = 0, value = Array.Empty<object>() });

			mockClient.Setup(x => x.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					null,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(emptyResult);

			mockClient.Setup(x => x.GetWorkItemsChangedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(emptyResult);

			mockClient.Setup(x => x.GetWorkItemsCreatedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(emptyResult);

			var capability = new DiscoverUserActivityCapability(mockClient.Object);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				DaysBack = 7,
				ActivityTypes = UserActivityType.AllWorkItems
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			using var doc = JsonDocument.Parse(result);
			var workItems = doc.RootElement.GetProperty("workItems");
			Assert.Equal(0, workItems.GetProperty("assigned").GetArrayLength());
			Assert.Equal(0, workItems.GetProperty("changed").GetArrayLength());
			Assert.Equal(0, workItems.GetProperty("created").GetArrayLength());
		}

		[Fact]
		public async Task ExecuteAsync_CustomDaysBack_PassesCorrectValue()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			int capturedDaysBack = 0;

			mockClient.Setup(x => x.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					null,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateWorkItemsJson(Array.Empty<int>()));

			mockClient.Setup(x => x.GetWorkItemsChangedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.Callback<string, int, IEnumerable<string>?, IEnumerable<string>?, IEnumerable<string>?, CancellationToken>(
					(_, days, _, _, _, _) => capturedDaysBack = days)
				.ReturnsAsync(CreateWorkItemsJson(Array.Empty<int>()));

			mockClient.Setup(x => x.GetWorkItemsCreatedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateWorkItemsJson(Array.Empty<int>()));

			var capability = new DiscoverUserActivityCapability(mockClient.Object);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				DaysBack = 30,
				ActivityTypes = UserActivityType.AllWorkItems
			};

			// Act
			await capability.ExecuteAsync(input);

			// Assert
			Assert.Equal(30, capturedDaysBack);
		}

		[Fact]
		public async Task ExecuteAsync_WorkItemOptions_PassesToClient()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
			IEnumerable<string>? capturedTypes = null;
			IEnumerable<string>? capturedStates = null;

			mockClient.Setup(x => x.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					null,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.Callback<string, IEnumerable<string>?, IEnumerable<string>?, string?, IEnumerable<string>?, CancellationToken>(
					(_, states, _, _, types, _) =>
					{
						capturedStates = states;
						capturedTypes = types;
					})
				.ReturnsAsync(CreateWorkItemsJson(Array.Empty<int>()));

			var capability = new DiscoverUserActivityCapability(mockClient.Object);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				ActivityTypes = UserActivityType.WorkItemsAssigned,
				WorkItemOptions = new WorkItemActivityOptions
				{
					States = new[] { "Active", "New" },
					WorkItemTypes = new[] { "Bug", "Task" }
				}
			};

			// Act
			await capability.ExecuteAsync(input);

			// Assert
			Assert.NotNull(capturedTypes);
			Assert.Contains("Bug", capturedTypes);
			Assert.Contains("Task", capturedTypes);
			Assert.NotNull(capturedStates);
			Assert.Contains("Active", capturedStates);
			Assert.Contains("New", capturedStates);
		}

		[Fact]
		public async Task ExecuteAsync_CombinedActivityTypes_CallsCorrectMethods()
		{
			// Arrange
			var mockClient = new Mock<IAzureDevOpsWorkItemClient>();

			mockClient.Setup(x => x.GetWorkItemsByAssignedUserAsync(
					It.IsAny<string>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					null,
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateWorkItemsJson(new[] { 1 }));

			mockClient.Setup(x => x.GetWorkItemsChangedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<IEnumerable<string>?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateWorkItemsJson(new[] { 2 }));

			var capability = new DiscoverUserActivityCapability(mockClient.Object);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				ActivityTypes = UserActivityType.WorkItemsAssigned | UserActivityType.WorkItemsChanged
			};

			// Act
			var result = await capability.ExecuteAsync(input);

			// Assert
			mockClient.Verify(x => x.GetWorkItemsByAssignedUserAsync(
				It.IsAny<string>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<IEnumerable<string>?>(),
				null,
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<CancellationToken>()), Times.Once);

			mockClient.Verify(x => x.GetWorkItemsChangedByUserAsync(
				It.IsAny<string>(),
				It.IsAny<int>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<CancellationToken>()), Times.Once);

			mockClient.Verify(x => x.GetWorkItemsCreatedByUserAsync(
				It.IsAny<string>(),
				It.IsAny<int>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<IEnumerable<string>?>(),
				It.IsAny<CancellationToken>()), Times.Never);

			using var doc = JsonDocument.Parse(result);
			var workItems = doc.RootElement.GetProperty("workItems");
			Assert.Equal(1, workItems.GetProperty("assigned").GetArrayLength());
			Assert.Equal(1, workItems.GetProperty("changed").GetArrayLength());
			Assert.Equal(JsonValueKind.Null, workItems.GetProperty("created").ValueKind);
		}
	}
}
