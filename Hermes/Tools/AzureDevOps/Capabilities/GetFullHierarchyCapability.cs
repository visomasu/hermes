using System.Text.Json;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;

namespace Hermes.Tools.AzureDevOps.Capabilities
{
	/// <summary>
	/// Capability for retrieving full hierarchy (parents + children) of an Azure DevOps work item.
	/// </summary>
	public sealed class GetFullHierarchyCapability : IAgentToolCapability<GetFullHierarchyCapabilityInput>
	{
		private readonly GetParentHierarchyCapability _parentHierarchyCapability;
		private readonly GetWorkItemTreeCapability _workItemTreeCapability;
		private readonly int _defaultDepth = 2;

		public GetFullHierarchyCapability(
			GetParentHierarchyCapability parentHierarchyCapability,
			GetWorkItemTreeCapability workItemTreeCapability)
		{
			_parentHierarchyCapability = parentHierarchyCapability;
			_workItemTreeCapability = workItemTreeCapability;
		}

		/// <inheritdoc />
		public string Name => "GetFullHierarchy";

		/// <inheritdoc />
		public string Description => "Retrieves the full hierarchy (parents and children) of an Azure DevOps work item.";

		/// <inheritdoc />
		public async Task<string> ExecuteAsync(GetFullHierarchyCapabilityInput input)
		{
			var depth = input.Depth ?? _defaultDepth;

			// Start both parent and children tasks in parallel
			var parentInput = new GetParentHierarchyCapabilityInput
			{
				WorkItemId = input.WorkItemId,
				Fields = input.Fields
			};
			var parentTask = _parentHierarchyCapability.ExecuteAsync(parentInput);

			var treeInput = new GetWorkItemTreeCapabilityInput
			{
				WorkItemId = input.WorkItemId,
				Depth = depth
			};
			var childrenTask = _workItemTreeCapability.ExecuteAsync(treeInput);

			// Wait for both to complete
			await Task.WhenAll(parentTask, childrenTask);

			// Parse results
			var parentJson = await parentTask;
			using var parentDoc = JsonDocument.Parse(parentJson);
			var parentsElement = parentDoc.RootElement.Clone();

			var childrenJson = await childrenTask;
			using var childrenDoc = JsonDocument.Parse(childrenJson);
			var childrenElement = childrenDoc.RootElement.Clone();

			// Merge into single response
			using var mergedDoc = JsonDocument.Parse(JsonSerializer.Serialize(new
			{
				parents = parentsElement,
				children = childrenElement
			}));

			return JsonSerializer.Serialize(mergedDoc.RootElement);
		}
	}
}
