using System.Text.Json;
using Integrations.AzureDevOps;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;

namespace Hermes.Tools.AzureDevOps.Capabilities
{
	/// <summary>
	/// Capability for retrieving an Azure DevOps work item parent hierarchy.
	/// </summary>
	public sealed class GetParentHierarchyCapability : IAgentToolCapability<GetParentHierarchyCapabilityInput>
	{
		private readonly IAzureDevOpsWorkItemClient _client;

		public GetParentHierarchyCapability(IAzureDevOpsWorkItemClient client)
		{
			_client = client;
		}

		/// <inheritdoc />
		public string Name => "GetParentHierarchy";

		/// <inheritdoc />
		public string Description => "Retrieves the parent hierarchy of an Azure DevOps work item, returning minimal data for hierarchy validation.";

		/// <inheritdoc />
		public async Task<string> ExecuteAsync(GetParentHierarchyCapabilityInput input)
		{
			// Delegate to the Azure DevOps client, which handles mandatory fields and traversal.
			var resultJson = await _client.GetParentHierarchyAsync(input.WorkItemId, input.Fields);

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
	}
}
