using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Hermes.Evals.Core.Evaluation;
using Hermes.Evals.Core.Execution;
using Hermes.Evals.Core.Reporting;
using Hermes.Evals.DataProviders;
using Hermes.Evals.Scenarios;
using Hermes.Evals.Core.Models.Metrics;
using Hermes.Evals.Core.Models.Results;
using Hermes.Evals.Core.Models.Scoring;
using Hermes.Evals.Core.Models.Scenario;

namespace Hermes.Evals;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    HERMES EVALUATION FRAMEWORK                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Parse CLI arguments
        string? scenarioFilter = null;
        if (args.Length > 0)
        {
            if (args[0] == "--help" || args[0] == "-h")
            {
                ShowHelp();
                return 0;
            }
            scenarioFilter = args[0];
        }

        // Setup DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        try
        {
            // Load scenarios
            var scenarioLoader = serviceProvider.GetRequiredService<ScenarioLoader>();

            Console.WriteLine("Loading scenarios from Scenarios/Definitions/...");
            var scenariosPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Scenarios", "Definitions");

            // Apply filter if specified
            var searchPattern = scenarioFilter != null ? $"*{scenarioFilter}*.yml" : "*.yml";
            var scenarioFiles = Directory.GetFiles(scenariosPath, searchPattern);

            if (scenarioFilter != null)
            {
                Console.WriteLine($"Filter: '{scenarioFilter}'");
            }
            Console.WriteLine($"Found {scenarioFiles.Length} scenario file(s)");
            Console.WriteLine();

            // Load all scenarios
            var scenarios = new List<EvaluationScenario>();
            foreach (var file in scenarioFiles)
            {
                var scenario = await scenarioLoader.LoadScenarioAsync(file);
                scenarios.Add(scenario);
                Console.WriteLine($"✓ Loaded: {scenario.Name}");
                Console.WriteLine($"  - Turns: {scenario.Turns.Count}");
                Console.WriteLine($"  - Mode: {scenario.ExecutionMode} / {scenario.DataMode}");
                Console.WriteLine($"  - Mock Work Items: {scenario.Setup.MockData?.WorkItems?.Count ?? 0}");
                Console.WriteLine();
            }

            // Run evaluations
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("RUNNING EVALUATIONS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();

            var engine = serviceProvider.GetRequiredService<IEvaluationEngine>();
            var results = await engine.RunScenariosAsync(scenarios);

            // Calculate metrics from real results
            var metrics = new EvaluationMetrics();
            metrics.CalculateFromScenarios(results.ToList());

            // Generate reports
            var outputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Output");
            Directory.CreateDirectory(outputDir);

            // Console Report
            var consoleReporter = serviceProvider.GetRequiredService<ConsoleReporter>();
            await consoleReporter.GenerateReportAsync(metrics, "");

            // JSON Report
            var jsonReporter = serviceProvider.GetRequiredService<JsonMetricsReporter>();
            var jsonPath = Path.Combine(outputDir, "evaluation-results.json");
            await jsonReporter.GenerateReportAsync(metrics, jsonPath);
            Console.WriteLine($"📄 JSON report saved: {jsonPath}");

            // Markdown Report
            var markdownReporter = serviceProvider.GetRequiredService<MarkdownReporter>();
            var mdPath = Path.Combine(outputDir, "evaluation-results.md");
            await markdownReporter.GenerateReportAsync(metrics, mdPath);
            Console.WriteLine($"📄 Markdown report saved: {mdPath}");
            Console.WriteLine();

            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"✅ Evaluation complete!");
            Console.WriteLine($"   Total scenarios: {results.Count()}");
            Console.WriteLine($"   Passed: {results.Count(r => r.Passed)}");
            Console.WriteLine($"   Failed: {results.Count(r => !r.Passed)}");
            Console.WriteLine($"   Overall score: {metrics.Summary.OverallScore:P2}");
            Console.WriteLine();

            return 0; // Success
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1; // Failure
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // HttpClient for REST API mode
        services.AddHttpClient("HermesApi", client =>
        {
            client.BaseAddress = new Uri("http://localhost:3978");
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        // Evaluators
        services.AddTransient<ToolSelectionEvaluator>();
        services.AddTransient<ParameterExtractionEvaluator>();
        services.AddTransient<ContextRetentionEvaluator>();
        services.AddTransient<ResponseQualityEvaluator>();

        // Evaluator Orchestrator
        services.AddSingleton<EvaluatorOrchestrator>(sp =>
        {
            var evaluators = new List<IEvaluator>
            {
                sp.GetRequiredService<ToolSelectionEvaluator>(),
                sp.GetRequiredService<ParameterExtractionEvaluator>(),
                sp.GetRequiredService<ContextRetentionEvaluator>(),
                sp.GetRequiredService<ResponseQualityEvaluator>()
            };

            return new EvaluatorOrchestrator(evaluators, new ScoringWeights());
        });

        // Log Parser for extracting tool metadata from Hermes logs
        services.AddSingleton<LogParser>();

        // Execution
        services.AddSingleton<ConversationRunner>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("HermesApi");
            var evaluatorOrchestrator = sp.GetRequiredService<EvaluatorOrchestrator>();
            var logger = sp.GetRequiredService<ILogger<ConversationRunner>>();

            // Configure log file path (default or from environment variable)
            var logFilePath = Environment.GetEnvironmentVariable("HERMES_LOG_PATH")
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".hermes",
                    "logs",
                    $"hermes-{DateTime.Now:yyyyMMdd}.log");

            return new ConversationRunner(httpClient, evaluatorOrchestrator, logger, logFilePath);
        });

        services.AddSingleton<IEvaluationEngine, EvaluationEngine>();

        // Data Providers
        services.AddSingleton<MockDataProvider>();

        // Scenario Loading
        services.AddSingleton<ScenarioLoader>();

        // Reporting
        services.AddSingleton<ConsoleReporter>();
        services.AddSingleton<JsonMetricsReporter>();
        services.AddSingleton<MarkdownReporter>();
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Usage: dotnet run [scenario-filter]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  scenario-filter    Optional filter to run specific scenarios (pattern match)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help        Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run                      # Run all scenarios");
        Console.WriteLine("  dotnet run sla                  # Run scenarios matching '*sla*.yml'");
        Console.WriteLine("  dotnet run newsletter           # Run scenarios matching '*newsletter*.yml'");
        Console.WriteLine("  dotnet run error-handling       # Run specific scenario");
        Console.WriteLine();
        Console.WriteLine("Available scenarios:");
        var scenariosPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Scenarios", "Definitions");
        if (Directory.Exists(scenariosPath))
        {
            var files = Directory.GetFiles(scenariosPath, "*.yml")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(f => f);
            foreach (var file in files)
            {
                Console.WriteLine($"  - {file}");
            }
        }
        Console.WriteLine();
    }
}
