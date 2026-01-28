using Exceptions;
using Integrations.AzureDevOps;
using Xunit;

namespace Hermes.Tests.Integrations.AzureDevOps
{
	public class AzureDevOpsGitClientTests
	{
		[Fact]
		public async Task GetPullRequestsCreatedByUserAsync_ThrowsIntegrationException_OnNullEmail()
		{
			var client = new AzureDevOpsGitClient("invalidOrg", "invalidProject");
			await Assert.ThrowsAsync<IntegrationException>(() =>
				client.GetPullRequestsCreatedByUserAsync(null!, 7));
		}

		[Fact]
		public async Task GetPullRequestsCreatedByUserAsync_ThrowsIntegrationException_OnEmptyEmail()
		{
			var client = new AzureDevOpsGitClient("invalidOrg", "invalidProject");
			await Assert.ThrowsAsync<IntegrationException>(() =>
				client.GetPullRequestsCreatedByUserAsync(string.Empty, 7));
		}

		[Fact]
		public async Task GetPullRequestsCreatedByUserAsync_ThrowsIntegrationException_OnWhitespaceEmail()
		{
			var client = new AzureDevOpsGitClient("invalidOrg", "invalidProject");
			await Assert.ThrowsAsync<IntegrationException>(() =>
				client.GetPullRequestsCreatedByUserAsync("   ", 7));
		}
	}
}
