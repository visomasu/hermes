using Integrations.AzureDevOps;
using System.Text.Json;

namespace Hermes.Tools.AzureDevOps
{
	/// <summary>
	/// Agent tool for Azure DevOps, supporting multiple capabilities.
	/// </summary>
	public class AzureDevOpsTool : IAgentTool
	{
		private readonly IAzureDevOpsWorkItemClient _client;
		private readonly int _defaultDepth = 2;

		// Static mapping of work item type to fields
		private static readonly Dictionary<string, List<string>> FieldsByType = new()
		{
			{ "Feature", new List<string> { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "Custom.PrivatePreviewDate", "Custom.PublicPreviewDate", "Custom.GAdate", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate", "Custom.CurrentStatus", "Custom.RiskAssessmentComment" } },
			{ "User Story", new List<string> { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "Custom.RiskAssessmentComment", "Custom.StoryField1", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate" } },
			{ "Task", new List<string> { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "System.AssignedTo", "Custom.TaskField1", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate" } }
		};

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
		public string Description => "Provides Azure DevOps capabilities such as retrieving work item trees, parent hierarchies and more.";

		/// <inheritdoc/>
		public IReadOnlyList<string> Capabilities => new[] { "GetWorkItemTree", "GetWorkItemsByAreaPath", "GetParentHierarchy", "GetFullHierarchy" };

		/// <inheritdoc/>
		public string GetMetadata() =>
			"Capabilities: [GetWorkItemTree, GetWorkItemsByAreaPath, GetParentHierarchy, GetFullHierarchy] | " +
			"Input (GetWorkItemTree): { 'workItemId': int, 'depth': int } | " +
			"Input (GetWorkItemsByAreaPath): { 'areaPath': string, 'workItemTypes': string[]?, 'fields': string[]? } | " +
			"Input (GetParentHierarchy): { 'workItemId': int, 'fields': string[]? } | " +
			"Input (GetFullHierarchy): { 'workItemId': int, 'depth': int?, 'fields': string[]? } | " +
			"Output: JSON";

		/// <inheritdoc/>
		public virtual async Task<string> ExecuteAsync(string operation, string input)
		{
			return operation switch
			{
				"GetWorkItemTree" => await ExecuteGetWorkItemTreeAsync(input),
				"GetWorkItemsByAreaPath" => await ExecuteGetWorkItemsByAreaPathAsync(input),
				"GetParentHierarchy" => await ExecuteGetParentHierarchyAsync(input),
				"GetFullHierarchy" => await ExecuteGetFullHierarchyAsync(input),
				_ => throw new NotSupportedException($"Operation '{operation}' is not supported."),
			};
		}

		#region WorkItemTree

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
			// Fetch root work item with fields based on its type
			var json = await _client.GetWorkItemAsync(id);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement.Clone();

			string? type = null;
			if (root.TryGetProperty("fields", out var fieldsElem) && fieldsElem.TryGetProperty("System.WorkItemType", out var typeElem))
			{
				type = typeElem.GetString();
			}

			IEnumerable<string>? fields = null;
			if (type != null && FieldsByType.TryGetValue(type, out var typeFields))
			{
				fields = typeFields;
			}

			// If fields are specified, re-fetch with those fields
			if (fields != null && fields.Any())
			{
				json = await _client.GetWorkItemAsync(id, fields);
				using var doc2 = JsonDocument.Parse(json);
				root = doc2.RootElement.Clone();
			}

			if (depth <= 0 || !root.TryGetProperty("relations", out var relations))
				return root;

			var children = new List<JsonElement>();
			foreach (var rel in relations.EnumerateArray())
			{
				if (IsChildRelation(rel) && TryGetChildIdFromRelation(rel, out int childId))
				{
					var child = await GetWorkItemTreeAsync(childId, depth - 1);
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
			childId = 0;
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

		#endregion

		#region ParentHierarchy

		private async Task<string> ExecuteGetParentHierarchyAsync(string input)
		{
			using var doc = JsonDocument.Parse(input);
			var root = doc.RootElement;

			ResolveWorkItemParameters(root, out var workItemId, out _, out var fields);

			// Delegate to the Azure DevOps client, which handles mandatory fields and traversal.
			var resultJson = await _client.GetParentHierarchyAsync(workItemId, fields);
			return resultJson;
		}

		#endregion

		#region FullHierarchy

		private async Task<string> ExecuteGetFullHierarchyAsync(string input)
		{
			using var doc = JsonDocument.Parse(input);
			var root = doc.RootElement;

			ResolveWorkItemParameters(root, out var workItemId, out var depth, out var fields);

			// Parents: use the client-level parent hierarchy API.
			var parentJson = await _client.GetParentHierarchyAsync(workItemId, fields);
			using var parentDoc = JsonDocument.Parse(parentJson);
			var parentsElement = parentDoc.RootElement.Clone();

			// Children: reuse the existing work item tree logic starting from the same work item.
			var subtree = await GetWorkItemTreeAsync(workItemId, depth);

			using var mergedDoc = JsonDocument.Parse(JsonSerializer.Serialize(new
			{
				parents = parentsElement,
				children = subtree
			}));

			return JsonSerializer.Serialize(mergedDoc.RootElement);
		}

		/// <summary>
		/// Resolves common parameters for work-item-based operations: workItemId, optional depth, and optional fields.
		/// </summary>
		private void ResolveWorkItemParameters(JsonElement root, out int workItemId, out int depth, out IEnumerable<string>? fields)
		{
			if (!root.TryGetProperty("workItemId", out var idProp))
			{
				throw new ArgumentException("'workItemId' is required.");
			}

			if (idProp.ValueKind == JsonValueKind.String)
			{
				if (!int.TryParse(idProp.GetString(), out workItemId))
				{
					throw new ArgumentException("'workItemId' must be convertible to int.");
				}
			}
			else if (idProp.ValueKind == JsonValueKind.Number)
			{
				workItemId = idProp.GetInt32();
			}
			else
			{
				throw new ArgumentException("'workItemId' must be a string or number.");
			}

			// Depth is optional; if not present, use the default.
			depth = _defaultDepth;
			if (root.TryGetProperty("depth", out var depthProp))
			{
				if (depthProp.ValueKind == JsonValueKind.Number)
				{
					depth = depthProp.GetInt32();
				}
				else if (depthProp.ValueKind == JsonValueKind.String && int.TryParse(depthProp.GetString(), out var d))
				{
					depth = d;
				}
			}

			fields = null;
			if (root.TryGetProperty("fields", out var fieldsProp) && fieldsProp.ValueKind == JsonValueKind.Array)
			{
				var fieldList = new List<string>();
				foreach (var f in fieldsProp.EnumerateArray())
				{
					if (f.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(f.GetString()))
					{
						fieldList.Add(f.GetString()!);
					}
				}
				fields = fieldList.Count > 0 ? fieldList : null;
			}
		}

		#endregion

		#region Area-Path

		private async Task<string> ExecuteGetWorkItemsByAreaPathAsync(string input)
		{
			using var doc = JsonDocument.Parse(input);
			var root = doc.RootElement;

			ResolveAreaPathParameters(root, out var areaPath, out var workItemTypes, out var fields);

			var resultJson = await _client.GetWorkItemsByAreaPathAsync(areaPath, workItemTypes, fields);
			return resultJson;
		}

		private void ResolveAreaPathParameters(
			JsonElement root,
			out string areaPath,
			out IEnumerable<string>? workItemTypes,
			out IEnumerable<string>? fields)
		{
			if (!root.TryGetProperty("areaPath", out var areaPathProp) || areaPathProp.ValueKind != JsonValueKind.String)
			{
				throw new ArgumentException("'areaPath' is required and must be a string.");
			}

			areaPath = areaPathProp.GetString() ?? string.Empty;

			workItemTypes = null;
			if (root.TryGetProperty("workItemTypes", out var typesProp) && typesProp.ValueKind == JsonValueKind.Array)
			{
				var list = new List<string>();
				foreach (var t in typesProp.EnumerateArray())
				{
					if (t.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(t.GetString()))
					{
						list.Add(t.GetString()!);
					}
				}
				workItemTypes = list.Count > 0 ? list : null;
			}

			fields = null;
			if (root.TryGetProperty("fields", out var fieldsProp) && fieldsProp.ValueKind == JsonValueKind.Array)
			{
				var fieldList = new List<string>();
				foreach (var f in fieldsProp.EnumerateArray())
				{
					if (f.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(f.GetString()))
					{
						fieldList.Add(f.GetString()!);
					}
				}
				fields = fieldList.Count > 0 ? fieldList : null;
			}
		}

		#endregion
	}
}
