using System.Text.Json;
using Integrations.AzureDevOps;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Microsoft.Extensions.Logging;

namespace Hermes.Tools.AzureDevOps.Capabilities
{
	/// <summary>
	/// Capability for discovering user pull request activity in Azure DevOps.
	/// </summary>
	public sealed class DiscoverUserActivityCapability : IAgentToolCapability<DiscoverUserActivityCapabilityInput>
	{
		private readonly IAzureDevOpsGitClient _gitClient;
		private readonly ILogger<DiscoverUserActivityCapability> _logger;
		private const int DefaultDaysBack = 7;

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
		};

		public DiscoverUserActivityCapability(
			IAzureDevOpsGitClient gitClient,
			ILogger<DiscoverUserActivityCapability> logger)
		{
			_gitClient = gitClient;
			_logger = logger;
		}

		/// <inheritdoc />
		public string Name => "DiscoverUserActivity";

		/// <inheritdoc />
		public string Description => "Discovers user pull request activity in Azure DevOps within a configurable time period.";

		/// <inheritdoc />
		public async Task<string> ExecuteAsync(DiscoverUserActivityCapabilityInput input)
		{
			if (string.IsNullOrWhiteSpace(input.UserEmail))
			{
				throw new ArgumentException("'userEmail' is required and must be a valid email address.");
			}

			var daysBack = input.DaysBack > 0 ? input.DaysBack : DefaultDaysBack;

			_logger.LogInformation(
				"Discovering user activity for {UserEmail} over last {DaysBack} days",
				input.UserEmail, daysBack);

			_logger.LogInformation("Fetching pull request activity for {UserEmail}", input.UserEmail);
			var pullRequestResult = await _GetPullRequestActivityAsync(input.UserEmail, daysBack);

			var result = new
			{
				userEmail = input.UserEmail,
				periodDays = daysBack,
				pullRequests = pullRequestResult
			};

			_logger.LogInformation("User activity discovery completed for {UserEmail}", input.UserEmail);

			return JsonSerializer.Serialize(result, JsonOptions);
		}

		#region Pull Request Activity

		private async Task<PullRequestActivityResult> _GetPullRequestActivityAsync(
			string userEmail,
			int daysBack)
		{
			var json = await _gitClient.GetPullRequestsCreatedByUserAsync(
				userEmail,
				daysBack);

			var createdPullRequests = _ParsePullRequests(json);

			_logger.LogInformation(
				"Pull request activity retrieved for {UserEmail}: {CreatedCount} created",
				userEmail, createdPullRequests.Count);

			return new PullRequestActivityResult
			{
				Created = createdPullRequests
			};
		}

		private static List<object> _ParsePullRequests(string json)
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
				var pullRequest = new
				{
					PullRequestId = item.TryGetProperty("pullrequestid", out var idProp) ? idProp.GetInt32() : 0,
					Title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null,
					Status = item.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null,
					CreatedBy = item.TryGetProperty("createdby", out var createdByProp) ? createdByProp.GetString() : null,
					CreatedByEmail = item.TryGetProperty("createdbyemail", out var emailProp) ? emailProp.GetString() : null,
					CreationDate = item.TryGetProperty("creationdate", out var creationDateProp) ? creationDateProp.GetString() : null,
					ClosedDate = item.TryGetProperty("closeddate", out var closedDateProp) ? closedDateProp.GetString() : null,
					RepositoryName = item.TryGetProperty("repositoryname", out var repoProp) ? repoProp.GetString() : null,
					SourceRefName = item.TryGetProperty("sourcerefname", out var sourceProp) ? sourceProp.GetString() : null,
					TargetRefName = item.TryGetProperty("targetrefname", out var targetProp) ? targetProp.GetString() : null,
					Url = item.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null
				};

				result.Add(pullRequest);
			}

			return result;
		}

		#endregion

		#region Result Models

		private class PullRequestActivityResult
		{
			public List<object>? Created { get; init; }
		}

		#endregion
	}
}
