namespace Hermes.Evals.DataProviders;

/// <summary>
/// Interface for providing test data to evaluation scenarios.
/// Implementations can provide mock data (fast, deterministic) or real data (Azure DevOps, Microsoft Graph).
/// </summary>
public interface IDataProvider
{
    /// <summary>
    /// Gets a work item by ID.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <returns>JSON string representation of the work item.</returns>
    Task<string> GetWorkItemAsync(int workItemId);

    /// <summary>
    /// Gets the work item tree (hierarchy) starting from the specified work item.
    /// </summary>
    /// <param name="workItemId">The root work item ID.</param>
    /// <returns>JSON string representation of the work item tree.</returns>
    Task<string> GetWorkItemTreeAsync(int workItemId);

    /// <summary>
    /// Gets the parent hierarchy for a work item.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <returns>JSON string representation of the parent hierarchy.</returns>
    Task<string> GetParentHierarchyAsync(int workItemId);

    /// <summary>
    /// Gets user profile information.
    /// </summary>
    /// <param name="userId">The user ID (email address).</param>
    /// <returns>JSON string representation of the user profile.</returns>
    Task<string> GetUserProfileAsync(string userId);

    /// <summary>
    /// Initializes the data provider with scenario-specific mock data.
    /// </summary>
    /// <param name="mockData">Mock data configuration from the scenario.</param>
    void Initialize(Core.Models.MockData.MockData? mockData);
}
