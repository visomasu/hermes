using Exceptions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System.Text.Json;

namespace Integrations.AzureDevOps
{
	/// <summary>
	/// Implementation of IAzureDevOpsWorkItemClient for Azure DevOps work item queries using the official .NET SDK.
	/// </summary>
	public class AzureDevOpsWorkItemClient : IAzureDevOpsWorkItemClient
	{
		private WorkItemTrackingHttpClient? _workItemClient;
		private readonly string _project;
		private readonly VssConnection _connection;

		// Mandatory fields: Id, Title, Description, Relations
		private static readonly IEnumerable<string> MandatoryFields = new[]
		{
			"System.Id",
			"System.Title",
			"System.Description",
			"System.State"
		};

		// Lowercase naming policy for JSON serialization
		private static readonly JsonSerializerOptions LowercaseJsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = new LowercaseNamingPolicy(),
		};

		private class LowercaseNamingPolicy : JsonNamingPolicy
		{
			public override string ConvertName(string name) => name.ToLowerInvariant();
		}

		/// <summary>
		/// Initializes a new instance of the AzureDevOpsWorkItemClient class using the Azure DevOps .NET SDK.
		/// </summary>
		/// <param name="organization">Azure DevOps organization name.</param>
		/// <param name="project">Azure DevOps project name.</param>
		/// <param name="personalAccessToken">Personal Access Token for authentication.</param>
		public AzureDevOpsWorkItemClient(string organization, string project, string personalAccessToken)
		{
			_project = project;
			_connection = new VssConnection(
				new Uri($"https://dev.azure.com/{organization}"),
				new VssBasicCredential(string.Empty, personalAccessToken)
			);
		}

		private WorkItemTrackingHttpClient GetClient()
		{
			if (_workItemClient == null)
			{
				try
				{
					_workItemClient = _connection.GetClient<WorkItemTrackingHttpClient>();
				}
				catch (VssServiceException ex)
				{
					throw new IntegrationException($"Azure DevOps service error during client initialization: {ex.Message}", IntegrationException.ErrorCode.ServiceError, ex);
				}
				catch (VssAuthenticationException ex)
				{
					throw new IntegrationException($"Azure DevOps authentication error during client initialization: {ex.Message}", IntegrationException.ErrorCode.AuthenticationError, ex);
				}
				catch (Exception ex)
				{
					throw new IntegrationException($"Unexpected error during client initialization: {ex.Message}", IntegrationException.ErrorCode.UnexpectedError, ex);
				}
			}
			return _workItemClient;
		}

		/// <inheritdoc/>
		public async Task<string> GetWorkItemAsync(int id)
		{
			return await GetWorkItemAsync(id, null);
		}

		/// <inheritdoc/>
		public async Task<string> GetWorkItemAsync(int id, IEnumerable<string>? fields)
		{
			try
			{
				var allFields = (fields ?? Enumerable.Empty<string>())
					.Concat(MandatoryFields)
					.Distinct()
					.ToList();

				var workItem = await GetClient().GetWorkItemAsync(id, expand: WorkItemExpand.All);

				// Filter the Fields dictionary to only include the requested fields
				var filteredFields = workItem.Fields
					.Where(kvp => allFields.Contains(kvp.Key))
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

				// Create a new object with filtered fields and relations
				var result = new
				{
					workItem.Id,
					workItem.Rev,
					workItem.Relations,
					Fields = filteredFields
				};

				return JsonSerializer.Serialize(result, LowercaseJsonOptions);
			}
			catch (VssServiceException ex)
			{
				throw new IntegrationException($"Azure DevOps service error while retrieving work item {id}: {ex.Message}", IntegrationException.ErrorCode.ServiceError, ex);
			}
			catch (VssAuthenticationException ex)
			{
				throw new IntegrationException($"Azure DevOps authentication error: {ex.Message}", IntegrationException.ErrorCode.AuthenticationError, ex);
			}
			catch (Exception ex)
			{
				throw new IntegrationException($"Unexpected error while retrieving work item {id}: {ex.Message}", IntegrationException.ErrorCode.UnexpectedError, ex);
			}
		}

		/// <inheritdoc/>
		public async Task<string> GetWorkItemsAsync(IEnumerable<int> ids, IEnumerable<string>? fields)
		{
			if (ids == null || !ids.Any())
				throw new IntegrationException("ids must not be null or empty", IntegrationException.ErrorCode.UnexpectedError);

			try
			{
				var allFields = (fields ?? Enumerable.Empty<string>())
					.Concat(MandatoryFields)
					.Distinct()
					.ToList();

				var workItems = await GetClient().GetWorkItemsAsync(ids.ToList(), expand: WorkItemExpand.All);

				var filteredResults = workItems.Select(workItem => new
				{
					workItem.Id,
					workItem.Rev,
					workItem.Relations,
					Fields = workItem.Fields
						.Where(kvp => allFields.Contains(kvp.Key))
						.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
				});

				return JsonSerializer.Serialize(filteredResults, LowercaseJsonOptions);
			}
			catch (VssServiceException ex)
			{
				throw new IntegrationException($"Azure DevOps service error while retrieving work items: {ex.Message}", IntegrationException.ErrorCode.ServiceError, ex);
			}
			catch (VssAuthenticationException ex)
			{
				throw new IntegrationException($"Azure DevOps authentication error: {ex.Message}", IntegrationException.ErrorCode.AuthenticationError, ex);
			}
			catch (Exception ex)
			{
				throw new IntegrationException($"Unexpected error while retrieving work items: {ex.Message}", IntegrationException.ErrorCode.UnexpectedError, ex);
			}
		}
	}
}
