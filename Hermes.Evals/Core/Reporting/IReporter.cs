using Hermes.Evals.Core.Models.Metrics;

namespace Hermes.Evals.Core.Reporting;

/// <summary>
/// Interface for generating evaluation reports in various formats.
/// </summary>
public interface IReporter
{
    /// <summary>
    /// Generates a report from evaluation metrics.
    /// </summary>
    /// <param name="metrics">Aggregated evaluation metrics.</param>
    /// <param name="outputPath">Path where the report should be saved.</param>
    Task GenerateReportAsync(EvaluationMetrics metrics, string outputPath);
}
