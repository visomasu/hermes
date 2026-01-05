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
		/// <returns>A JSON string representing a list of WorkItem objects.</returns>
		Task<string> GetWorkItemsByAreaPathAsync(string areaPath, IEnumerable<string>? workItemTypes = null, IEnumerable<string>? fields = null);

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
	}
}
