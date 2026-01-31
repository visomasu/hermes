using Hermes.Evals.Core.Models.Scenario;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hermes.Evals.Scenarios;

/// <summary>
/// Loads evaluation scenarios from YAML or JSON definition files.
/// Supports wildcards, schema validation, and deserialization to EvaluationScenario models.
/// </summary>
public class ScenarioLoader
{
    private readonly ILogger<ScenarioLoader> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public ScenarioLoader(ILogger<ScenarioLoader> logger)
    {
        _logger = logger;

        // Configure YamlDotNet deserializer
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Loads a single scenario from a YAML or JSON file.
    /// </summary>
    /// <param name="filePath">Path to the scenario file.</param>
    /// <returns>Deserialized evaluation scenario.</returns>
    public async Task<EvaluationScenario> LoadScenarioAsync(string filePath)
    {
        _logger.LogInformation("Loading scenario from: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Scenario file not found: {filePath}");
        }

        var fileContent = await File.ReadAllTextAsync(filePath);
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        EvaluationScenario scenario;

        try
        {
            if (fileExtension == ".yml" || fileExtension == ".yaml")
            {
                scenario = _yamlDeserializer.Deserialize<EvaluationScenario>(fileContent);
            }
            else if (fileExtension == ".json")
            {
                scenario = System.Text.Json.JsonSerializer.Deserialize<EvaluationScenario>(fileContent)
                    ?? throw new InvalidOperationException("Deserialization returned null");
            }
            else
            {
                throw new NotSupportedException($"File extension '{fileExtension}' is not supported. Use .yml, .yaml, or .json");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize scenario from {FilePath}", filePath);
            throw new InvalidOperationException($"Failed to parse scenario file: {filePath}", ex);
        }

        // Validate scenario
        _ValidateScenario(scenario, filePath);

        _logger.LogInformation("Successfully loaded scenario: {ScenarioName} ({TurnCount} turns)",
            scenario.Name, scenario.Turns.Count);

        return scenario;
    }

    /// <summary>
    /// Loads multiple scenarios from files matching a pattern (supports wildcards).
    /// </summary>
    /// <param name="searchPattern">File search pattern (e.g., "sla-*.yml" or "*.yaml").</param>
    /// <param name="searchDirectory">Directory to search (defaults to Scenarios/Definitions/).</param>
    /// <returns>List of loaded scenarios.</returns>
    public async Task<List<EvaluationScenario>> LoadScenariosAsync(
        string searchPattern,
        string? searchDirectory = null)
    {
        // Default to Scenarios/Definitions/ relative to project root
        searchDirectory ??= Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Hermes.Evals", "Scenarios", "Definitions");

        searchDirectory = Path.GetFullPath(searchDirectory);

        _logger.LogInformation("Loading scenarios from: {Directory} (Pattern: {Pattern})",
            searchDirectory, searchPattern);

        if (!Directory.Exists(searchDirectory))
        {
            throw new DirectoryNotFoundException($"Scenario directory not found: {searchDirectory}");
        }

        var files = Directory.GetFiles(searchDirectory, searchPattern, SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
        {
            _logger.LogWarning("No scenario files found matching pattern: {Pattern} in {Directory}",
                searchPattern, searchDirectory);
            return new List<EvaluationScenario>();
        }

        _logger.LogInformation("Found {FileCount} scenario files matching pattern", files.Length);

        var scenarios = new List<EvaluationScenario>();

        foreach (var file in files)
        {
            try
            {
                var scenario = await LoadScenarioAsync(file);
                scenarios.Add(scenario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load scenario from file: {FilePath}", file);
                // Continue loading other scenarios
            }
        }

        _logger.LogInformation("Successfully loaded {ScenarioCount}/{FileCount} scenarios",
            scenarios.Count, files.Length);

        return scenarios;
    }

    /// <summary>
    /// Loads all scenarios from the default Scenarios/Definitions/ directory.
    /// </summary>
    /// <returns>List of all loaded scenarios.</returns>
    public async Task<List<EvaluationScenario>> LoadAllScenariosAsync()
    {
        return await LoadScenariosAsync("*.yml");
    }

    /// <summary>
    /// Validates a loaded scenario for correctness.
    /// </summary>
    private void _ValidateScenario(EvaluationScenario scenario, string filePath)
    {
        var errors = new List<string>();

        // Basic validations
        if (string.IsNullOrWhiteSpace(scenario.Name))
        {
            errors.Add("Scenario name is required");
        }

        if (scenario.Turns == null || scenario.Turns.Count == 0)
        {
            errors.Add("Scenario must have at least one turn");
        }

        // Validate turn numbers are sequential
        if (scenario.Turns != null)
        {
            for (int i = 0; i < scenario.Turns.Count; i++)
            {
                var turn = scenario.Turns[i];

                if (turn.TurnNumber != i + 1)
                {
                    errors.Add($"Turn numbers must be sequential. Expected turn {i + 1}, found {turn.TurnNumber}");
                }

                if (string.IsNullOrWhiteSpace(turn.Input))
                {
                    errors.Add($"Turn {turn.TurnNumber} has empty input");
                }
            }
        }

        // Validate scoring weights if provided
        if (scenario.Scoring != null && !scenario.Scoring.IsValid())
        {
            errors.Add("Scoring weights must sum to 1.0");
        }

        // Validate setup
        if (string.IsNullOrWhiteSpace(scenario.Setup.UserId))
        {
            errors.Add("Setup.UserId is required");
        }

        if (errors.Any())
        {
            var errorMessage = $"Scenario validation failed for {filePath}:\n  - {string.Join("\n  - ", errors)}";
            _logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
    }
}
