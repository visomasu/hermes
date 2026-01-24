using System.Text.Json;
using Integrations.AzureDevOps;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;

namespace Hermes.Tools.AzureDevOps.Capabilities
{
	/// <summary>
	/// Capability for discovering user activity across Azure DevOps and integrated services.
	/// </summary>
	public sealed class DiscoverUserActivityCapability : IAgentToolCapability<DiscoverUserActivityCapabilityInput>
	{
		private readonly IAzureDevOpsWorkItemClient _workItemClient;
		private const int DefaultDaysBack = 7;

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
		};

		public DiscoverUserActivityCapability(IAzureDevOpsWorkItemClient workItemClient)
		{
			_workItemClient = workItemClient;
		}

		/// <inheritdoc />
		public string Name => "DiscoverUserActivity";

		/// <inheritdoc />
		public string Description => "Discovers user activity across Azure DevOps and integrated services, including work items, pull requests, commits, and documents within a configurable time period.";

		/// <inheritdoc />
		public async Task<string> ExecuteAsync(DiscoverUserActivityCapabilityInput input)
		{
			if (string.IsNullOrWhiteSpace(input.UserEmail))
			{
				throw new ArgumentException("'userEmail' is required and must be a valid email address.");
			}

			var daysBack = input.DaysBack > 0 ? input.DaysBack : DefaultDaysBack;
			var activityTypes = input.ActivityTypes;

			// Build list of tasks for each activity category
			var tasks = new List<Task<object?>>();

			// Work Items
			if (_HasAnyWorkItemActivityType(activityTypes))
			{
				tasks.Add(_GetWorkItemActivityAsync(input.UserEmail, daysBack, activityTypes, input.WorkItemOptions));
			}

			// Future activity categories:
			// if (_HasAnyPullRequestActivityType(activityTypes))
			// {
			//     tasks.Add(_GetPullRequestActivityAsync(input.UserEmail, daysBack, activityTypes, input.PullRequestOptions));
			// }
			//
			// if (_HasAnyDocumentActivityType(activityTypes))
			// {
			//     tasks.Add(_GetDocumentActivityAsync(input.UserEmail, daysBack, activityTypes, input.DocumentOptions));
			// }

			// Execute all category tasks in parallel
			var results = await Task.WhenAll(tasks);

			// Build the result object
			var result = new
			{
				userEmail = input.UserEmail,
				periodDays = daysBack,
				activityTypes = activityTypes.ToString(),
				workItems = _HasAnyWorkItemActivityType(activityTypes) ? results.FirstOrDefault(r => r is WorkItemActivityResult) : null,
				// pullRequests = _HasAnyPullRequestActivityType(activityTypes) ? results.FirstOrDefault(r => r is PullRequestActivityResult) : null,
				// documents = _HasAnyDocumentActivityType(activityTypes) ? results.FirstOrDefault(r => r is DocumentActivityResult) : null,
			};

			return JsonSerializer.Serialize(result, JsonOptions);
		}

		#region Work Item Activity

		private static readonly string[] DefaultWorkItemFields = new[]
		{
			"System.Id",
			"System.Title",
			"System.State",
			"System.WorkItemType",
			"System.AssignedTo",
			"System.CreatedBy",
			"System.CreatedDate",
			"System.ChangedBy",
			"System.ChangedDate",
			"System.AreaPath"
		};

		private static bool _HasAnyWorkItemActivityType(UserActivityType activityTypes)
		{
			return activityTypes.HasFlag(UserActivityType.WorkItemsAssigned) ||
				   activityTypes.HasFlag(UserActivityType.WorkItemsChanged) ||
				   activityTypes.HasFlag(UserActivityType.WorkItemsCreated) ||
				   activityTypes.HasFlag(UserActivityType.WorkItemComments);
		}

