namespace Hermes.Evals.Core.Models.Enums;

/// <summary>
/// Defines which data source to use for evaluations.
/// </summary>
public enum DataMode
{
    /// <summary>
    /// Use in-memory mock data. Fast, deterministic, no external dependencies.
    /// Ideal for CI/CD pipelines and rapid iteration.
    /// </summary>
    Mock,

    /// <summary>
    /// Use live Azure DevOps and Microsoft Graph APIs.
    /// More realistic, tests actual integrations, but slower and non-deterministic.
    /// </summary>
    Real
}
