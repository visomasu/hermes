namespace Hermes.Evals.Core.Models.MockData;

/// <summary>
/// Represents a mock user profile.
/// </summary>
public class MockUserProfile
{
    public string Email { get; set; } = string.Empty;
    public bool IsManager { get; set; }
    public int DirectReportCount { get; set; }
    public List<string>? DirectReportEmails { get; set; }
}
