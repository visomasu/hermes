using Hermes.Evals.Core.Evaluation;
using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Results;
using Hermes.Evals.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hermes.Evals.Core.Execution;

/// <summary>
/// Executes a single conversation scenario turn-by-turn, capturing metadata and evaluating results.
/// Supports both RestApi (HTTP) and DirectOrchestrator (in-process) execution modes.
/// </summary>
public class ConversationRunner
{
    private readonly HttpClient _httpClient;
    private readonly EvaluatorOrchestrator _evaluatorOrchestrator;
    private readonly ILogger<ConversationRunner> _logger;
    private readonly string _logFilePath;

    // Session state for multi-turn conversations
    private string? _sessionId;
    private string? _userId;

    public ConversationRunner(
        HttpClient httpClient,
        EvaluatorOrchestrator evaluatorOrchestrator,
        ILogger<ConversationRunner> logger,
        string? logFilePath = null)
    {
        _httpClient = httpClient;
        _evaluatorOrchestrator = evaluatorOrchestrator;
        _logger = logger;

        // Default log file path if not provided
        _logFilePath = logFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hermes",
            "logs",
            $"hermes-{DateTime.Now:yyyyMMdd}.log");
    }

    /// <summary>
    /// Executes a complete evaluation scenario (all turns) and returns the overall result.
    /// </summary>
    /// <param name="scenario">The scenario to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Evaluation result for the entire scenario.</returns>
    public async Task<EvaluationResult> RunScenarioAsync(
        EvaluationScenario scenario,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting scenario: {ScenarioName} ({TurnCount} turns, Mode: {ExecutionMode}, Data: {DataMode})",
            scenario.Name, scenario.Turns.Count, scenario.ExecutionMode, scenario.DataMode);

        // Initialize result
        var result = new EvaluationResult
        {
            ScenarioName = scenario.Name,
            ExecutionMode = scenario.ExecutionMode,
            DataMode = scenario.DataMode
        };

        // Initialize session state
        _sessionId = Guid.NewGuid().ToString();
        _userId = scenario.Setup.UserId;

        // Reset evaluator state for new scenario
        _evaluatorOrchestrator.ResetForNewScenario();

        var scenarioStopwatch = Stopwatch.StartNew();

        // Execute each turn sequentially
        foreach (var turn in scenario.Turns)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Scenario {ScenarioName} cancelled at turn {TurnNumber}", scenario.Name, turn.TurnNumber);
                break;
            }

            _logger.LogInformation("Executing turn {TurnNumber}/{TotalTurns}: {Input}",
                turn.TurnNumber, scenario.Turns.Count, turn.Input.Substring(0, Math.Min(50, turn.Input.Length)));

            var turnStopwatch = Stopwatch.StartNew();

            try
            {
                // Execute turn based on execution mode
                TurnResult turnResult = scenario.ExecutionMode switch
                {
                    ExecutionMode.RestApi => await _ExecuteTurnViaRestApiAsync(scenario, turn, cancellationToken),
                    ExecutionMode.DirectOrchestrator => await _ExecuteTurnViaDirectOrchestratorAsync(scenario, turn, cancellationToken),
                    _ => throw new NotSupportedException($"Execution mode {scenario.ExecutionMode} is not supported")
                };

                turnStopwatch.Stop();
                turnResult.ExecutionTimeMs = turnStopwatch.ElapsedMilliseconds;

                result.TurnResults.Add(turnResult);

                _logger.LogInformation("Turn {TurnNumber} completed: Success={Success}, Score={Score:F2}, Time={TimeMs}ms",
                    turn.TurnNumber, turnResult.Success, turnResult.OverallScore, turnResult.ExecutionTimeMs);

                // Stop on failure if configured
                if (!turnResult.Success && scenario.StopOnFailure)
                {
                    _logger.LogWarning("Stopping scenario {ScenarioName} due to turn failure (StopOnFailure=true)", scenario.Name);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing turn {TurnNumber} in scenario {ScenarioName}", turn.TurnNumber, scenario.Name);

                // Create failed turn result
                var failedTurnResult = new TurnResult
                {
                    TurnNumber = turn.TurnNumber,
                    EvaluatorName = "ConversationRunner",
                    Success = false,
                    OverallScore = 0.0,
                    ExecutionTimeMs = turnStopwatch.ElapsedMilliseconds,
                    CapturedMetadata = new Dictionary<string, object> { ["error"] = ex.Message }
                };
                failedTurnResult.AddCheck("ExecutionSuccess", false, $"Exception: {ex.Message}");

                result.TurnResults.Add(failedTurnResult);

                if (scenario.StopOnFailure)
                {
                    break;
                }
            }
        }

        scenarioStopwatch.Stop();
        result.ExecutionTimeMs = scenarioStopwatch.ElapsedMilliseconds;

        // Calculate overall metrics
        result.CalculateOverallMetrics();

        _logger.LogInformation("Scenario {ScenarioName} completed: Passed={Passed}, Score={Score:F2}, Time={TimeMs}ms",
            scenario.Name, result.Passed, result.OverallScore, result.ExecutionTimeMs);

        return result;
    }

    /// <summary>
    /// Executes a single turn via REST API (HTTP call to localhost:3978).
    /// </summary>
    private async Task<TurnResult> _ExecuteTurnViaRestApiAsync(
        EvaluationScenario scenario,
        ConversationTurn turn,
        CancellationToken cancellationToken)
    {
        // Prepare request payload
        var requestPayload = new
        {
            text = turn.Input,
            userId = _userId,
            sessionId = _sessionId
        };

        _logger.LogDebug("Sending REST API request: POST /api/hermes/v1.0/chat (SessionId: {SessionId})", _sessionId);

        // Make HTTP request
        var jsonContent = JsonContent.Create(requestPayload);

        // Add required correlation ID header
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/hermes/v1.0/chat")
        {
            Content = jsonContent
        };
        request.Headers.Add("x-ms-correlation-id", _sessionId ?? Guid.NewGuid().ToString());

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("Received response: {ResponseLength} characters", responseContent.Length);

        // Parse response to extract metadata
        var capturedMetadata = await _ExtractMetadataFromRestApiResponseAsync(responseContent, response);

        // Evaluate turn
        var turnResult = await _evaluatorOrchestrator.EvaluateAsync(turn, turn.Expectations, capturedMetadata);

        return turnResult;
    }

    /// <summary>
    /// Executes a single turn via direct orchestrator invocation (in-process).
    /// NOTE: This requires TestHermesOrchestrator wrapper for instrumentation (Phase 7).
    /// </summary>
    private Task<TurnResult> _ExecuteTurnViaDirectOrchestratorAsync(
        EvaluationScenario scenario,
        ConversationTurn turn,
        CancellationToken cancellationToken)
    {
        // TODO: Implement direct orchestrator execution in Phase 7 (Infrastructure)
        // This will require TestHermesOrchestrator wrapper to capture tool invocations
        throw new NotImplementedException(
            "DirectOrchestrator execution mode is not yet implemented. " +
            "This will be added in Phase 7 (Infrastructure) with TestHermesOrchestrator instrumentation.");
    }

    /// <summary>
    /// Extracts metadata from REST API response for evaluation.
    /// Passes raw data (response text, log file path, session ID) to evaluators.
    /// Evaluators are responsible for parsing logs if needed.
    /// </summary>
    private Task<Dictionary<string, object>> _ExtractMetadataFromRestApiResponseAsync(
        string responseContent,
        HttpResponseMessage httpResponse)
    {
        var metadata = new Dictionary<string, object>
        {
            ["responseText"] = responseContent,
            ["logFilePath"] = _logFilePath,
            ["sessionId"] = _sessionId ?? "session-1"
        };

        // Optional: Check for custom headers (legacy support for pre-parsed metadata)
        // Evaluators can check for these as a fallback if log parsing fails
        if (httpResponse.Headers.TryGetValues("X-Tool-Name", out var toolNames))
        {
            metadata["actualTool"] = toolNames.FirstOrDefault() ?? string.Empty;
        }

        if (httpResponse.Headers.TryGetValues("X-Tool-Operation", out var operations))
        {
            metadata["actualCapability"] = operations.FirstOrDefault() ?? string.Empty;
        }

        if (httpResponse.Headers.TryGetValues("X-Tool-Parameters", out var parameters))
        {
            try
            {
                var paramsJson = parameters.FirstOrDefault();
                if (!string.IsNullOrEmpty(paramsJson))
                {
                    using var doc = JsonDocument.Parse(paramsJson);
                    var paramsDict = new Dictionary<string, object>();
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        paramsDict[property.Name] = property.Value.ValueKind switch
                        {
                            JsonValueKind.Number => property.Value.GetInt32(),
                            JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => property.Value.ToString()
                        };
                    }
                    metadata["actualParameters"] = paramsDict;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse tool parameters from header");
            }
        }

        _logger.LogDebug("Captured metadata for evaluation: ResponseLength={ResponseLength}, LogPath={LogPath}, SessionId={SessionId}",
            responseContent.Length, _logFilePath, metadata["sessionId"]);

        return Task.FromResult(metadata);
    }
}
