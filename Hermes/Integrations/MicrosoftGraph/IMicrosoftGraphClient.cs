namespace Hermes.Integrations.MicrosoftGraph;

/// <summary>
/// Client for interacting with Microsoft Graph API to retrieve user and organizational information.
/// </summary>
public interface IMicrosoftGraphClient
{
	/// <summary>
	/// Gets user's email from Azure AD profile.
	/// </summary>
	/// <param name="teamsUserId">The Azure AD object ID of the user</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>User's email address, or null if not found</returns>
	Task<string?> GetUserEmailAsync(
		string teamsUserId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets emails of user's direct reports.
	/// </summary>
	/// <param name="teamsUserId">The Azure AD object ID of the user</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of direct report email addresses</returns>
	Task<List<string>> GetDirectReportEmailsAsync(
		string teamsUserId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets user profile with direct reports in parallel (optimized).
	/// </summary>
	/// <param name="teamsUserId">The Azure AD object ID of the user</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Complete user profile including direct reports</returns>
	Task<UserProfileResult> GetUserProfileWithDirectReportsAsync(
		string teamsUserId,
		CancellationToken cancellationToken = default);
}

/// <summary>
/// Result model containing user profile information including direct reports.
/// </summary>
public class UserProfileResult
{
	/// <summary>
	/// User's email address from Azure AD.
	/// </summary>
	public string Email { get; set; } = string.Empty;

	/// <summary>
	/// List of direct report email addresses.
	/// </summary>
	public List<string> DirectReportEmails { get; set; } = new();

	/// <summary>
	/// Optional area paths to filter work items for SLA violation checks.
	/// If empty, all area paths are checked.
	/// </summary>
	public List<string> AreaPaths { get; set; } = new();

	/// <summary>
	/// Indicates whether the user is a manager (has direct reports).
	/// </summary>
	public bool IsManager => DirectReportEmails.Count > 0;
}
