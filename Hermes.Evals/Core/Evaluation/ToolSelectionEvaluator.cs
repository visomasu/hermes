using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Expectations;
using Hermes.Evals.Core.Models.Results;
using Hermes.Evals.Core.Models.Scoring;
using Hermes.Evals.Core.Execution;
using Microsoft.Extensions.Logging;

namespace Hermes.Evals.Core.Evaluation;

/// <summary>
/// Evaluates whether the correct tool and capability were selected by the LLM.
/// Weight: 30% (most critical - wrong tool = total failure).
/// </summary>
public class ToolSelectionEvaluator : IEvaluator
{
    private readonly ILogger<ToolSelectionEvaluator> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ToolSelectionEvaluator(
        ILogger<ToolSelectionEvaluator> logger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public string Name => "ToolSelection";
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

        // If no tool selection expectations, skip evaluation
        var toolExpectation = expectation.ToolSelection;
        if (toolExpectation == null)
        {
            result.OverallScore = 1.0;
            result.Scores.ToolSelection = 1.0;
            return result;
        }

        // Extract tool metadata from logs or pre-parsed metadata
        var (actualTool, actualCapability) = await _ExtractToolMetadataAsync(capturedMetadata);

        if (actualTool == null || actualCapability == null)
        {
            result.AddCheck("ToolCalled", false, "No tool invocation captured");
            result.OverallScore = 0.0;
            result.Scores.ToolSelection = 0.0;
            result.Success = false;
            return result;
        }

        // Check 1: Verify correct tool was called
        var toolMatches = string.Equals(
            actualTool,
            toolExpectation.ExpectedTool,
            StringComparison.OrdinalIgnoreCase);

        result.AddCheck("CorrectToolSelected", toolMatches,
            $"Expected: {toolExpectation.ExpectedTool}, Actual: {actualTool}");

        // Check 2: Verify correct capability or alias
        var capabilityMatches = string.Equals(
            actualCapability,
            toolExpectation.ExpectedCapability,
            StringComparison.OrdinalIgnoreCase);

        var aliasMatches = toolExpectation.AllowedAliases?.Any(alias =>
            string.Equals(alias, actualCapability, StringComparison.OrdinalIgnoreCase)) ?? false;

        var capabilityCorrect = capabilityMatches || aliasMatches;

        result.AddCheck("CorrectCapabilitySelected", capabilityCorrect,
            $"Expected: {toolExpectation.ExpectedCapability}" +
            (toolExpectation.AllowedAliases?.Any() == true ? $" or aliases [{string.Join(", ", toolExpectation.AllowedAliases)}]" : "") +
            $", Actual: {actualCapability}");

        // Calculate score: 100% if both match, 50% if tool correct but capability wrong, 0% otherwise
        if (toolMatches && capabilityCorrect)
        {
            result.OverallScore = 1.0;
            result.Success = true;
        }
        else if (toolMatches)
        {
            result.OverallScore = 0.5;
            result.Success = false;
        }
        else
        {
            result.OverallScore = 0.0;
            result.Success = false;
        }

        result.Scores.ToolSelection = result.OverallScore;

        return result;
    }

    /// <summary>
    /// Extracts tool name and capability from logs or pre-parsed metadata.
    /// </summary>
    private async Task<(string? tool, string? capability)> _ExtractToolMetadataAsync(
        Dictionary<string, object> capturedMetadata)
    {
        // First check for pre-parsed metadata (HTTP headers fallback)
        var actualTool = capturedMetadata.GetValueOrDefault("actualTool") as string;
        var actualCapability = capturedMetadata.GetValueOrDefault("actualCapability") as string;

        if (actualTool != null && actualCapability != null)
        {
            _logger.LogDebug("Using pre-parsed tool metadata from headers: Tool={Tool}, Capability={Capability}",
                actualTool, actualCapability);
            return (actualTool, actualCapability);
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

                _logger.LogDebug("Parsing logs for tool metadata: LogFile={LogFile}, SessionId={SessionId}",
                    logFilePath, actualSessionId);

                try
                {
                    var logParser = new LogParser(_loggerFactory.CreateLogger<LogParser>());
                    var toolMetadata = await logParser.ParseLogFileAsync(logFilePath, actualSessionId);

                    actualTool = toolMetadata.GetValueOrDefault("actualTool") as string;
                    actualCapability = toolMetadata.GetValueOrDefault("actualCapability") as string;

                    if (actualTool != null || actualCapability != null)
                    {
                        _logger.LogDebug("Extracted tool metadata from logs: Tool={Tool}, Capability={Capability}",
                            actualTool ?? "null", actualCapability ?? "null");
                    }
                    else
                    {
                        _logger.LogWarning("No tool invocation found in logs for session {SessionId}", actualSessionId);
                    }

                    return (actualTool, actualCapability);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse logs for tool metadata: LogFile={LogFile}, SessionId={SessionId}",
                        logFilePath, actualSessionId);
                }
            }
        }

        _logger.LogWarning("Could not extract tool metadata: no pre-parsed data and no valid log file path/session ID");
        return (null, null);
    }
}
