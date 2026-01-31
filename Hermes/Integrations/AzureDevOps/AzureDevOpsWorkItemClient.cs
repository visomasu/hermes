using Azure.Core;
using Azure.Identity;
using Exceptions;
using Microsoft.TeamFoundation.Core.WebApi.Types;
using Microsoft.TeamFoundation.Work.WebApi;
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

		// Azure DevOps API limit for GetWorkItems batch request
		private const int MaxWorkItemsBatchSize = 200;

		// Mandatory fields: Id, Title, Description, Relations
		private static readonly IEnumerable<string> MandatoryFields = new[]
		{
			"System.Id",
			"System.Title",
			"System.Description",
			"System.State",
            "System.WorkItemType"
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
		/// Initializes a new instance of the AzureDevOpsWorkItemClient class using Azure CLI authentication.
		/// Uses DefaultAzureCredential which supports:
		/// - Azure CLI (az login) for local development
		/// - Managed Identity for production (Azure App Service, Container Apps, etc.)
		/// - Environment variables
		/// </summary>
		/// <param name="organization">Azure DevOps organization name.</param>
		/// <param name="project">Azure DevOps project name.</param>
		public AzureDevOpsWorkItemClient(string organization, string project)
		{
			_project = project;

			// Use DefaultAzureCredential for authentication (supports az login, Managed Identity, etc.)
			var tokenCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
			{
				// Exclude interactive browser credential to avoid prompts in production
				ExcludeInteractiveBrowserCredential = true,
				// Exclude Visual Studio Code credential for cleaner auth flow
				ExcludeVisualStudioCodeCredential = true
			});

			// Get access token for Azure DevOps
			var tokenRequestContext = new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" });
			var accessToken = tokenCredential.GetToken(tokenRequestContext, default);

			// Create VssConnection with OAuth access token as PAT
			// VssBasicCredential accepts Bearer tokens as the password parameter
			var vssCredential = new VssBasicCredential(string.Empty, accessToken.Token);

			_connection = new VssConnection(
				new Uri($"https://dev.azure.com/{organization}"),
				vssCredential
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

		/// <inheritdoc/>
		public async Task<string> GetWorkItemsByAreaPathAsync(string areaPath, IEnumerable<string>? workItemTypes = null, IEnumerable<string>? fields = null, int pageNumber = 1, int pageSize = 5)
		{
			if (string.IsNullOrWhiteSpace(areaPath))
			{
				throw new IntegrationException("areaPath must not be null or empty", IntegrationException.ErrorCode.UnexpectedError);
			}

			// Normalize and validate work item types if provided
			var workItemTypesList = workItemTypes?
				.Where(t => !string.IsNullOrWhiteSpace(t))
				.Select(t => t.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			// Normalize paging arguments (1-based pageNumber, positive pageSize)
			if (pageNumber < 1)
			{
				pageNumber = 1;
			}

			if (pageSize <= 0)
			{
				pageSize = 5;
			}
 
			try
			{
				var allFields = (fields ?? Enumerable.Empty<string>())
					.Concat(MandatoryFields)
					.Distinct()
					.ToList();
 
				var wiql = BuildAreaPathWiql(areaPath, workItemTypesList);
 
				var client = GetClient();
				var queryResult = await client.QueryByWiqlAsync(wiql, _project);
 
				if (queryResult.WorkItems == null || !queryResult.WorkItems.Any())
				{
					return JsonSerializer.Serialize(Array.Empty<object>(), LowercaseJsonOptions);
				}

				// Apply paging over the list of work item references
				var skip = (pageNumber - 1) * pageSize;
				var page = queryResult.WorkItems
					.Skip(skip)
					.Take(pageSize)
					.ToList();

				if (!page.Any())
				{
					return JsonSerializer.Serialize(Array.Empty<object>(), LowercaseJsonOptions);
				}

				var ids = page.Select(w => w.Id).ToList();
				var workItems = await client.GetWorkItemsAsync(ids, expand: WorkItemExpand.All);
 
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
				throw new IntegrationException($"Azure DevOps service error while querying work items by area path '{areaPath}': {ex.Message}", IntegrationException.ErrorCode.ServiceError, ex);
			}
			catch (VssAuthenticationException ex)
			{
				throw new IntegrationException($"Azure DevOps authentication error: {ex.Message}", IntegrationException.ErrorCode.AuthenticationError, ex);
			}
			catch (Exception ex)
			{
				throw new IntegrationException($"Unexpected error while querying work items by area path '{areaPath}': {ex.Message}", IntegrationException.ErrorCode.UnexpectedError, ex);
			}
		}

		/// <inheritdoc/>
		public async Task<string> GetParentHierarchyAsync(int id, IEnumerable<string>? fields = null)
		{
			try
			{
				// Ensure we always include the mandatory fields when traversing the hierarchy.
				var allFields = (fields ?? Enumerable.Empty<string>())
					.Concat(MandatoryFields)
					.Distinct()
					.ToList();

				var client = GetClient();
				var hierarchy = new List<object>();
				var visited = new HashSet<int>();
				int? currentId = id;

				// Walk up the parent chain until there is no parent or a cycle is detected.
				while (currentId.HasValue)
				{
					if (!visited.Add(currentId.Value))
					{
						// Cycle detected; stop traversal.
						break;
					}

					var workItem = await client.GetWorkItemAsync(
						currentId.Value,
						fields: null,
						asOf: null,
						expand: WorkItemExpand.Relations,
						userState: null,
						cancellationToken: CancellationToken.None);

					var filteredFields = workItem.Fields
						.Where(kvp => allFields.Contains(kvp.Key))
						.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

					hierarchy.Add(new
					{
						workItem.Id,
						workItem.Rev,
						workItem.Relations,
						Fields = filteredFields
					});

					// Find the parent relation (Hierarchy-Reverse is parent link in Azure DevOps).
					var parentRelation = workItem.Relations?
						.FirstOrDefault(r => string.Equals(r.Rel, "System.LinkTypes.Hierarchy-Reverse", StringComparison.OrdinalIgnoreCase));

					// Ensure the URL points to a work item; accept standard ADO URLs like '.../workItems/{id}'.
					if (parentRelation == null || !parentRelation.Url.Contains("/workItems/", StringComparison.OrdinalIgnoreCase))
					{
						currentId = null;
						continue;
					}

					// Extract the parent work item id from the URL.
					var segments = parentRelation.Url.Split('/', StringSplitOptions.RemoveEmptyEntries);
					if (int.TryParse(segments.Last(), out var parentId))
					{
						currentId = parentId;
					}
					else
					{
						currentId = null;
					}
				}

				// The traversal built the hierarchy from the starting item up to the root.
				// Reverse to get topmost parent first.
				hierarchy.Reverse();

				return JsonSerializer.Serialize(hierarchy, LowercaseJsonOptions);
			}
			catch (VssServiceException ex)
			{
				throw new IntegrationException($"Azure DevOps service error while retrieving parent hierarchy for work item {id}: {ex.Message}", IntegrationException.ErrorCode.ServiceError, ex);
			}
			catch (VssAuthenticationException ex)
			{
				throw new IntegrationException($"Azure DevOps authentication error: {ex.Message}", IntegrationException.ErrorCode.AuthenticationError, ex);
			}
			catch (Exception ex)
			{
				throw new IntegrationException($"Unexpected error while retrieving parent hierarchy for work item {id}: {ex.Message}", IntegrationException.ErrorCode.UnexpectedError, ex);
			}
		}

		// Builds a WIQL query for selecting work items under an area path with optional type filters.
		private static Wiql BuildAreaPathWiql(string areaPath, IList<string>? workItemTypes)
		{
			var wiqlClauses = new List<string>
			{
				"[System.TeamProject] = @project",
				$"[System.AreaPath] UNDER '{areaPath.Replace("'", "''")}'"
			};

			if (workItemTypes != null && workItemTypes.Count > 0)
			{
				var typeConditions = workItemTypes
					.Select(t => $"[System.WorkItemType] = '{t.Replace("'", "''")}'");
				wiqlClauses.Add($"({string.Join(" OR ", typeConditions)})");

				// Only include work items that are in New or Active state when types are specified.
				wiqlClauses.Add("[System.State] IN ('New','Active')");
			}

			return new Wiql
			{
				Query =
					"SELECT [System.Id] " +
					"FROM WorkItems " +
					"WHERE " + string.Join(" AND ", wiqlClauses) +
					" ORDER BY [System.ChangedDate] DESC"
			};
		}

		/// <inheritdoc/>
		public async Task<string> GetWorkItemsByAssignedUserAsync(
			string userEmail,
			IEnumerable<string>? states = null,
			IEnumerable<string>? fields = null,
			string? iterationPath = null,
			IEnumerable<string>? areaPaths = null,
			IEnumerable<string>? workItemTypes = null,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(userEmail))
			{
				throw new IntegrationException("userEmail must not be null or empty", IntegrationException.ErrorCode.UnexpectedError);
			}

			try
			{
				var allFields = (fields ?? Enumerable.Empty<string>())
					.Concat(MandatoryFields)
					.Distinct()
					.ToList();

				var wiql = BuildAssignedUserWiql(userEmail, states, iterationPath, areaPaths, workItemTypes);

				var client = GetClient();
				var queryResult = await client.QueryByWiqlAsync(wiql, _project);

				if (queryResult.WorkItems == null || !queryResult.WorkItems.Any())
				{
					return JsonSerializer.Serialize(new { count = 0, value = Array.Empty<object>() }, LowercaseJsonOptions);
				}

				var ids = queryResult.WorkItems.Select(w => w.Id).ToList();

				// Batch work item IDs into chunks of MaxWorkItemsBatchSize to avoid Azure DevOps API limit
				var allWorkItems = new List<WorkItem>();
				for (int i = 0; i < ids.Count; i += MaxWorkItemsBatchSize)
				{
					var batchIds = ids.Skip(i).Take(MaxWorkItemsBatchSize).ToList();
					var batchWorkItems = await client.GetWorkItemsAsync(batchIds, expand: WorkItemExpand.All);
					allWorkItems.AddRange(batchWorkItems);
				}

				var filteredResults = allWorkItems.Select(workItem => new
				{
					id = workItem.Id,
					rev = workItem.Rev,
					fields = allFields.ToDictionary(
						field => field,
						field => workItem.Fields.TryGetValue(field, out var value) ? value : null
					)
				});

				return JsonSerializer.Serialize(new { count = filteredResults.Count(), value = filteredResults }, LowercaseJsonOptions);
			}
			catch (Exception ex)
			{
				throw new IntegrationException(
					$"Error querying work items for user '{userEmail}': {ex.Message}",
					IntegrationException.ErrorCode.UnexpectedError,
					ex);
			}
		}

		private static Wiql BuildAssignedUserWiql(
			string userEmail,
			IEnumerable<string>? states,
			string? iterationPath,
			IEnumerable<string>? areaPaths,
			IEnumerable<string>? workItemTypes)
		{
			var wiqlClauses = new List<string>
			{
				"[System.TeamProject] = @project",
				$"[System.AssignedTo] = '{userEmail.Replace("'", "''")}'"
			};

			// Filter by states
			var statesList = states?
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Select(s => s.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (statesList != null && statesList.Count > 0)
			{
				var stateConditions = statesList
					.Select(s => $"'{s.Replace("'", "''")}'");
				wiqlClauses.Add($"[System.State] IN ({string.Join(",", stateConditions)})");
			}

			// Filter by iteration path
			if (!string.IsNullOrWhiteSpace(iterationPath))
			{
				wiqlClauses.Add($"[System.IterationPath] UNDER '{iterationPath.Replace("'", "''")}'");
			}

			// Filter by area paths
			var areaPathsList = areaPaths?
				.Where(a => !string.IsNullOrWhiteSpace(a))
				.Select(a => a.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (areaPathsList != null && areaPathsList.Count > 0)
			{
				var areaPathConditions = areaPathsList
					.Select(a => $"[System.AreaPath] UNDER '{a.Replace("'", "''")}'");
				wiqlClauses.Add($"({string.Join(" OR ", areaPathConditions)})");
			}

			// Filter by work item types
			var typesList = workItemTypes?
				.Where(t => !string.IsNullOrWhiteSpace(t))
				.Select(t => t.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (typesList != null && typesList.Count > 0)
			{
				var typeConditions = typesList
					.Select(t => $"[System.WorkItemType] = '{t.Replace("'", "''")}'");
				wiqlClauses.Add($"({string.Join(" OR ", typeConditions)})");
			}

			return new Wiql
			{
				Query =
					"SELECT [System.Id] " +
					"FROM WorkItems " +
					"WHERE " + string.Join(" AND ", wiqlClauses) +
					" ORDER BY [System.ChangedDate] DESC"
			};
		}

		/// <inheritdoc/>
		public async Task<string?> GetCurrentIterationPathAsync(
			string teamName,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(teamName))
			{
				throw new IntegrationException("teamName must not be null or empty", IntegrationException.ErrorCode.UnexpectedError);
			}

			try
			{
				// Get Work HTTP client for querying team iterations
				var workClient = _connection.GetClient<WorkHttpClient>();

				// Get team context
				var teamContext = new TeamContext(_project, teamName);

				// Query team iterations
				var iterations = await workClient.GetTeamIterationsAsync(teamContext, cancellationToken: cancellationToken);

				if (iterations == null || !iterations.Any())
				{
					return null;
				}

				// Find the iteration where current date falls within start and finish dates
				var now = DateTime.UtcNow;
				var currentIteration = iterations.FirstOrDefault(iteration =>
					iteration.Attributes != null &&
					iteration.Attributes.StartDate.HasValue &&
					iteration.Attributes.FinishDate.HasValue &&
					iteration.Attributes.StartDate.Value <= now &&
					iteration.Attributes.FinishDate.Value >= now);

				// Return the iteration path if found
				return currentIteration?.Path;
			}
			catch (Exception ex)
			{
				throw new IntegrationException(
					$"Error querying current iteration for team '{teamName}': {ex.Message}",
					IntegrationException.ErrorCode.UnexpectedError,
					ex);
			}
		}
	}
}
