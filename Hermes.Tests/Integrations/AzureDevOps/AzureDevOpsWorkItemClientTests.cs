using Exceptions;
using Integrations.AzureDevOps;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using System.Text.Json;
using System.Threading;
using Xunit;

namespace Hermes.Tests.Integrations.AzureDevOps
{
	public class AzureDevOpsWorkItemClientTests
	{
		[Fact]
		public async Task GetWorkItemAsync_ThrowsIntegrationException_OnError()
		{
			var client = new AzureDevOpsWorkItemClient("invalidOrg", "invalidProject", "invalidPat");
			await Assert.ThrowsAsync<IntegrationException>(() => client.GetWorkItemAsync(-1));
		}

		[Fact]
		public async Task GetWorkItemsAsync_ThrowsIntegrationException_OnNullIds()
		{
			var client = new AzureDevOpsWorkItemClient("invalidOrg", "invalidProject", "invalidPat");
			await Assert.ThrowsAsync<IntegrationException>(() => client.GetWorkItemsAsync(null!, null));
		}

		[Fact]
		public async Task GetWorkItemsAsync_ThrowsIntegrationException_OnEmptyIds()
		{
			var client = new AzureDevOpsWorkItemClient("invalidOrg", "invalidProject", "invalidPat");
			await Assert.ThrowsAsync<IntegrationException>(() => client.GetWorkItemsAsync(Array.Empty<int>(), null));
		}

		[Fact]
		public async Task GetWorkItemsByAreaPathAsync_ThrowsIntegrationException_OnNullOrWhitespaceAreaPath()
		{
			var client = new AzureDevOpsWorkItemClient("invalidOrg", "invalidProject", "invalidPat");

			// Null or empty/whitespace areaPath should result in an IntegrationException from validation.
			await Assert.ThrowsAsync<IntegrationException>(() => client.GetWorkItemsByAreaPathAsync(null!));
			await Assert.ThrowsAsync<IntegrationException>(() => client.GetWorkItemsByAreaPathAsync(string.Empty));
			await Assert.ThrowsAsync<IntegrationException>(() => client.GetWorkItemsByAreaPathAsync("   "));
		}

