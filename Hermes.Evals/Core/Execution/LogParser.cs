using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hermes.Evals.Core.Execution;

/// <summary>
/// Parses Hermes structured logs to extract tool invocation metadata.
/// Supports parsing console output and log files.
/// </summary>
public class LogParser
{
    private readonly ILogger<LogParser> _logger;

    // Regex patterns for structured log parsing
    private static readonly Regex SessionStartPattern = new(
        @"\[OrchestrationStart\]\s+SessionId=(?<sessionId>[^\s]+)\s+UserId=(?<userId>[^\s]+)\s+QueryLength=(?<queryLength>\d+)",
        RegexOptions.Compiled);

    private static readonly Regex ToolInvocationPattern = new(
        @"\[ToolInvocation\]\s+Tool=(?<tool>[^\s]+)\s+Operation=(?<operation>[^\s]+)\s+Input=(?<input>.*?)(?=\s+\[|$)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public LogParser(ILogger<LogParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses log lines to extract tool invocation metadata for a specific session.
    /// </summary>
    /// <param name="logLines">Collection of log lines from Hermes output.</param>
    /// <param name="sessionId">Session ID to filter log entries.</param>
    /// <returns>Dictionary containing extracted tool metadata.</returns>
    public Dictionary<string, object> ParseToolInvocations(IEnumerable<string> logLines, string sessionId)
    {
        var metadata = new Dictionary<string, object>();
        string? currentSessionId = null;
        string? lastTool = null;
        string? lastOperation = null;
        Dictionary<string, object>? lastParameters = null;

        foreach (var line in logLines)
        {
            // Check for session start (to track context)
            var sessionMatch = SessionStartPattern.Match(line);
            if (sessionMatch.Success)
            {
                var lineSessionId = sessionMatch.Groups["sessionId"].Value;
                if (lineSessionId == sessionId)
                {
                    currentSessionId = lineSessionId;
                    _logger.LogDebug("Found session start for {SessionId}", sessionId);
                }
                continue;
            }

            // Only process tool invocations for our target session
            if (currentSessionId == null)
            {
                continue;
            }

            // Parse tool invocation
            var toolMatch = ToolInvocationPattern.Match(line);
            if (toolMatch.Success)
            {
                lastTool = toolMatch.Groups["tool"].Value;
                lastOperation = toolMatch.Groups["operation"].Value;
                var inputJson = toolMatch.Groups["input"].Value;

                _logger.LogDebug(
                    "Parsed tool invocation: Tool={Tool}, Operation={Operation}, InputLength={InputLength}",
                    lastTool,
                    lastOperation,
                    inputJson.Length);

                // Parse input JSON to extract parameters
                try
                {
                    if (!string.IsNullOrWhiteSpace(inputJson) && inputJson.StartsWith("{"))
                    {
                        // Handle truncated input (ends with "...")
                        var cleanedInput = inputJson.TrimEnd('.', ' ');
                        if (!cleanedInput.EndsWith("}"))
                        {
                            // Input was truncated, we can't parse it fully
                            _logger.LogDebug("Tool input was truncated in logs, cannot extract full parameters");
                            lastParameters = new Dictionary<string, object>
                            {
                                ["_truncated"] = true,
                                ["_partialInput"] = cleanedInput
                            };
                        }
                        else
                        {
                            using var doc = JsonDocument.Parse(cleanedInput);
                            lastParameters = new Dictionary<string, object>();
                            foreach (var property in doc.RootElement.EnumerateObject())
                            {
                                lastParameters[property.Name] = property.Value.ValueKind switch
                                {
                                    JsonValueKind.Number => property.Value.GetInt32(),
                                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                                    JsonValueKind.True => true,
                                    JsonValueKind.False => false,
                                    _ => property.Value.ToString()
                                };
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse tool input JSON from logs");
                    lastParameters = null;
                }
            }
        }

        // Populate metadata with last captured tool invocation
        if (lastTool != null)
        {
            metadata["actualTool"] = lastTool;
        }

        if (lastOperation != null)
        {
            metadata["actualCapability"] = lastOperation;
        }

        if (lastParameters != null)
        {
            metadata["actualParameters"] = lastParameters;
        }

        _logger.LogInformation(
            "Log parsing complete for session {SessionId}: Tool={Tool}, Operation={Operation}, Parameters={ParamCount}",
            sessionId,
            lastTool ?? "none",
            lastOperation ?? "none",
            lastParameters?.Count ?? 0);

        return metadata;
    }

    /// <summary>
    /// Parses log content from a string (e.g., captured console output).
    /// </summary>
    public Dictionary<string, object> ParseLogContent(string logContent, string sessionId)
    {
        var lines = logContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return ParseToolInvocations(lines, sessionId);
    }

    /// <summary>
    /// Reads and parses a log file for tool invocations.
    /// </summary>
    public async Task<Dictionary<string, object>> ParseLogFileAsync(string logFilePath, string sessionId)
    {
        if (!File.Exists(logFilePath))
        {
            _logger.LogWarning("Log file not found: {LogFilePath}", logFilePath);
            return new Dictionary<string, object>();
        }

        // Open file with FileShare.ReadWrite to allow reading while Serilog is writing
        using var fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fileStream);

        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line != null)
            {
                lines.Add(line);
            }
        }

        return ParseToolInvocations(lines.ToArray(), sessionId);
    }
}
