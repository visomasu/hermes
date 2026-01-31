namespace Hermes.Evals.Core.Models.MockData;

/// <summary>
/// Contains mock data for deterministic testing.
/// </summary>
public class MockData
{
    /// <summary>
    /// Mock work items with their properties and relations.
    /// </summary>
    public List<MockWorkItem>? WorkItems { get; set; }

    /// <summary>
    /// Mock user profiles with manager status and direct reports.
    /// </summary>
    public MockUserProfile? UserProfile { get; set; }
}