		[Fact]
		public async Task GetWorkItemsByAreaPathAsync_BuildsExpectedWiqlAndCallsGetWorkItems()
		{
			// Arrange: create a testable client and inject a mocked WorkItemTrackingHttpClient
			var organization = "org";
			var project = "proj";
			var pat = "token";

			var queryResult = new WorkItemQueryResult
			{
				WorkItems = new List<WorkItemReference>
				{
					new WorkItemReference { Id = 1 },
					new WorkItemReference { Id = 2 }
				}
			};

			Wiql? capturedWiql = null;
			string? capturedProject = null;

			var returnedItems = new List<WorkItem>
			{
				new WorkItem { Id = 1, Rev = 1, Fields = new Dictionary<string, object?> { { "System.Id", 1 }, { "System.Title", "Item 1" } } },
				new WorkItem { Id = 2, Rev = 1, Fields = new Dictionary<string, object?> { { "System.Id", 2 }, { "System.Title", "Item 2" } } },
			};

			IReadOnlyList<int>? capturedIds = null;

			// Real connection instance (we won't call GetClient on it in the test)
			var connection = new VssConnection(
				new Uri($"https://dev.azure.com/{organization}"),
				new VssBasicCredential(string.Empty, pat));

			// Test client that allows us to inject connection and work item client via reflection
			var testClient = new TestableAzureDevOpsWorkItemClient(connection, project);

			var witClientMock = new Mock<WorkItemTrackingHttpClient>(MockBehavior.Strict, new Uri("https://dev.azure.com/org"), new VssCredentials());

			witClientMock
				.Setup(c => c.QueryByWiqlAsync(
					It.IsAny<Wiql>(),
					It.IsAny<string>(),
					It.IsAny<bool?>(),
					It.IsAny<int?>(),
					It.IsAny<object>(),
					It.IsAny<System.Threading.CancellationToken>()))
				.Callback<Wiql, string, bool?, int?, object, System.Threading.CancellationToken>(
					(wiql, projArg, _, _, _, _) =>
					{
						capturedWiql = wiql;
						capturedProject = projArg;
					})
				.ReturnsAsync(queryResult);

			witClientMock
				.Setup(c => c.GetWorkItemsAsync(
					It.IsAny<IEnumerable<int>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<DateTime?>(),
					It.IsAny<WorkItemExpand?>(),
					It.IsAny<WorkItemErrorPolicy?>(),
					It.IsAny<object>(),
					It.IsAny<System.Threading.CancellationToken>()))
				.Callback<IEnumerable<int>, IEnumerable<string>, DateTime?, WorkItemExpand?, WorkItemErrorPolicy?, object, System.Threading.CancellationToken>(
					(ids, _, _, _, _, _, _) =>
					{
						capturedIds = ids.ToList();
					})
				.ReturnsAsync(returnedItems);

			// Inject mocked WorkItemTrackingHttpClient into the test client by setting the private _workItemClient field
			var clientType = typeof(AzureDevOpsWorkItemClient);
			var workItemClientField = clientType.GetField("_workItemClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			workItemClientField!.SetValue(testClient, witClientMock.Object);
 
			// Act
			var json = await testClient.GetWorkItemsByAreaPathAsync("proj\\team\\area", new[] { "Feature", "User Story" }, new[] { "System.Id", "System.Title" }, pageNumber: 1, pageSize: 2);

			// Assert: WIQL contains expected clauses and project, and GetWorkItemsAsync called with ids 1 and 2
			Assert.NotNull(capturedWiql);
			Assert.Equal(project, capturedProject);
			Assert.Contains("[System.TeamProject] = @project", capturedWiql!.Query);
			Assert.Contains("[System.AreaPath] UNDER 'proj\\team\\area'", capturedWiql.Query);
			Assert.Contains("[System.WorkItemType] = 'Feature'", capturedWiql.Query);
			Assert.Contains("[System.WorkItemType] = 'User Story'", capturedWiql.Query);

			Assert.NotNull(capturedIds);
			Assert.Equal(new[] { 1, 2 }, capturedIds!.OrderBy(id => id));
			Assert.Contains("id\":1", json);
			Assert.Contains("id\":2", json);
		}

		[Fact]
		public async Task GetWorkItemsByAreaPathAsync_AppliesPagingToQueryResults()
		{
			// Arrange: 3 work item references, but paging asks for the middle one only (pageNumber=2, pageSize=1)
			var organization = "org";
			var project = "proj";
			var pat = "token";

			var queryResult = new WorkItemQueryResult
			{
				WorkItems = new List<WorkItemReference>
				{
					new WorkItemReference { Id = 1 },
					new WorkItemReference { Id = 2 },
					new WorkItemReference { Id = 3 }
				}
			};

			var returnedItems = new List<WorkItem>
			{
				new WorkItem { Id = 2, Rev = 1, Fields = new Dictionary<string, object?> { { "System.Id", 2 }, { "System.Title", "Item 2" } } }
			};

			IReadOnlyList<int>? capturedIds = null;

			var connection = new VssConnection(
				new Uri($"https://dev.azure.com/{organization}"),
				new VssBasicCredential(string.Empty, pat));

			var testClient = new TestableAzureDevOpsWorkItemClient(connection, project);

			var witClientMock = new Mock<WorkItemTrackingHttpClient>(MockBehavior.Strict, new Uri("https://dev.azure.com/org"), new VssCredentials());

			witClientMock
				.Setup(c => c.QueryByWiqlAsync(
					It.IsAny<Wiql>(),
					It.IsAny<string>(),
					It.IsAny<bool?>(),
					It.IsAny<int?>(),
					It.IsAny<object>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(queryResult);

			witClientMock
				.Setup(c => c.GetWorkItemsAsync(
					It.IsAny<IEnumerable<int>>(),
					It.IsAny<IEnumerable<string>>(),
					It.IsAny<DateTime?>(),
					It.IsAny<WorkItemExpand?>(),
					It.IsAny<WorkItemErrorPolicy?>(),
					It.IsAny<object>(),
					It.IsAny<CancellationToken>()))
				.Callback<IEnumerable<int>, IEnumerable<string>, DateTime?, WorkItemExpand?, WorkItemErrorPolicy?, object, CancellationToken>(
					(ids, _, _, _, _, _, _) =>
					{
						capturedIds = ids.ToList();
					})
				.ReturnsAsync(returnedItems);

			var clientType = typeof(AzureDevOpsWorkItemClient);
			var workItemClientField = clientType.GetField("_workItemClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			workItemClientField!.SetValue(testClient, witClientMock.Object);

			// Act: pageNumber=2, pageSize=1 => skip first, take one (id 2)
			var json = await testClient.GetWorkItemsByAreaPathAsync("proj\\team\\area", null, new[] { "System.Id", "System.Title" }, pageNumber: 2, pageSize: 1);

			// Assert: only id 2 requested and present in json
			Assert.NotNull(capturedIds);
			Assert.Single(capturedIds!);
			Assert.Equal(2, capturedIds!.Single());
			Assert.Contains("id\":2", json);
		}

		[Fact]
		public async Task GetParentHierarchyAsync_WalksUpParentChainAndReturnsOrderedHierarchy()
		{
			// Arrange
			var organization = "org";
			var project = "proj";
			var pat = "token";

			var connection = new VssConnection(
				new Uri($"https://dev.azure.com/{organization}"),
				new VssBasicCredential(string.Empty, pat));

			var testClient = new TestableAzureDevOpsWorkItemClient(connection, project);

			var witClientMock = new Mock<WorkItemTrackingHttpClient>(MockBehavior.Strict, new Uri("https://dev.azure.com/org"), new VssCredentials());

			// Parent (id=1) has no parent relation
			var parent = new WorkItem
			{
				Id = 1,
				Rev = 1,
				Fields = new Dictionary<string, object?>
				{
					{ "System.Id", 1 },
					{ "System.Title", "Parent" },
					{ "System.WorkItemType", "Feature" }
				},
				Relations = new List<WorkItemRelation>()
			};

			// Child (id=2) points to parent via Hierarchy-Reverse
			var child = new WorkItem
			{
				Id = 2,
				Rev = 1,
				Fields = new Dictionary<string, object?>
				{
					{ "System.Id", 2 },
					{ "System.Title", "Child" },
					{ "System.WorkItemType", "User Story" }
				},
				Relations = new List<WorkItemRelation>
				{
					new WorkItemRelation
					{
						Rel = "System.LinkTypes.Hierarchy-Reverse",
						Url = "https://dev.azure.com/org/proj/_apis/wit/workItems/1"
					}
				}
			};

			// Updated setups to match the 6-parameter overload used in production code.
			witClientMock
				.Setup(c => c.GetWorkItemAsync(
					2,
					null,
					null,
					WorkItemExpand.Relations,
					null,
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(child);

			witClientMock
				.Setup(c => c.GetWorkItemAsync(
					1,
					null,
					null,
					WorkItemExpand.Relations,
					null,
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(parent);

			var clientType = typeof(AzureDevOpsWorkItemClient);
			var workItemClientField = clientType.GetField("_workItemClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			workItemClientField!.SetValue(testClient, witClientMock.Object);

			// Act
			var json = await testClient.GetParentHierarchyAsync(2, new[] { "System.Id", "System.Title", "System.WorkItemType" });

			// Assert
			Assert.Contains("\"id\":1", json); // parent first
			Assert.Contains("\"id\":2", json); // then child
			var parentIndex = json.IndexOf("\"id\":1", StringComparison.Ordinal);
			var childIndex = json.IndexOf("\"id\":2", StringComparison.Ordinal);
			Assert.True(parentIndex < childIndex);
		}

		// Test-only subclass to allow injecting a mocked VssConnection
		private sealed class TestableAzureDevOpsWorkItemClient : AzureDevOpsWorkItemClient
		{
			public TestableAzureDevOpsWorkItemClient(VssConnection connection, string project)
				: base("org", project, "token")
			{
				// Overwrite the internal connection and project fields via reflection for testing.
				var type = typeof(AzureDevOpsWorkItemClient);
				var connectionField = type.GetField("_connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				var projectField = type.GetField("_project", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				connectionField!.SetValue(this, connection);
				projectField!.SetValue(this, project);
			}
		}
	}
}
