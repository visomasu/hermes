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
    // Updated to handle timestamp and log level prefix: [20:57:11 INF]
    private static readonly Regex SessionStartPattern = new(
        @"\[OrchestrationStart\]\s+SessionId=(?<sessionId>[^\s]+)\s+UserId=(?<userId>[^\s]+)\s+QueryLength=(?<queryLength>\d+)",
        RegexOptions.Compiled);

    private static readonly Regex ToolInvocationPattern = new(
        @"\[ToolInvocation\]\s+SessionId=(?<sessionId>[^\s]+)\s+Tool=(?<tool>[^\s]+)\s+Operation=(?<operation>[^\s]+)\s+Input=(?<input>.*?)$",
        RegexOptions.Compiled);

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
        string? lastTool = null;
        string? lastOperation = null;
        Dictionary<string, object>? lastParameters = null;

        var lineCount = 0;
        var toolInvocationMatchCount = 0;
        var orchestrationStartCount = 0;
        var lastOrchestrationStartIndex = -1;

        // Convert to list for indexed access
        var lines = logLines.ToList();

        // Find the LAST OrchestrationStart for our target sessionId
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var sessionMatch = SessionStartPattern.Match(lines[i]);
            if (sessionMatch.Success && sessionMatch.Groups["sessionId"].Value == sessionId)
            {
                lastOrchestrationStartIndex = i;
                _logger.LogDebug("Found last OrchestrationStart for session {SessionId} at line {LineIndex}", sessionId, i);
                break;
            }
        }

        if (lastOrchestrationStartIndex == -1)
        {
            _logger.LogWarning("No OrchestrationStart found for session {SessionId} (total lines: {TotalLines})", sessionId, lines.Count);
            return metadata;
        }

        // Find the next OrchestrationStart (for any session) after our target start
        var nextOrchestrationStartIndex = lines.Count;
        for (int i = lastOrchestrationStartIndex + 1; i < lines.Count; i++)
        {
            var sessionMatch = SessionStartPattern.Match(lines[i]);
            if (sessionMatch.Success)
            {
                nextOrchestrationStartIndex = i;
                _logger.LogDebug("Found next OrchestrationStart at line {LineIndex}", i);
                break;
            }
        }

        _logger.LogInformation("Parsing window for session {SessionId}: lines {StartIndex} to {EndIndex} (total {TotalLines})",
            sessionId, lastOrchestrationStartIndex, nextOrchestrationStartIndex, lines.Count);

        // Now parse tool invocations only within this window
        for (int i = lastOrchestrationStartIndex; i < nextOrchestrationStartIndex; i++)
        {
            var line = lines[i];
            lineCount++;

            // Parse tool invocation
            var toolMatch = ToolInvocationPattern.Match(line);
            if (toolMatch.Success)
            {
                toolInvocationMatchCount++;
                var toolSessionId = toolMatch.Groups["sessionId"].Value;

                _logger.LogDebug(
                    "Found ToolInvocation: LineSessionId={LineSessionId}, TargetSessionId={TargetSessionId}, Match={Match}",
                    toolSessionId,
                    sessionId,
                    toolSessionId == sessionId);

                // Only process tool invocations that match our target session
                if (toolSessionId != sessionId)
                {
                    continue;
                }

                lastTool = toolMatch.Groups["tool"].Value;
                lastOperation = toolMatch.Groups["operation"].Value;
                var inputJson = toolMatch.Groups["input"].Value;

                _logger.LogDebug(
                    "Parsed tool invocation for session {SessionId}: Tool={Tool}, Operation={Operation}, InputLength={InputLength}",
                    sessionId,
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
            "Log parsing complete for session {SessionId}: LinesProcessed={LineCount}, ToolInvocationsFound={MatchCount}, Tool={Tool}, Operation={Operation}, Parameters={ParamCount}",
            sessionId,
            lineCount,
            toolInvocationMatchCount,
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
        // Try to find the session in the specified log file first
        var metadata = await _TryParseLogFileAsync(logFilePath, sessionId);

        // If session not found, try all log files from today (in case of rolling logs)
        if (!metadata.ContainsKey("actualTool"))
        {
            _logger.LogWarning("Session {SessionId} not found in {LogFilePath}, searching all today's log files", sessionId, logFilePath);

            var logDir = Path.GetDirectoryName(logFilePath);
            if (logDir != null && Directory.Exists(logDir))
            {
                var today = DateTime.Now.ToString("yyyyMMdd");
                var todaysLogFiles = Directory.GetFiles(logDir, $"hermes-{today}*.log")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToList();

                foreach (var logFile in todaysLogFiles)
                {
                    if (logFile == logFilePath) continue; // Already tried this one

                    _logger.LogInformation("Trying log file: {LogFile}", logFile);
                    metadata = await _TryParseLogFileAsync(logFile, sessionId);

                    if (metadata.ContainsKey("actualTool"))
                    {
                        _logger.LogInformation("Found session {SessionId} in {LogFile}", sessionId, logFile);
                        break;
                    }
                }
            }
        }

        return metadata;
    }

    private async Task<Dictionary<string, object>> _TryParseLogFileAsync(string logFilePath, string sessionId)
    {
        if (!File.Exists(logFilePath))
        {
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
