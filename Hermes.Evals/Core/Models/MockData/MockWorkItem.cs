namespace Hermes.Evals.Core.Models.MockData;

/// <summary>
/// Represents a mock work item for testing.
/// </summary>
public class MockWorkItem
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public List<MockWorkItemRelation>? Relations { get; set; }
}
