using System.Text.Json;
using Integrations.AzureDevOps;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;

namespace Hermes.Tools.AzureDevOps.Capabilities
{
	/// <summary>
	/// Capability for retrieving an Azure DevOps work item tree.
	/// </summary>
	public sealed class GetWorkItemTreeCapability : IAgentToolCapability<GetWorkItemTreeCapabilityInput>
	{
		private readonly IAzureDevOpsWorkItemClient _client;
		private readonly int _defaultDepth = 2;
		private readonly int _maxConcurrentFetches;

		// Static mapping of work item type to fields (mirrors AzureDevOpsTool behavior)
		private static readonly Dictionary<string, List<string>> FieldsByType = new()
		{
			{ "Epic", new List<string> { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "System.IterationPath", "Custom.PrivatePreviewDate", "Custom.PublicPreviewDate", "Custom.GAdate", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate", "Custom.CurrentStatus", "Custom.RiskAssessmentComment" } },
			{ "Feature", new List<string> { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "System.IterationPath", "Custom.PrivatePreviewDate", "Custom.PublicPreviewDate", "Custom.GAdate", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate", "Custom.CurrentStatus", "Custom.RiskAssessmentComment" } },
			{ "User Story", new List<string> { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "System.IterationPath", "Custom.RiskAssessmentComment", "Custom.StoryField1", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate" } },
			{ "Task", new List<string> { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "System.IterationPath", "System.AssignedTo", "Custom.TaskField1", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate" } }
		};

		public GetWorkItemTreeCapability(IAzureDevOpsWorkItemClient client, int maxConcurrentFetches = 5)
		{
			_client = client;
			_maxConcurrentFetches = maxConcurrentFetches;
		}

		/// <inheritdoc />
		public string Name => "GetWorkItemTree";

		/// <inheritdoc />
		public string Description => "Retrieves a hierarchical tree of Azure DevOps work items starting from a root ID.";

		/// <inheritdoc />
		public async Task<string> ExecuteAsync(GetWorkItemTreeCapabilityInput input)
		{
			var depth = input.Depth > 0 ? input.Depth : _defaultDepth;
			var rootNode = await BuildWorkItemTreeAsync(input.WorkItemId, depth);
			return JsonSerializer.Serialize(rootNode);
		}

		private async Task<JsonElement> BuildWorkItemTreeAsync(int workItemId, int remainingDepth)
		{
			var rootWorkItem = await FetchWorkItemWithTypedFieldsAsync(workItemId);

			if (remainingDepth <= 0 || !rootWorkItem.TryGetProperty("relations", out var relationsElement))
			{
				return rootWorkItem;
			}

			// Collect all child IDs first
			var childIds = new List<int>();
			foreach (var relationElement in relationsElement.EnumerateArray())
			{
				if (!IsChildRelation(relationElement) || !TryGetChildIdFromRelation(relationElement, out var childId))
				{
					continue;
				}
				childIds.Add(childId);
			}

			// Fetch all children in parallel with throttling
			if (childIds.Count == 0)
			{
				return CreateTreeNode(rootWorkItem, Array.Empty<JsonElement>());
			}

			var semaphore = new SemaphoreSlim(_maxConcurrentFetches);
			var childTasks = childIds.Select(async childId =>
			{
				await semaphore.WaitAsync();
				try
				{
					return await BuildWorkItemTreeAsync(childId, remainingDepth - 1);
				}
				finally
				{
					semaphore.Release();
				}
			}).ToArray();

			var childNodes = await Task.WhenAll(childTasks);

			return CreateTreeNode(rootWorkItem, childNodes);
		}

		private async Task<JsonElement> FetchWorkItemWithTypedFieldsAsync(int workItemId)
		{
			// Initial fetch to discover type and relations
			var workItemJson = await _client.GetWorkItemAsync(workItemId);
			using var discoveryDocument = JsonDocument.Parse(workItemJson);
			var discoveredWorkItem = discoveryDocument.RootElement.Clone();

			var workItemType = TryGetWorkItemType(discoveredWorkItem);
			var fieldSelection = ResolveFieldSelectionForType(workItemType);

			if (fieldSelection == null || !fieldSelection.Any())
			{
				return discoveredWorkItem;
			}

			// Re-fetch including the desired fields for this type
			var typedWorkItemJson = await _client.GetWorkItemAsync(workItemId, fieldSelection);
			using var typedDocument = JsonDocument.Parse(typedWorkItemJson);
			return typedDocument.RootElement.Clone();
		}

		private static string? TryGetWorkItemType(JsonElement workItemElement)
		{
			if (!workItemElement.TryGetProperty("fields", out var fieldsElement))
			{
				return null;
			}

			return fieldsElement.TryGetProperty("System.WorkItemType", out var typeElement)
				? typeElement.GetString()
				: null;
		}

		private static IEnumerable<string>? ResolveFieldSelectionForType(string? workItemType)
		{
			if (workItemType == null)
			{
				return null;
			}

			return FieldsByType.TryGetValue(workItemType, out var fields)
				? fields
				: null;
		}

		private static JsonElement CreateTreeNode(JsonElement workItemElement, IReadOnlyCollection<JsonElement> childNodes)
		{
			using var workItemDocument = JsonDocument.Parse(JsonSerializer.Serialize(workItemElement));
			var workItemObject = workItemDocument.RootElement.Clone();

			using var childrenDocument = JsonDocument.Parse(JsonSerializer.Serialize(childNodes));
			var childrenArray = childrenDocument.RootElement.Clone();

			using var mergedDocument = JsonDocument.Parse(JsonSerializer.Serialize(new
			{
				workItem = workItemObject,
				children = childrenArray
			}));

			return mergedDocument.RootElement.Clone();
		}

		private static bool IsChildRelation(JsonElement relationElement)
		{
			return relationElement.TryGetProperty("rel", out var relationTypeElement) &&
				relationTypeElement.GetString() == "System.LinkTypes.Hierarchy-Forward" &&
				relationElement.TryGetProperty("attributes", out var attributesElement) &&
				attributesElement.TryGetProperty("name", out var nameElement) &&
				nameElement.GetString() == "Child";
		}

		private static bool TryGetChildIdFromRelation(JsonElement relationElement, out int childId)
		{
			childId = 0;
			if (!relationElement.TryGetProperty("url", out var urlElement))
			{
				return false;
			}

			var url = urlElement.GetString();
			if (url == null)
			{
				return false;
			}

			if (!int.TryParse(url.Split('/').Last(), out var parsedChildId))
			{
				return false;
			}

			childId = parsedChildId;
			return true;
		}
	}
}
