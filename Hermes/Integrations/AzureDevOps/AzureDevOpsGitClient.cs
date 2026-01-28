using Exceptions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;
using Microsoft.VisualStudio.Services.WebApi;
using System.Text.Json;

namespace Integrations.AzureDevOps
{
	/// <summary>
	/// Implementation of IAzureDevOpsGitClient for Azure DevOps Git operations using the official .NET SDK.
	/// </summary>
	public class AzureDevOpsGitClient : IAzureDevOpsGitClient
	{
		private GitHttpClient? _gitClient;
		private IdentityHttpClient? _identityClient;
		private readonly string _project;
		private readonly VssConnection _connection;

		private static readonly JsonSerializerOptions LowercaseJsonOptions = new()
		{
			PropertyNamingPolicy = new LowercaseNamingPolicy(),
		};

		private class LowercaseNamingPolicy : JsonNamingPolicy
		{
			public override string ConvertName(string name) => name.ToLowerInvariant();
		}

		/// <summary>
		/// Initializes a new instance of the AzureDevOpsGitClient class.
		/// </summary>
		/// <param name="organization">Azure DevOps organization name.</param>
		/// <param name="project">Azure DevOps project name.</param>
		/// <param name="personalAccessToken">Personal Access Token for authentication.</param>
		public AzureDevOpsGitClient(string organization, string project, string personalAccessToken)
		{
			_project = project;
			_connection = new VssConnection(
				new Uri($"https://dev.azure.com/{organization}"),
				new VssBasicCredential(string.Empty, personalAccessToken)
			);
		}

		private GitHttpClient _GetClient()
		{
			if (_gitClient == null)
			{
				try
				{
					_gitClient = _connection.GetClient<GitHttpClient>();
				}
				catch (VssServiceException ex)
				{
					throw new IntegrationException(
						$"Azure DevOps service error during Git client initialization: {ex.Message}",
						IntegrationException.ErrorCode.ServiceError, ex);
				}
				catch (VssAuthenticationException ex)
				{
					throw new IntegrationException(
						$"Azure DevOps authentication error during Git client initialization: {ex.Message}",
						IntegrationException.ErrorCode.AuthenticationError, ex);
				}
				catch (Exception ex)
				{
					throw new IntegrationException(
						$"Unexpected error during Git client initialization: {ex.Message}",
						IntegrationException.ErrorCode.UnexpectedError, ex);
				}
			}
			return _gitClient;
		}

		/// <inheritdoc/>
		public async Task<string> GetPullRequestsCreatedByUserAsync(
			string userEmail,
			int daysBack,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(userEmail))
			{
				throw new IntegrationException(
					"userEmail must not be null or empty",
					IntegrationException.ErrorCode.UnexpectedError);
			}

			try
			{
				var client = _GetClient();

				// Resolve user email to identity GUID
				var creatorId = await _ResolveUserIdAsync(userEmail, cancellationToken);

				var searchCriteria = new GitPullRequestSearchCriteria
				{
					CreatorId = creatorId != Guid.Empty ? creatorId : null,
					MinTime = daysBack > 0 ? DateTime.UtcNow.AddDays(-daysBack) : null
				};

				var prs = await client.GetPullRequestsByProjectAsync(
					_project,
					searchCriteria,
					cancellationToken: cancellationToken);

				var allPullRequests = prs.Select(pr => _MapPullRequest(pr)).ToList();

				return JsonSerializer.Serialize(
					new { count = allPullRequests.Count, value = allPullRequests },
					LowercaseJsonOptions);
			}
			catch (IntegrationException)
			{
				throw;
			}
			catch (VssServiceException ex)
			{
				throw new IntegrationException(
					$"Azure DevOps service error while querying pull requests for user '{userEmail}': {ex.Message}",
					IntegrationException.ErrorCode.ServiceError, ex);
			}
			catch (VssAuthenticationException ex)
			{
				throw new IntegrationException(
					$"Azure DevOps authentication error: {ex.Message}",
					IntegrationException.ErrorCode.AuthenticationError, ex);
			}
			catch (Exception ex)
			{
				throw new IntegrationException(
					$"Error querying pull requests for user '{userEmail}': {ex.Message}",
					IntegrationException.ErrorCode.UnexpectedError, ex);
			}
		}

		private async Task<Guid> _ResolveUserIdAsync(string userEmail, CancellationToken cancellationToken)
		{
			if (_identityClient == null)
			{
				_identityClient = _connection.GetClient<IdentityHttpClient>();
			}

			var identities = await _identityClient.ReadIdentitiesAsync(
				IdentitySearchFilter.General,
				userEmail,
				ReadIdentitiesOptions.None,
				QueryMembership.None,
				cancellationToken: cancellationToken);

			return identities?.FirstOrDefault()?.Id ?? Guid.Empty;
		}

		private static object _MapPullRequest(GitPullRequest pr)
		{
			return new
			{
				pullRequestId = pr.PullRequestId,
				title = pr.Title,
				description = pr.Description,
				status = pr.Status.ToString(),
				createdBy = pr.CreatedBy?.DisplayName,
				createdByEmail = pr.CreatedBy?.UniqueName,
				creationDate = pr.CreationDate,
				closedDate = pr.ClosedDate,
				sourceRefName = pr.SourceRefName,
				targetRefName = pr.TargetRefName,
				repositoryName = pr.Repository?.Name,
				url = pr.Url,
				reviewers = pr.Reviewers?.Select(r => new
				{
					displayName = r.DisplayName,
					email = r.UniqueName,
					vote = r.Vote
				}).ToList()
			};
		}
	}
}
