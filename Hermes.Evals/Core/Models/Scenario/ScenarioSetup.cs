using Hermes.Evals.Core.Models.MockData;

namespace Hermes.Evals.Core.Models.Scenario;

/// <summary>
/// Defines the setup and configuration for an evaluation scenario.
/// </summary>
public class ScenarioSetup
{
    /// <summary>
    /// User ID for the scenario (e.g., "testuser@microsoft.com").
    /// Used in sessionId format: "userId|sessionId".
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Session ID for conversation history tracking.
    /// Combined with UserId: "userId|sessionId".
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Mock data to use when DataMode is Mock.
    /// Contains work items, user profiles, etc.
    /// </summary>
    public MockData.MockData? MockData { get; set; }
}
