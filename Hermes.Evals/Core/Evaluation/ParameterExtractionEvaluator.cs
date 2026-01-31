using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Expectations;
using Hermes.Evals.Core.Models.Results;
using Hermes.Evals.Core.Models.Scoring;
using Hermes.Evals.Core.Execution;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hermes.Evals.Core.Evaluation;

/// <summary>
/// Evaluates whether parameters were correctly extracted from natural language input.
/// Weight: 30% (very critical - wrong parameters = wrong results).
/// </summary>
public class ParameterExtractionEvaluator : IEvaluator
{
    private readonly ILogger<ParameterExtractionEvaluator> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ParameterExtractionEvaluator(
        ILogger<ParameterExtractionEvaluator> logger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public string Name => "ParameterExtraction";
    public double Weight => 0.30;

    public async Task<TurnResult> EvaluateAsync(
        ConversationTurn turn,
        TurnExpectation expectation,
        Dictionary<string, object> capturedMetadata)
    {
        var result = new TurnResult
        {
            TurnNumber = turn.TurnNumber,
            EvaluatorName = Name
        };

        // If no parameter extraction expectations, skip evaluation
        var paramExpectation = expectation.ParameterExtraction;
        if (paramExpectation == null || paramExpectation.ExpectedParameters.Count == 0)
        {
            result.OverallScore = 1.0;
            result.Scores.ParameterExtraction = 1.0;
            return result;
        }

        // Extract parameters from logs or pre-parsed metadata
        var actualParams = await _ExtractParametersAsync(capturedMetadata);

        if (actualParams == null || actualParams.Count == 0)
        {
            result.AddCheck("ParametersExtracted", false, "No parameters captured from tool invocation");
            result.OverallScore = 0.0;
            result.Scores.ParameterExtraction = 0.0;
            result.Success = false;
            return result;
        }

        int correctParams = 0;
        int totalParams = paramExpectation.ExpectedParameters.Count;

        // Validate each expected parameter
        foreach (var (key, expectedValue) in paramExpectation.ExpectedParameters)
        {
            if (actualParams.TryGetValue(key, out var actualValue))
            {
                // Compare values (handle type conversions)
                var matches = CompareValues(expectedValue, actualValue);
                result.AddCheck($"Parameter_{key}", matches,
                    matches
                        ? $"Correct: {FormatValue(actualValue)}"
                        : $"Expected: {FormatValue(expectedValue)}, Actual: {FormatValue(actualValue)}");

                if (matches) correctParams++;
            }
            else
            {
                result.AddCheck($"Parameter_{key}", false, "Parameter missing from tool call");
            }
        }

        // Check for required parameters
        foreach (var requiredParam in paramExpectation.RequiredParameters)
        {
            var hasParam = actualParams.ContainsKey(requiredParam);
            if (!hasParam)
            {
                result.AddCheck($"Required_{requiredParam}", false, "Required parameter missing");
            }
        }

        // Calculate score: (correct parameters / total expected parameters)
        result.OverallScore = totalParams > 0 ? (double)correctParams / totalParams : 1.0;
        result.Scores.ParameterExtraction = result.OverallScore;
        result.Success = result.OverallScore >= 0.5; // At least 50% of parameters correct

        return result;
    }

    /// <summary>
    /// Extracts parameters from logs or pre-parsed metadata.
    /// </summary>
    private async Task<Dictionary<string, object>?> _ExtractParametersAsync(
        Dictionary<string, object> capturedMetadata)
    {
        // First check for pre-parsed metadata (HTTP headers fallback)
        var actualParams = capturedMetadata.GetValueOrDefault("actualParameters") as Dictionary<string, object>;

        if (actualParams != null)
        {
            _logger.LogDebug("Using pre-parsed parameters from headers: {ParamCount} parameters", actualParams.Count);
            return actualParams;
        }

        // Parse logs ourselves
        if (capturedMetadata.TryGetValue("logFilePath", out var logPathObj) &&
            capturedMetadata.TryGetValue("sessionId", out var sessionIdObj))
        {
            var logFilePath = logPathObj as string;
            var sessionId = sessionIdObj as string;

            if (!string.IsNullOrEmpty(logFilePath) && !string.IsNullOrEmpty(sessionId))
            {
                // Extract actual session ID (format: "userId|sessionId" or just "sessionId")
                var actualSessionId = sessionId;
                if (sessionId.Contains('|'))
                {
                    var parts = sessionId.Split('|', 2);
                    actualSessionId = parts[1];
                }

                _logger.LogDebug("Parsing logs for parameters: LogFile={LogFile}, SessionId={SessionId}",
                    logFilePath, actualSessionId);

                try
                {
                    var logParser = new LogParser(_loggerFactory.CreateLogger<LogParser>());
                    var toolMetadata = await logParser.ParseLogFileAsync(logFilePath, actualSessionId);

                    actualParams = toolMetadata.GetValueOrDefault("actualParameters") as Dictionary<string, object>;

                    if (actualParams != null)
                    {
                        _logger.LogDebug("Extracted {ParamCount} parameters from logs", actualParams.Count);
                    }
                    else
                    {
                        _logger.LogWarning("No parameters found in logs for session {SessionId}", actualSessionId);
                    }

                    return actualParams;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse logs for parameters: LogFile={LogFile}, SessionId={SessionId}",
                        logFilePath, actualSessionId);
                }
            }
        }

        _logger.LogWarning("Could not extract parameters: no pre-parsed data and no valid log file path/session ID");
        return null;
    }

    /// <summary>
    /// Compares two values, handling type conversions and JSON elements.
    /// </summary>
    private static bool CompareValues(object expected, object actual)
    {
        if (expected == null && actual == null) return true;
        if (expected == null || actual == null) return false;

        // Handle JsonElement from deserialization
        if (actual is JsonElement jsonElement)
        {
            actual = JsonElementToObject(jsonElement);
        }

        // Try exact match first
        if (expected.Equals(actual)) return true;

        // Try string comparison (case-insensitive)
        var expectedStr = expected.ToString();
        var actualStr = actual.ToString();

        if (string.Equals(expectedStr, actualStr, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Try numeric comparison (handle int vs long, etc.)
        if (IsNumeric(expected) && IsNumeric(actual))
        {
            try
            {
                var expectedDouble = Convert.ToDouble(expected);
                var actualDouble = Convert.ToDouble(actual);
                return Math.Abs(expectedDouble - actualDouble) < 0.0001;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts a JsonElement to a primitive object.
    /// </summary>
    private static object JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Checks if a value is numeric.
    /// </summary>
    private static bool IsNumeric(object value)
    {
        return value is int or long or float or double or decimal or short or byte;
    }

    /// <summary>
    /// Formats a value for display in check details.
    /// </summary>
    private static string FormatValue(object? value)
    {
        if (value == null) return "null";
        if (value is string str) return $"\"{str}\"";
        return value.ToString() ?? "null";
    }
}
