namespace Hermes.Tools.AzureDevOps.Capabilities.Inputs
{
	/// <summary>
	/// Types of user activity that can be discovered across Azure DevOps and integrated services.
	/// </summary>
	[Flags]
	public enum UserActivityType
	{
		/// <summary>
		/// No activity types selected.
		/// </summary>
		None = 0,

		// =====================================================
		// Work Item Activity Types (1-4)
		// =====================================================

		/// <summary>
		/// Work items assigned to the user.
		/// </summary>
		WorkItemsAssigned = 1 << 0,

		/// <summary>
		/// Work items changed by the user.
		/// </summary>
		WorkItemsChanged = 1 << 1,

		/// <summary>
		/// Work items created by the user.
		/// </summary>
		WorkItemsCreated = 1 << 2,

		/// <summary>
		/// Comments made by the user on work items.
		/// </summary>
		WorkItemComments = 1 << 3,

		/// <summary>
		/// All work item activity types.
		/// </summary>
		AllWorkItems = WorkItemsAssigned | WorkItemsChanged | WorkItemsCreated | WorkItemComments,

		// =====================================================
		// Pull Request Activity Types (16-64)
		// =====================================================

		/// <summary>
		/// Pull requests created by the user.
		/// </summary>
		PullRequestsCreated = 1 << 4,

		/// <summary>
		/// Pull requests where the user is a reviewer.
		/// </summary>
		PullRequestsReviewed = 1 << 5,

		/// <summary>
		/// Pull requests where the user has commented.
		/// </summary>
		PullRequestsCommented = 1 << 6,

		/// <summary>
		/// All pull request activity types.
		/// </summary>
		AllPullRequests = PullRequestsCreated | PullRequestsReviewed | PullRequestsCommented,

		// =====================================================
		// Code/Commit Activity Types (128-256)
		// =====================================================

		/// <summary>
		/// Commits authored by the user.
		/// </summary>
		Commits = 1 << 7,

		/// <summary>
		/// Code pushes by the user.
		/// </summary>
		Pushes = 1 << 8,

		/// <summary>
		/// All code activity types.
		/// </summary>
		AllCode = Commits | Pushes,

		// =====================================================
		// Wiki/Documentation Activity Types (512-1024)
		// =====================================================

		/// <summary>
		/// Wiki pages created by the user.
		/// </summary>
		WikiPagesCreated = 1 << 9,

		/// <summary>
		/// Wiki pages edited by the user.
		/// </summary>
		WikiPagesEdited = 1 << 10,

		/// <summary>
		/// All wiki activity types.
		/// </summary>
		AllWiki = WikiPagesCreated | WikiPagesEdited,

		// =====================================================
		// Build/Pipeline Activity Types (2048-4096)
		// =====================================================

		/// <summary>
		/// Builds triggered by the user.
		/// </summary>
		BuildsTriggered = 1 << 11,

		/// <summary>
		/// Releases deployed by the user.
		/// </summary>
		ReleasesDeployed = 1 << 12,

		/// <summary>
		/// All build/pipeline activity types.
		/// </summary>
		AllBuilds = BuildsTriggered | ReleasesDeployed,

		// =====================================================
		// Testing Activity Types (8192-32768)
		// =====================================================

		/// <summary>
		/// Test cases created by the user.
		/// </summary>
		TestCasesCreated = 1 << 13,

		/// <summary>
		/// Test runs executed by the user.
		/// </summary>
		TestRunsExecuted = 1 << 14,

		/// <summary>
		/// Test results submitted by the user.
		/// </summary>
		TestResultsSubmitted = 1 << 15,

		/// <summary>
		/// All testing activity types.
		/// </summary>
		AllTesting = TestCasesCreated | TestRunsExecuted | TestResultsSubmitted,

		// =====================================================
		// Approval Activity Types (65536-131072)
		// =====================================================

		/// <summary>
		/// Approvals given by the user (deployments, PRs, etc.).
		/// </summary>
		ApprovalsGiven = 1 << 16,

		/// <summary>
		/// Items pending the user's approval.
		/// </summary>
		ApprovalsPending = 1 << 17,

		/// <summary>
		/// All approval activity types.
		/// </summary>
		AllApprovals = ApprovalsGiven | ApprovalsPending,

		// =====================================================
		// Artifact/Package Activity Types (262144-524288)
		// =====================================================

		/// <summary>
		/// Packages published by the user (NuGet, npm, etc.).
		/// </summary>
		PackagesPublished = 1 << 18,

		/// <summary>
		/// Build artifacts uploaded by the user.
		/// </summary>
		ArtifactsUploaded = 1 << 19,

		/// <summary>
		/// All artifact/package activity types.
		/// </summary>
		AllArtifacts = PackagesPublished | ArtifactsUploaded,

		// =====================================================
		// Query/Dashboard Activity Types (1048576-2097152)
		// =====================================================

		/// <summary>
		/// Work item queries created or saved by the user.
		/// </summary>
		QueriesCreated = 1 << 20,

		/// <summary>
		/// Dashboards created or modified by the user.
		/// </summary>
		DashboardsModified = 1 << 21,

		/// <summary>
		/// All query/dashboard activity types.
		/// </summary>
		AllQueriesDashboards = QueriesCreated | DashboardsModified,

		// =====================================================
		// Communication Activity Types (4194304-16777216)
		// =====================================================

		/// <summary>
		/// Teams messages sent by the user related to projects.
		/// </summary>
		TeamsMessages = 1 << 22,

		/// <summary>
		/// Times the user was @mentioned in work items, PRs, or discussions.
		/// </summary>
		Mentions = 1 << 23,

		/// <summary>
		/// All communication activity types.
		/// </summary>
		AllCommunication = TeamsMessages | Mentions,

		// =====================================================
		// OneDrive/SharePoint Document Activity Types (33554432-134217728)
		// =====================================================

		/// <summary>
		/// Documents created by the user in OneDrive/SharePoint.
		/// </summary>
		DocumentsCreated = 1 << 24,

		/// <summary>
		/// Documents edited by the user in OneDrive/SharePoint.
		/// </summary>
		DocumentsEdited = 1 << 25,

		/// <summary>
		/// Documents shared by the user in OneDrive/SharePoint.
		/// </summary>
		DocumentsShared = 1 << 26,

		/// <summary>
		/// All document activity types.
		/// </summary>
		AllDocuments = DocumentsCreated | DocumentsEdited | DocumentsShared,

		// =====================================================
		// Aggregate Types
		// =====================================================

		/// <summary>
		/// All Azure DevOps activity types.
		/// </summary>
		AllAzureDevOps = AllWorkItems | AllPullRequests | AllCode | AllWiki | AllBuilds | AllTesting | AllApprovals | AllArtifacts | AllQueriesDashboards,

		/// <summary>
		/// All supported activity types across all integrated services.
		/// </summary>
		All = AllAzureDevOps | AllCommunication | AllDocuments
	}
}
