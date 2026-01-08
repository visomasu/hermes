using Integrations.AzureDevOps;
using System.Text.Json;
using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using System.Reflection;

namespace Hermes.Tools.AzureDevOps
{
	/// <summary>
	/// Agent tool for Azure DevOps, supporting multiple capabilities.
	/// </summary>
	public class AzureDevOpsTool : IAgentTool
	{
		private readonly IAzureDevOpsWorkItemClient _client;

		private readonly IAgentToolCapability<GetWorkItemTreeCapabilityInput> _getWorkItemTreeCapability;
		private readonly IAgentToolCapability<GetWorkItemsByAreaPathCapabilityInput> _getWorkItemsByAreaPathCapability;
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
		/// <param name="getWorkItemTreeCapability">Capability implementation for GetWorkItemTree.</param>
		/// <param name="getWorkItemsByAreaPathCapability">Capability implementation for GetWorkItemsByAreaPath.</param>
		public AzureDevOpsTool(
			IAzureDevOpsWorkItemClient client,
			IAgentToolCapability<GetWorkItemTreeCapabilityInput> getWorkItemTreeCapability,
			IAgentToolCapability<GetWorkItemsByAreaPathCapabilityInput> getWorkItemsByAreaPathCapability)
		{
			_client = client;

			_getWorkItemTreeCapability = getWorkItemTreeCapability;
			_getWorkItemsByAreaPathCapability = getWorkItemsByAreaPathCapability;
		}

		/// <inheritdoc/>
		public string Name => "AzureDevOpsTool";

		/// <inheritdoc/>
		public string Description => "Provides Azure DevOps capabilities such as retrieving work item trees, parent hierarchies and more.";

		/// <inheritdoc/>
		public IReadOnlyList<string> Capabilities => new[] { "GetWorkItemTree", "GetWorkItemsByAreaPath", "GetParentHierarchy", "GetFullHierarchy" };

		/// <inheritdoc/>
		public string GetMetadata()
		{
			var getWorkItemTreeInput = BuildInputSchemaDescription(typeof(GetWorkItemTreeCapabilityInput));
			var getWorkItemsByAreaPathInput = BuildInputSchemaDescription(typeof(GetWorkItemsByAreaPathCapabilityInput));

			return
				"Capabilities: [GetWorkItemTree, GetWorkItemsByAreaPath, GetParentHierarchy, GetFullHierarchy] | " +
				$"Input (GetWorkItemTree): {getWorkItemTreeInput} | " +
				$"Input (GetWorkItemsByAreaPath): {getWorkItemsByAreaPathInput} | " +
				"Input (GetParentHierarchy): { 'workItemId': int, 'fields': string[]? } | " +
				"Input (GetFullHierarchy): { 'workItemId': int, 'depth': int?, 'fields': string[]? } | " +
				"Output: JSON";
		}

		private static string BuildInputSchemaDescription(Type inputType)
		{
			// Render a JSON-like shape from public properties on the input type, using camelCase names.
			var properties = inputType
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.CanRead)
				.Select(p => $"'{ToCamelCase(p.Name)}': {MapTypeToSchemaName(p.PropertyType)}");

			return "{" + string.Join(", ", properties) + "}";
		}

		private static string ToCamelCase(string name)
		{
			if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
			{
				return name;
			}

			if (name.Length == 1)
			{
				return name.ToLowerInvariant();
			}

			return char.ToLowerInvariant(name[0]) + name[1..];
		}

		private static string MapTypeToSchemaName(Type type)
		{
			if (type == typeof(int) || type == typeof(int?)) return "int";
			if (type == typeof(string)) return "string";
			if (type.IsArray)
			{
				var elementType = type.GetElementType() ?? typeof(object);
				return MapTypeToSchemaName(elementType) + "[]";
			}
			return "object";
		}

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
			// Use the capability for the actual implementation; keep JSON contract the same.
			var model = JsonSerializer.Deserialize<GetWorkItemTreeCapabilityInput>(input)
				?? throw new ArgumentException("Invalid input for GetWorkItemTree.");

			return await _getWorkItemTreeCapability.ExecuteAsync(model);
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

			// For hierarchy validation, only return minimal data per node: id, title, type, area path, level.
			using var resultDoc = JsonDocument.Parse(resultJson);
			if (resultDoc.RootElement.ValueKind == JsonValueKind.Array)
			{
				var minimalItems = resultDoc.RootElement
					.EnumerateArray()
					.Select(item => new
					{
						Id = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : (int?)null,
						Title = item.TryGetProperty("fields", out var fieldsElement) &&
							fieldsElement.TryGetProperty("System.Title", out var titleProp)
								? titleProp.GetString()
								: null,
						WorkItemType = item.TryGetProperty("fields", out fieldsElement) &&
							fieldsElement.TryGetProperty("System.WorkItemType", out var typeProp)
								? typeProp.GetString()
								: null,
						AreaPath = item.TryGetProperty("fields", out fieldsElement) &&
							fieldsElement.TryGetProperty("System.AreaPath", out var areaProp)
								? areaProp.GetString()
								: null,
						Level = item.TryGetProperty("level", out var levelProp) && levelProp.ValueKind == JsonValueKind.Number
							? levelProp.GetInt32()
							: (int?)null
					})
					.ToList();

				return JsonSerializer.Serialize(minimalItems);
			}

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
			var subtreeJson = await ExecuteGetWorkItemTreeAsync(JsonSerializer.Serialize(new { workItemId = workItemId, depth }));
			using var subtreeDoc = JsonDocument.Parse(subtreeJson);
			var childrenElement = subtreeDoc.RootElement.Clone();

			using var mergedDoc = JsonDocument.Parse(JsonSerializer.Serialize(new
			{
				parents = parentsElement,
				children = childrenElement
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
			var model = JsonSerializer.Deserialize<GetWorkItemsByAreaPathCapabilityInput>(input)
				?? throw new ArgumentException("Invalid input for GetWorkItemsByAreaPath.");

			return await _getWorkItemsByAreaPathCapability.ExecuteAsync(model);
		}

		private static object? JsonElementToNetObject(JsonElement element)
		{
			return element.ValueKind switch
			{
				JsonValueKind.String => element.GetString(),
				JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
				JsonValueKind.True => true,
				JsonValueKind.False => false,
				JsonValueKind.Null => null,
				JsonValueKind.Undefined => null,
				_ => JsonSerializer.Deserialize<object>(element.GetRawText())
			};
		}

		#endregion
	}
}
