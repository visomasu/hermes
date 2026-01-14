using System.Text.Json;
using Integrations.AzureDevOps;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;

namespace Hermes.Tools.AzureDevOps.Capabilities
{
	/// <summary>
	/// Capability for retrieving Azure DevOps work items by area path.
	/// </summary>
	public sealed class GetWorkItemsByAreaPathCapability : IAgentToolCapability<GetWorkItemsByAreaPathCapabilityInput>
	{
		private readonly IAzureDevOpsWorkItemClient _client;

		public GetWorkItemsByAreaPathCapability(IAzureDevOpsWorkItemClient client)
		{
			_client = client;
		}

		/// <inheritdoc />
		public string Name => "GetWorkItemsByAreaPath";

		/// <inheritdoc />
		public string Description => "Retrieves Azure DevOps work items by area path with optional type, field, and paging filters.";

		/// <inheritdoc />
		public async Task<string> ExecuteAsync(GetWorkItemsByAreaPathCapabilityInput input)
		{
			if (string.IsNullOrWhiteSpace(input.AreaPath))
			{
				throw new ArgumentException("'areaPath' is required and must be a string.");
			}

			var pageNumber = input.PageNumber.GetValueOrDefault(1);
			var pageSize = input.PageSize.GetValueOrDefault(5);

			var resultJson = await _client.GetWorkItemsByAreaPathAsync(
				input.AreaPath,
				input.WorkItemTypes,
				input.Fields,
				pageNumber,
				pageSize);

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
								: null
					})
					.ToList();

				return JsonSerializer.Serialize(minimalItems);
			}

			return resultJson;
		}
	}
}
