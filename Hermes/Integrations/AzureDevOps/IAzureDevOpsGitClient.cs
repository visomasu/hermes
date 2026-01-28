namespace Integrations.AzureDevOps
{
	/// <summary>
	/// Interface for Azure DevOps Git client operations including pull requests.
	/// </summary>
	public interface IAzureDevOpsGitClient
	{
		/// <summary>
		/// Gets pull requests created by a specific user within a date range.
		/// </summary>
		/// <param name="userEmail">Email address of the PR creator.</param>
		/// <param name="daysBack">Number of days to look back.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>JSON string with PR results in { count, value } format.</returns>
		Task<string> GetPullRequestsCreatedByUserAsync(
			string userEmail,
			int daysBack,
			CancellationToken cancellationToken = default);
	}
}
