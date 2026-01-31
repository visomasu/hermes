using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hermes.Evals.DataProviders;

/// <summary>
/// Provides mock data for fast, deterministic evaluation testing.
/// Data is configured via scenario YAML files and stored in memory.
/// </summary>
public class MockDataProvider : IDataProvider
{
    private readonly ILogger<MockDataProvider> _logger;
    private Dictionary<int, Core.Models.MockData.MockWorkItem> _workItems = new();
    private Core.Models.MockData.MockUserProfile? _userProfile;

    public MockDataProvider(ILogger<MockDataProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes the provider with mock data from the scenario configuration.
    /// </summary>
    public void Initialize(Core.Models.MockData.MockData? mockData)
    {
        if (mockData == null)
        {
            _logger.LogWarning("No mock data provided for initialization");
            return;
        }

        // Load work items into dictionary for quick lookup
        if (mockData.WorkItems != null)
        {
            _workItems = mockData.WorkItems.ToDictionary(wi => wi.Id);
            _logger.LogInformation("Initialized MockDataProvider with {WorkItemCount} work items", _workItems.Count);
        }

        // Load user profile
        if (mockData.UserProfile != null)
        {
            _userProfile = mockData.UserProfile;
            _logger.LogInformation("Initialized MockDataProvider with user profile: {Email}", _userProfile.Email);
        }
    }

    /// <summary>
    /// Gets a work item by ID.
    /// </summary>
    public Task<string> GetWorkItemAsync(int workItemId)
    {
        if (!_workItems.TryGetValue(workItemId, out var workItem))
        {
            _logger.LogWarning("Work item {WorkItemId} not found in mock data", workItemId);
            return Task.FromResult(JsonSerializer.Serialize(new { error = "Work item not found", id = workItemId }));
        }

        // Format as Hermes expects (simplified format)
        var result = new
        {
            id = workItem.Id,
            fields = new Dictionary<string, object>
            {
                ["System.WorkItemType"] = workItem.Type,
                ["System.Title"] = workItem.Title,
                ["System.State"] = workItem.State
            }
        };

        return Task.FromResult(JsonSerializer.Serialize(result));
    }

    /// <summary>
    /// Gets the work item tree (hierarchy) starting from the specified work item.
    /// </summary>
    public Task<string> GetWorkItemTreeAsync(int workItemId)
    {
        if (!_workItems.TryGetValue(workItemId, out var rootWorkItem))
        {
            _logger.LogWarning("Work item {WorkItemId} not found in mock data", workItemId);
            return Task.FromResult(JsonSerializer.Serialize(new { error = "Work item not found", id = workItemId }));
        }

        // Build tree recursively
        var tree = _BuildWorkItemNode(rootWorkItem);

        return Task.FromResult(JsonSerializer.Serialize(tree));
    }

    /// <summary>
    /// Gets the parent hierarchy for a work item.
    /// </summary>
    public Task<string> GetParentHierarchyAsync(int workItemId)
    {
        if (!_workItems.TryGetValue(workItemId, out var workItem))
        {
            _logger.LogWarning("Work item {WorkItemId} not found in mock data", workItemId);
            return Task.FromResult(JsonSerializer.Serialize(new { error = "Work item not found", id = workItemId }));
        }

        // Build parent hierarchy
        var hierarchy = new List<object>();
        _BuildParentHierarchy(workItem, hierarchy);

        return Task.FromResult(JsonSerializer.Serialize(hierarchy));
    }

    /// <summary>
    /// Gets user profile information.
    /// </summary>
    public Task<string> GetUserProfileAsync(string userId)
    {
        if (_userProfile == null || !string.Equals(_userProfile.Email, userId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("User profile for {UserId} not found in mock data", userId);
            return Task.FromResult(JsonSerializer.Serialize(new { error = "User not found", userId }));
        }

        var profile = new
        {
            email = _userProfile.Email,
            isManager = _userProfile.IsManager,
            directReportCount = _userProfile.DirectReportCount,
            directReports = _userProfile.DirectReportEmails ?? new List<string>()
        };

        return Task.FromResult(JsonSerializer.Serialize(profile));
    }

    /// <summary>
    /// Recursively builds a work item tree node with its children.
    /// </summary>
    private object _BuildWorkItemNode(Core.Models.MockData.MockWorkItem workItem)
    {
        var node = new Dictionary<string, object>
        {
            ["id"] = workItem.Id,
            ["type"] = workItem.Type,
            ["title"] = workItem.Title,
            ["state"] = workItem.State,
            ["children"] = new List<object>()
        };

        // Find child work items based on relations
        if (workItem.Relations != null)
        {
            var children = workItem.Relations
                .Where(r => r.Type.Equals("Child", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Id)
                .Where(childId => _workItems.ContainsKey(childId))
                .Select(childId => _BuildWorkItemNode(_workItems[childId]))
                .ToList();

            node["children"] = children;
        }

        return node;
    }

    /// <summary>
    /// Builds parent hierarchy by walking up the tree.
    /// </summary>
    private void _BuildParentHierarchy(Core.Models.MockData.MockWorkItem workItem, List<object> hierarchy)
    {
        // Add current work item to hierarchy
        hierarchy.Insert(0, new
        {
            id = workItem.Id,
            type = workItem.Type,
            title = workItem.Title,
            state = workItem.State
        });

        // Find parent
        if (workItem.Relations != null)
        {
            var parentRelation = workItem.Relations
                .FirstOrDefault(r => r.Type.Equals("Parent", StringComparison.OrdinalIgnoreCase));

            if (parentRelation != null && _workItems.TryGetValue(parentRelation.Id, out var parentWorkItem))
            {
                _BuildParentHierarchy(parentWorkItem, hierarchy);
            }
        }
    }
}
