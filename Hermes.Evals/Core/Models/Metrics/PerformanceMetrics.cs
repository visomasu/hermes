namespace Hermes.Evals.Core.Models.Metrics;

/// <summary>
/// Performance metrics for evaluation execution.
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Average execution time per turn in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// 95th percentile execution time in milliseconds.
    /// </summary>
    public long P95ExecutionTimeMs { get; set; }

    /// <summary>
    /// 99th percentile execution time in milliseconds.
    /// </summary>
    public long P99ExecutionTimeMs { get; set; }
}
