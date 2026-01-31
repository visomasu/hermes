namespace Hermes.Evals.Core.Models.MockData;

/// <summary>
/// Represents a work item relation.
/// </summary>
public class MockWorkItemRelation
{
    public string Type { get; set; } = string.Empty; // "Child", "Parent", etc.
    public int Id { get; set; }
}
