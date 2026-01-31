using System.Text.Json;
using Hermes.Evals.Core.Models.Metrics;
using Microsoft.Extensions.Logging;

namespace Hermes.Evals.Core.Reporting;

/// <summary>
/// Generates JSON metrics report for machine-readable output.
/// Used for CI/CD integration, baseline comparison, and automated analysis.
/// </summary>
public class JsonMetricsReporter : IReporter
{
    private readonly ILogger<JsonMetricsReporter> _logger;

    public JsonMetricsReporter(ILogger<JsonMetricsReporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a JSON metrics report.
    /// </summary>
    public async Task GenerateReportAsync(EvaluationMetrics metrics, string outputPath)
    {
        _logger.LogInformation("Generating JSON metrics report: {OutputPath}", outputPath);

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Serialize with pretty printing
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(metrics, options);
        await File.WriteAllTextAsync(outputPath, json);

        _logger.LogInformation("JSON metrics report saved: {OutputPath} ({Size} bytes)",
            outputPath, json.Length);
    }
}
