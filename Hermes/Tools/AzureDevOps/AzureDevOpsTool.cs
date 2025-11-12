using System.Text.Json;
using Integrations.AzureDevOps;

namespace Hermes.Tools.AzureDevOps
{
	/// <summary>
	/// Agent tool for Azure DevOps, supporting multiple capabilities.
	/// </summary>
	public class AzureDevOpsTool : IAgentTool
	{
		private readonly IAzureDevOpsWorkItemClient _client;

		/// <summary>
		/// Initializes a new instance of <see cref="AzureDevOpsTool"/>.
		/// </summary>
		/// <param name="client">The Azure DevOps work item client.</param>
		public AzureDevOpsTool(IAzureDevOpsWorkItemClient client)
		{
			_client = client;
		}

		/// <inheritdoc/>
		public string Name => "AzureDevOpsTool";

		/// <inheritdoc/>
		public string Description => "Provides Azure DevOps capabilities such as retrieving work item trees and more.";

		/// <inheritdoc/>
		public IReadOnlyList<string> Capabilities => new[] { "GetWorkItemTree" };

		/// <inheritdoc/>
		public string GetMetadata() =>
			"Capabilities: [GetWorkItemTree] | Input: { 'rootId': int, 'depth': int } | Output: JSON tree of work items with children";

		/// <inheritdoc/>
		public virtual async Task<string> ExecuteAsync(string operation, string input)
		{
			return operation switch
			{
				"GetWorkItemTree" => await ExecuteGetWorkItemTreeAsync(input),
				_ => throw new NotSupportedException($"Operation '{operation}' is not supported."),
			};
		}

		/// <summary>
		/// Executes the GetWorkItemTree capability, retrieving a tree of work items from the specified root to the given depth.
		/// </summary>
		/// <param name="input">Input JSON containing 'rootId' and 'depth'.</param>
		/// <returns>JSON string representing the work item tree.</returns>
		private async Task<string> ExecuteGetWorkItemTreeAsync(string input)
		{
			var doc = JsonDocument.Parse(input);
			int rootId = doc.RootElement.GetProperty("rootId").GetInt32();
			int depth = doc.RootElement.GetProperty("depth").GetInt32();

			var tree = await GetWorkItemTreeAsync(rootId, depth);
			return JsonSerializer.Serialize(tree);
		}

		/// <summary>
		/// Recursively retrieves a work item and its children up to the specified depth.
		/// </summary>
		/// <param name="id">The root work item ID.</param>
		/// <param name="depth">The depth to traverse.</param>
		/// <returns>A <see cref="JsonElement"/> representing the work item tree.</returns>
		private async Task<JsonElement> GetWorkItemTreeAsync(int id, int depth)
		{
			var json = await _client.GetWorkItemAsync(id);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement.Clone();

			if (depth <= 0 || !root.TryGetProperty("relations", out var relations))
				return root;

			var children = new List<JsonElement>();
			foreach (var rel in relations.EnumerateArray())
			{
				if (IsChildRelation(rel) && TryGetChildIdFromRelation(rel, out int childId))
				{
					var child = await GetWorkItemTreeAsync(childId, depth -1);
					children.Add(child);
				}
			}

			using var rootDoc = JsonDocument.Parse(JsonSerializer.Serialize(root));
			var rootObj = rootDoc.RootElement.Clone();
			using var childrenDoc = JsonDocument.Parse(JsonSerializer.Serialize(children));
			var childrenArr = childrenDoc.RootElement.Clone();

			using var mergedDoc = JsonDocument.Parse(JsonSerializer.Serialize(new
			{
				workItem = rootObj,
				children = childrenArr
			}));

			return mergedDoc.RootElement.Clone();
		}

		/// <summary>
		/// Determines if the relation element represents a child work item.
		/// </summary>
		/// <param name="rel">The relation element.</param>
		/// <returns>True if the relation is a child relation; otherwise, false.</returns>
		private bool IsChildRelation(JsonElement rel)
		{
			return rel.TryGetProperty("rel", out var relType) &&
				relType.GetString() == "System.LinkTypes.Hierarchy-Forward" &&
				rel.TryGetProperty("attributes", out var attributes) &&
				attributes.TryGetProperty("name", out var nameProp) &&
				nameProp.GetString() == "Child";
		}

		/// <summary>
		/// Attempts to extract the child work item ID from a relation element.
		/// </summary>
		/// <param name="rel">The relation element.</param>
		/// <param name="childId">The extracted child ID, if successful.</param>
		/// <returns>True if the child ID was successfully extracted; otherwise, false.</returns>
		private bool TryGetChildIdFromRelation(JsonElement rel, out int childId)
		{
			childId =0;
			if (rel.TryGetProperty("url", out var urlProp))
			{
				var url = urlProp.GetString();
				if (url != null && int.TryParse(url.Split('/').Last(), out int id))
				{
					childId = id;
					return true;
				}
			}
			return false;
		}
	}
}
