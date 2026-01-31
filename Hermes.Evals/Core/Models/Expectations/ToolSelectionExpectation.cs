namespace Hermes.Evals.Core.Models.Expectations;

/// <summary>
/// Defines expectations for tool selection.
/// </summary>
public class ToolSelectionExpectation
{
    /// <summary>
    /// Expected tool name (e.g., "AzureDevOpsTool", "UserManagementTool").
    /// </summary>
    public string ExpectedTool { get; set; } = string.Empty;

    /// <summary>
    /// Expected capability name (e.g., "GetWorkItemTree", "RegisterSlaNotifications").
    /// </summary>
    public string ExpectedCapability { get; set; } = string.Empty;

    /// <summary>
    /// Allowed aliases that should also match (e.g., ["GetTree", "WorkItemTree"]).
    /// If the actual capability matches any alias, it's considered correct.
    /// </summary>
    public List<string>? AllowedAliases { get; set; }
}
