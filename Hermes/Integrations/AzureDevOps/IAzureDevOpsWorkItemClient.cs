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
	}
}
