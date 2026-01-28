using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Integrations.AzureDevOps;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Hermes.Tests.Tools.AzureDevOps.Capabilities
{
	public class DiscoverUserActivityCapabilityTests
	{
		private const string TestUserEmail = "test.user@example.com";
		private static readonly ILogger<DiscoverUserActivityCapability> NullLogger =
			new Mock<ILogger<DiscoverUserActivityCapability>>().Object;

		private static string CreatePullRequestsJson(int[] ids)
		{
			var items = ids.Select(id => new
			{
				pullrequestid = id,
				title = $"PR {id}",
				status = "Active",
				createdby = "Test User",
				createdbyemail = TestUserEmail,
				creationdate = "2026-01-20T10:00:00Z",
				repositoryname = "test-repo",
				sourcerefname = "refs/heads/feature",
				targetrefname = "refs/heads/main"
			});
			return JsonSerializer.Serialize(new { count = ids.Length, value = items });
		}

		[Fact]
		public void Name_ReturnsCorrectCapabilityName()
		{
			var mockGitClient = new Mock<IAzureDevOpsGitClient>();
			var capability = new DiscoverUserActivityCapability(mockGitClient.Object, NullLogger);

			var name = capability.Name;

			Assert.Equal("DiscoverUserActivity", name);
		}

		[Fact]
		public void Description_ReturnsNonEmptyDescription()
		{
			var mockGitClient = new Mock<IAzureDevOpsGitClient>();
			var capability = new DiscoverUserActivityCapability(mockGitClient.Object, NullLogger);

			var description = capability.Description;

			Assert.NotNull(description);
			Assert.NotEmpty(description);
			Assert.Contains("pull request", description, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task ExecuteAsync_ValidInput_ReturnsPullRequests()
		{
			var mockGitClient = new Mock<IAzureDevOpsGitClient>();

			mockGitClient.Setup(x => x.GetPullRequestsCreatedByUserAsync(
					TestUserEmail,
					It.IsAny<int>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreatePullRequestsJson(new[] { 1, 2, 3 }));

			var capability = new DiscoverUserActivityCapability(mockGitClient.Object, NullLogger);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				DaysBack = 7
			};

			var result = await capability.ExecuteAsync(input);

			Assert.NotNull(result);
			using var doc = JsonDocument.Parse(result);
			var root = doc.RootElement;

			Assert.Equal(TestUserEmail, root.GetProperty("userEmail").GetString());
			Assert.Equal(7, root.GetProperty("periodDays").GetInt32());

			var pullRequests = root.GetProperty("pullRequests");
			Assert.Equal(3, pullRequests.GetProperty("created").GetArrayLength());
		}

		[Fact]
		public async Task ExecuteAsync_EmptyUserEmail_ThrowsArgumentException()
		{
			var mockGitClient = new Mock<IAzureDevOpsGitClient>();
			var capability = new DiscoverUserActivityCapability(mockGitClient.Object, NullLogger);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = "",
				DaysBack = 7
			};

			await Assert.ThrowsAsync<ArgumentException>(() => capability.ExecuteAsync(input));
		}

		[Fact]
		public async Task ExecuteAsync_DefaultDaysBack_UsesSevenDays()
		{
			var mockGitClient = new Mock<IAzureDevOpsGitClient>();
			int capturedDaysBack = 0;

			mockGitClient.Setup(x => x.GetPullRequestsCreatedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<CancellationToken>()))
				.Callback<string, int, CancellationToken>(
					(_, days, _) => capturedDaysBack = days)
				.ReturnsAsync(CreatePullRequestsJson(Array.Empty<int>()));

			var capability = new DiscoverUserActivityCapability(mockGitClient.Object, NullLogger);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				DaysBack = 0
			};

			await capability.ExecuteAsync(input);

			Assert.Equal(7, capturedDaysBack);
		}

		[Fact]
		public async Task ExecuteAsync_CustomDaysBack_PassesCorrectValue()
		{
			var mockGitClient = new Mock<IAzureDevOpsGitClient>();
			int capturedDaysBack = 0;

			mockGitClient.Setup(x => x.GetPullRequestsCreatedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<CancellationToken>()))
				.Callback<string, int, CancellationToken>(
					(_, days, _) => capturedDaysBack = days)
				.ReturnsAsync(CreatePullRequestsJson(Array.Empty<int>()));

			var capability = new DiscoverUserActivityCapability(mockGitClient.Object, NullLogger);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				DaysBack = 30
			};

			await capability.ExecuteAsync(input);

			Assert.Equal(30, capturedDaysBack);
		}

		[Fact]
		public async Task ExecuteAsync_NoResultsFound_ReturnsEmptyArray()
		{
			var mockGitClient = new Mock<IAzureDevOpsGitClient>();
			var emptyResult = JsonSerializer.Serialize(new { count = 0, value = Array.Empty<object>() });

			mockGitClient.Setup(x => x.GetPullRequestsCreatedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(emptyResult);

			var capability = new DiscoverUserActivityCapability(mockGitClient.Object, NullLogger);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				DaysBack = 7
			};

			var result = await capability.ExecuteAsync(input);

			using var doc = JsonDocument.Parse(result);
			var pullRequests = doc.RootElement.GetProperty("pullRequests");
			Assert.Equal(0, pullRequests.GetProperty("created").GetArrayLength());
		}

		[Fact]
		public async Task ExecuteAsync_CallsGitClientWithCorrectEmail()
		{
			var mockGitClient = new Mock<IAzureDevOpsGitClient>();
			string? capturedEmail = null;

			mockGitClient.Setup(x => x.GetPullRequestsCreatedByUserAsync(
					It.IsAny<string>(),
					It.IsAny<int>(),
					It.IsAny<CancellationToken>()))
				.Callback<string, int, CancellationToken>(
					(email, _, _) => capturedEmail = email)
				.ReturnsAsync(CreatePullRequestsJson(Array.Empty<int>()));

			var capability = new DiscoverUserActivityCapability(mockGitClient.Object, NullLogger);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = TestUserEmail,
				DaysBack = 7
			};

			await capability.ExecuteAsync(input);

			Assert.Equal(TestUserEmail, capturedEmail);
		}

		[Fact]
		public async Task ExecuteAsync_WhitespaceUserEmail_ThrowsArgumentException()
		{
			var mockGitClient = new Mock<IAzureDevOpsGitClient>();
			var capability = new DiscoverUserActivityCapability(mockGitClient.Object, NullLogger);
			var input = new DiscoverUserActivityCapabilityInput
			{
				UserEmail = "   ",
				DaysBack = 7
			};

			await Assert.ThrowsAsync<ArgumentException>(() => capability.ExecuteAsync(input));
		}
	}
}