		private async Task<object?> _GetWorkItemActivityAsync(
			string userEmail,
			int daysBack,
			UserActivityType activityTypes,
			WorkItemActivityOptions? options)
		{
			var fields = options?.Fields ?? DefaultWorkItemFields;
			var states = options?.States;
			var workItemTypes = options?.WorkItemTypes;

			// Initialize result collections
			var assignedItems = new List<object>();
			var changedItems = new List<object>();
			var createdItems = new List<object>();

			// Build list of tasks based on requested work item activity types
			var tasks = new List<Task>();

			if (activityTypes.HasFlag(UserActivityType.WorkItemsAssigned))
			{
				tasks.Add(_FetchAssignedWorkItemsAsync(userEmail, states, fields, workItemTypes, assignedItems));
			}

			if (activityTypes.HasFlag(UserActivityType.WorkItemsChanged))
			{
				tasks.Add(_FetchChangedWorkItemsAsync(userEmail, daysBack, states, fields, workItemTypes, changedItems));
			}

			if (activityTypes.HasFlag(UserActivityType.WorkItemsCreated))
			{
				tasks.Add(_FetchCreatedWorkItemsAsync(userEmail, daysBack, states, fields, workItemTypes, createdItems));
			}

			// WorkItemComments not yet implemented
			// if (activityTypes.HasFlag(UserActivityType.WorkItemComments))
			// {
			//     tasks.Add(_FetchWorkItemCommentsAsync(userEmail, daysBack, commentItems));
			// }

			// Execute all tasks in parallel
			await Task.WhenAll(tasks);

			return new WorkItemActivityResult
			{
				Assigned = activityTypes.HasFlag(UserActivityType.WorkItemsAssigned) ? assignedItems : null,
				Changed = activityTypes.HasFlag(UserActivityType.WorkItemsChanged) ? changedItems : null,
				Created = activityTypes.HasFlag(UserActivityType.WorkItemsCreated) ? createdItems : null
			};
		}

		private async Task _FetchAssignedWorkItemsAsync(
			string userEmail,
			string[]? states,
			IEnumerable<string> fields,
			string[]? workItemTypes,
			List<object> results)
		{
			var json = await _workItemClient.GetWorkItemsByAssignedUserAsync(
				userEmail,
				states,
				fields,
				iterationPath: null,
				workItemTypes);

			var items = _ParseWorkItems(json);
			results.AddRange(items);
		}

		private async Task _FetchChangedWorkItemsAsync(
			string userEmail,
			int daysBack,
			string[]? states,
			IEnumerable<string> fields,
			string[]? workItemTypes,
			List<object> results)
		{
			var json = await _workItemClient.GetWorkItemsChangedByUserAsync(
				userEmail,
				daysBack,
				states,
				fields,
				workItemTypes);

			var items = _ParseWorkItems(json);
			results.AddRange(items);
		}

		private async Task _FetchCreatedWorkItemsAsync(
			string userEmail,
			int daysBack,
			string[]? states,
			IEnumerable<string> fields,
			string[]? workItemTypes,
			List<object> results)
		{
			var json = await _workItemClient.GetWorkItemsCreatedByUserAsync(
				userEmail,
				daysBack,
				states,
				fields,
				workItemTypes);

			var items = _ParseWorkItems(json);
			results.AddRange(items);
		}

		private static List<object> _ParseWorkItems(string json)
		{
			var result = new List<object>();

			using var doc = JsonDocument.Parse(json);

			// Handle { count, value } wrapper format from client
			JsonElement itemsArray;
			if (doc.RootElement.TryGetProperty("value", out var valueElement))
			{
				itemsArray = valueElement;
			}
			else if (doc.RootElement.ValueKind == JsonValueKind.Array)
			{
				itemsArray = doc.RootElement;
			}
			else
			{
				return result;
			}

			foreach (var item in itemsArray.EnumerateArray())
			{
				var workItem = new
				{
					Id = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
					Title = _GetFieldString(item, "System.Title"),
					State = _GetFieldString(item, "System.State"),
					WorkItemType = _GetFieldString(item, "System.WorkItemType"),
					AreaPath = _GetFieldString(item, "System.AreaPath"),
					AssignedTo = _GetFieldString(item, "System.AssignedTo"),
					CreatedBy = _GetFieldString(item, "System.CreatedBy"),
					CreatedDate = _GetFieldString(item, "System.CreatedDate"),
					ChangedBy = _GetFieldString(item, "System.ChangedBy"),
					ChangedDate = _GetFieldString(item, "System.ChangedDate")
				};

				result.Add(workItem);
			}

			return result;
		}

		private static string? _GetFieldString(JsonElement item, string fieldName)
		{
			if (!item.TryGetProperty("fields", out var fieldsElement))
			{
				return null;
			}

			if (fieldsElement.TryGetProperty(fieldName, out var fieldValue))
			{
				return fieldValue.ValueKind == JsonValueKind.Null ? null : fieldValue.ToString();
			}

			return null;
		}

		#endregion

		#region Result Models

		private class WorkItemActivityResult
		{
			public List<object>? Assigned { get; init; }
			public List<object>? Changed { get; init; }
			public List<object>? Created { get; init; }
		}

		// Future result models:
		// private class PullRequestActivityResult { ... }
		// private class DocumentActivityResult { ... }

		#endregion
	}
}
