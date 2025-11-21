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
		private readonly int _defaultDepth =2;

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
			"Capabilities: [GetWorkItemTree] | Input: { 'workItemId': int, 'depth': int } | Output: JSON tree of work items with children";

		/// <inheritdoc/>
		public virtual async Task<string> ExecuteAsync(string operation, string input)
		{
			return operation switch
			{
				"GetWorkItemTree" => await ExecuteGetWorkItemTreeAsync(input),
				_ => throw new NotSupportedException($"Operation '{operation}' is not supported."),
			};
		}

		private async Task<string> ExecuteGetWorkItemTreeAsync(string input)
		{
			var doc = JsonDocument.Parse(input);
			int rootId = ExtractRootId(doc.RootElement);
			int depth = ExtractDepth(doc.RootElement);

			var tree = await GetWorkItemTreeAsync(rootId, depth);
			return JsonSerializer.Serialize(tree);
		}

		private int ExtractRootId(JsonElement root)
		{
			var rootIdElem = root.GetProperty("rootId");
			if (rootIdElem.ValueKind == JsonValueKind.String)
			{
				if (int.TryParse(rootIdElem.GetString(), out int rootId))
					return rootId;
				throw new ArgumentException("rootId must be convertible to int.");
			}
			else if (rootIdElem.ValueKind == JsonValueKind.Number)
			{
				return rootIdElem.GetInt32();
			}
			else
			{
				throw new ArgumentException("rootId must be a string or number.");
			}
		}

		private int ExtractDepth(JsonElement root)
		{
			if (root.TryGetProperty("depth", out var depthElem))
			{
				if (depthElem.ValueKind == JsonValueKind.String)
				{
					if (int.TryParse(depthElem.GetString(), out int depth))
						return depth;
					return _defaultDepth;
				}
				else if (depthElem.ValueKind == JsonValueKind.Number)
				{
					return depthElem.GetInt32();
				}
			}
			return _defaultDepth;
		}

		private async Task<JsonElement> GetWorkItemTreeAsync(int id, int depth)
		{
			var json = await _client.GetWorkItemAsync(id);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement.Clone();

			if (depth <=0 || !root.TryGetProperty("relations", out var relations))
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

		private bool IsChildRelation(JsonElement rel)
		{
			return rel.TryGetProperty("rel", out var relType) &&
				relType.GetString() == "System.LinkTypes.Hierarchy-Forward" &&
				rel.TryGetProperty("attributes", out var attributes) &&
				attributes.TryGetProperty("name", out var nameProp) &&
				nameProp.GetString() == "Child";
		}

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
