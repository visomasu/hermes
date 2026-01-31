using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Expectations;
using Hermes.Evals.Core.Models.Results;
using Hermes.Evals.Core.Models.Scoring;
using Hermes.Evals.Core.Execution;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hermes.Evals.Core.Evaluation;

/// <summary>
/// Evaluates whether context is retained and used correctly across conversation turns.
/// Weight: 25% (important for UX - users shouldn't repeat information).
/// This evaluator is STATEFUL - maintains context across turns within a scenario.
/// </summary>
public class ContextRetentionEvaluator : IEvaluator
{
    private readonly ILoggerFactory _loggerFactory;

    public string Name => "ContextRetention";
    public double Weight => 0.25;

    /// <summary>
    /// Conversation context storage (key-value pairs remembered from previous turns).
    /// This should be reset for each new scenario.
    /// </summary>
    private readonly Dictionary<string, object> _conversationContext = new();

    public ContextRetentionEvaluator(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

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

        // If no context retention expectations, skip evaluation
        var contextExpectation = expectation.ContextRetention;
        if (contextExpectation == null)
        {
            result.OverallScore = 1.0;
            result.Scores.ContextRetention = 1.0;
            return result;
        }

        int correctUsage = 0;
        int totalChecks = 0;

        // Step 1: Store context from this turn for future turns
        if (contextExpectation.ShouldRemember != null && contextExpectation.ShouldRemember.Count > 0)
        {
            foreach (var item in contextExpectation.ShouldRemember)
            {
                _conversationContext[item.Key] = item.Value;
            }

            result.AddCheck("ContextStored", true,
                $"Stored {contextExpectation.ShouldRemember.Count} context item(s): " +
                $"{string.Join(", ", contextExpectation.ShouldRemember.Select(i => i.Key))}");
        }

        // Step 2: Verify context usage from previous turns
        if (contextExpectation.VerifyContextUsage != null && contextExpectation.VerifyContextUsage.Count > 0)
        {
            totalChecks = contextExpectation.VerifyContextUsage.Count;

            // Parse logs to get actualParameters if not already in metadata
            var actualParams = capturedMetadata.GetValueOrDefault("actualParameters") as Dictionary<string, object>;

            if (actualParams == null && capturedMetadata.ContainsKey("logFilePath") && capturedMetadata.ContainsKey("sessionId"))
            {
                try
                {
                    var logFilePath = capturedMetadata["logFilePath"].ToString();
                    var actualSessionId = capturedMetadata["sessionId"].ToString();

                    if (!string.IsNullOrEmpty(logFilePath) && !string.IsNullOrEmpty(actualSessionId))
                    {
                        var logParser = new LogParser(_loggerFactory.CreateLogger<LogParser>());
                        var toolMetadata = await logParser.ParseLogFileAsync(logFilePath, actualSessionId);

                        if (toolMetadata.ContainsKey("actualParameters"))
                        {
                            actualParams = toolMetadata["actualParameters"] as Dictionary<string, object>;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.AddCheck("LogParsing", false, $"Failed to parse logs: {ex.Message}");
                }
            }

            foreach (var check in contextExpectation.VerifyContextUsage)
            {
                // Check if the context value was stored in a previous turn
                if (!_conversationContext.TryGetValue(check.ContextKey, out var contextValue))
                {
                    result.AddCheck($"ContextUsed_{check.ContextKey}", false,
                        $"Context key '{check.ContextKey}' was not stored from previous turn");
                    continue;
                }

                // Check if the context value was used in the expected parameter
                if (actualParams == null || actualParams.Count == 0)
                {
                    result.AddCheck($"ContextUsed_{check.ContextKey}", false,
                        "No parameters captured - cannot verify context usage");
                    continue;
                }

                if (!actualParams.TryGetValue(check.UsedInParameter, out var paramValue))
                {
                    result.AddCheck($"ContextUsed_{check.ContextKey}", false,
                        $"Parameter '{check.UsedInParameter}' not found in tool call");
                    continue;
                }

                // Compare context value with parameter value
                var matches = CompareValues(contextValue, paramValue);
                result.AddCheck($"ContextUsed_{check.ContextKey}", matches,
                    matches
                        ? $"Context '{check.ContextKey}'={FormatValue(contextValue)} correctly used in parameter '{check.UsedInParameter}'"
                        : $"Expected parameter '{check.UsedInParameter}'={FormatValue(contextValue)}, got {FormatValue(paramValue)}");

                if (matches) correctUsage++;
            }
        }

        // Calculate score
        if (totalChecks == 0)
        {
            // If only storing context (no verification), give full score
            result.OverallScore = 1.0;
            result.Success = true;
        }
        else
        {
            result.OverallScore = (double)correctUsage / totalChecks;
            result.Success = result.OverallScore >= 0.5; // At least 50% of context correctly used
        }

        result.Scores.ContextRetention = result.OverallScore;

        return result;
    }

    /// <summary>
    /// Resets the conversation context (call this when starting a new scenario).
    /// </summary>
    public void Reset()
    {
        _conversationContext.Clear();
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

        // Try numeric comparison
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
