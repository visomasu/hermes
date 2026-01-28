using System.Threading.Tasks;
using System.Collections.Generic;

namespace Integrations.AzureDevOps
{
    /// <summary>
    /// Interface for Azure DevOps work item client.
    /// </summary>
    public interface IAzureDevOpsWorkItemClient
    {
        /// <summary>
        /// Gets the current iteration path for a given team based on current date.
        /// Queries Azure DevOps classification nodes and finds the iteration where
        /// StartDate <= DateTime.UtcNow <= FinishDate.
        /// </summary>
        /// <param name="teamName">The name of the team to query iterations for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The iteration path (e.g., "OneCRM\\FY26\\Q3\\Month\\01 Jan (Dec 28 - Jan 31)"), or null if no current iteration is found.</returns>
        Task<string?> GetCurrentIterationPathAsync(string teamName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a work item by its ID.
        /// </summary>
        /// <param name="id">The work item ID.</param>
        /// <param name="fields">The list of field reference names to include in the response.</param>
        /// <returns>The JSON string representing the WorkItem object.</returns>
        Task<string> GetWorkItemAsync(int id, IEnumerable<string>? fields = null);

        /// <summary>
        /// Gets multiple work items by their IDs, returning only the specified fields.
        /// </summary>
        /// <param name="ids">The list of work item IDs.</param>
        /// <param name="fields">The list of field reference names to include in the response.</param>
        /// <returns>A JSON string representing a list of WorkItem objects.</returns>
        Task<string> GetWorkItemsAsync(IEnumerable<int> ids, IEnumerable<string>? fields = null);

        /// <summary>
        /// Gets work items under the specified area path, optionally filtered by work item type.
        /// </summary>
        /// <param name="areaPath">The area path to query under (e.g. 'Project\\Team\\Area').</param>
        /// <param name="workItemTypes">
        /// Optional collection of work item type names to filter on (e.g. 'Bug', 'User Story').
        /// If null or empty, all work item types are returned.
        /// </param>
        /// <param name="fields">The list of field reference names to include in the response.</param>
        /// <param name="pageNumber">1-based page number for paging through the result set.</param>
        /// <param name="pageSize">Maximum number of work items to return per page.</param>
        /// <returns>A JSON string representing a list of WorkItem objects.</returns>
        Task<string> GetWorkItemsByAreaPathAsync(string areaPath, IEnumerable<string>? workItemTypes = null, IEnumerable<string>? fields = null, int pageNumber = 1, int pageSize = 5);

        /// <summary>
        /// Retrieves the full parent hierarchy for the specified work item, walking up the parent chain.
        /// </summary>
        /// <param name="id">The work item ID to start from.</param>
        /// <param name="fields">
        /// Optional list of field reference names to include for each work item in the hierarchy
        /// (for example: 'System.Id', 'System.WorkItemType', 'System.Title', 'System.State').
        /// If null, default mandatory fields are included.
        /// </param>
        /// <returns>
        /// A JSON string representing the ordered hierarchy from the topmost parent down to the specified work item.
        /// </returns>
        Task<string> GetParentHierarchyAsync(int id, IEnumerable<string>? fields = null);

        /// <summary>
        /// Gets work items assigned to a specific user.
        /// </summary>
        /// <param name="userEmail">The email address of the assigned user.</param>
        /// <param name="states">Optional collection of work item states to filter on (e.g., 'Active', 'New'). If null, all states are included.</param>
        /// <param name="fields">The list of field reference names to include in the response.</param>
        /// <param name="iterationPath">Optional iteration path to filter work items (e.g., 'Project\\Sprint 1'). If null, all iterations are included.</param>
        /// <param name="areaPaths">Optional collection of area paths to filter work items (e.g., 'Project\\Team1', 'Project\\Team2'). If null or empty, all area paths are included.</param>
        /// <param name="workItemTypes">Optional collection of work item types to filter on (e.g., 'Bug', 'Task'). If null, all types are included.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A JSON string representing a list of WorkItem objects assigned to the user.</returns>
        Task<string> GetWorkItemsByAssignedUserAsync(
            string userEmail,
            IEnumerable<string>? states = null,
            IEnumerable<string>? fields = null,
            string? iterationPath = null,
            IEnumerable<string>? areaPaths = null,
            IEnumerable<string>? workItemTypes = null,
            CancellationToken cancellationToken = default);
    }
}
