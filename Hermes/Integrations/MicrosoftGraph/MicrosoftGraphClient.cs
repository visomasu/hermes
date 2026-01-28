using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Hermes.Integrations.MicrosoftGraph;

/// <summary>
/// Implementation of Microsoft Graph client using DefaultAzureCredential for authentication.
/// Supports local development (az login) and production (Managed Identity).
/// </summary>
public class MicrosoftGraphClient : IMicrosoftGraphClient
{
	private readonly GraphServiceClient _graphClient;
	private readonly ILogger<MicrosoftGraphClient> _logger;

	private static readonly string[] DefaultScopes = new[] { "https://graph.microsoft.com/.default" };

	public MicrosoftGraphClient(ILogger<MicrosoftGraphClient> logger)
	{
		_logger = logger;

		// Use DefaultAzureCredential which automatically tries:
		// 1. Azure CLI (az login) for local development
		// 2. Managed Identity for production (Azure App Service, Container Apps, etc.)
		// 3. Environment variables
		// 4. Visual Studio credentials
		var credential = new DefaultAzureCredential();

		_graphClient = new GraphServiceClient(credential, DefaultScopes);

		_logger.LogInformation("Microsoft Graph client initialized using DefaultAzureCredential");
	}

	/// <inheritdoc/>
	public async Task<string?> GetUserEmailAsync(
		string teamsUserId,
		CancellationToken cancellationToken = default)
	{
		try
		{
			_logger.LogDebug("Fetching email for user {TeamsUserId}", teamsUserId);

			var user = await _graphClient.Users[teamsUserId]
				.GetAsync(requestConfiguration =>
				{
					requestConfiguration.QueryParameters.Select = new[] { "mail", "userPrincipalName" };
				}, cancellationToken);

			var email = user?.Mail ?? user?.UserPrincipalName;

			if (string.IsNullOrWhiteSpace(email))
			{
				_logger.LogWarning("No email found for user {TeamsUserId}", teamsUserId);
				return null;
			}

			_logger.LogDebug("Retrieved email {Email} for user {TeamsUserId}", email, teamsUserId);
			return email;
		}
		catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
		{
			_logger.LogWarning("User {TeamsUserId} not found in Azure AD", teamsUserId);
			return null;
		}
		catch (ServiceException ex)
		{
			_logger.LogError(ex, "Microsoft Graph API error while fetching email for user {TeamsUserId}. StatusCode: {StatusCode}",
				teamsUserId, ex.ResponseStatusCode);
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error while fetching email for user {TeamsUserId}", teamsUserId);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<List<string>> GetDirectReportEmailsAsync(
		string teamsUserId,
		CancellationToken cancellationToken = default)
	{
		try
		{
			_logger.LogDebug("Fetching direct reports for user {TeamsUserId}", teamsUserId);

			var directReports = await _graphClient.Users[teamsUserId]
				.DirectReports
				.GetAsync(requestConfiguration =>
				{
					requestConfiguration.QueryParameters.Select = new[] { "mail", "userPrincipalName" };
				}, cancellationToken);

			if (directReports?.Value == null || directReports.Value.Count == 0)
			{
				_logger.LogDebug("No direct reports found for user {TeamsUserId}", teamsUserId);
				return new List<string>();
			}

			// Extract emails from direct reports (cast to User type)
			var emails = directReports.Value
				.OfType<User>()
				.Select(u => u.Mail ?? u.UserPrincipalName)
				.Where(email => !string.IsNullOrWhiteSpace(email))
				.Cast<string>()
				.ToList();

			_logger.LogDebug("Retrieved {Count} direct report emails for user {TeamsUserId}", emails.Count, teamsUserId);
			return emails;
		}
		catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
		{
			_logger.LogWarning("User {TeamsUserId} not found in Azure AD when fetching direct reports", teamsUserId);
			return new List<string>();
		}
		catch (ServiceException ex)
		{
			_logger.LogError(ex, "Microsoft Graph API error while fetching direct reports for user {TeamsUserId}. StatusCode: {StatusCode}",
				teamsUserId, ex.ResponseStatusCode);
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error while fetching direct reports for user {TeamsUserId}", teamsUserId);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<UserProfileResult> GetUserProfileWithDirectReportsAsync(
		string teamsUserId,
		CancellationToken cancellationToken = default)
	{
		try
		{
			_logger.LogDebug("Fetching complete user profile for user {TeamsUserId}", teamsUserId);

			// Fetch email and direct reports in parallel for efficiency
			var emailTask = GetUserEmailAsync(teamsUserId, cancellationToken);
			var directReportsTask = GetDirectReportEmailsAsync(teamsUserId, cancellationToken);

			await Task.WhenAll(emailTask, directReportsTask);

			var result = new UserProfileResult
			{
				Email = await emailTask ?? string.Empty,
				DirectReportEmails = await directReportsTask
			};

			_logger.LogInformation(
				"Retrieved profile for user {TeamsUserId}: Email={Email}, IsManager={IsManager}, DirectReports={Count}",
				teamsUserId,
				result.Email,
				result.IsManager,
				result.DirectReportEmails.Count);

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching complete user profile for user {TeamsUserId}", teamsUserId);
			throw;
		}
	}
}
