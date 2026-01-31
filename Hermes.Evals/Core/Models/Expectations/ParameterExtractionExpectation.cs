namespace Hermes.Evals.Core.Models.Expectations;

/// <summary>
/// Defines expectations for parameter extraction.
/// </summary>
public class ParameterExtractionExpectation
{
    /// <summary>
    /// Expected parameters with their values (e.g., {"workItemId": 3097408}).
    /// </summary>
    public Dictionary<string, object> ExpectedParameters { get; set; } = new();

    /// <summary>
    /// List of parameter names that MUST be present (e.g., ["workItemId", "teamsUserId"]).
    /// </summary>
    public List<string> RequiredParameters { get; set; } = new();
}
