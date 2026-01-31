# Hermes.Evals - Test Harness & Evaluation Framework

**Hermes.Evals** is a comprehensive evaluation framework for testing Hermes AI agent through multi-turn conversations. It measures tool selection accuracy, parameter extraction, context retention, and response quality using declarative YAML scenarios.

## Features

- 🎯 **4 Weighted Evaluators**: Tool Selection (30%), Parameter Extraction (30%), Context Retention (25%), Response Quality (15%)
- 📝 **YAML-Based Scenarios**: Declarative test definitions with clear expectations
- 🔄 **Multi-Turn Testing**: Evaluate context retention across conversation turns
- 📊 **Triple Report Output**: JSON (CI/CD), Markdown (human-readable), Console (real-time)
- 🎭 **Mock & Real Data**: Fast deterministic tests (Mock) or live API validation (Real)
- ⚡ **Two Execution Modes**: REST API or Direct Orchestrator calls
- 📈 **Automated Recommendations**: Smart suggestions based on metric thresholds

## Quick Start

### Run the Demo

```bash
cd Hermes.Evals
dotnet run --configuration Release
```

This demonstrates:
- ✅ Loading 2 sample YAML scenarios
- ✅ Generating evaluation metrics
- ✅ Creating JSON, Markdown, and Console reports

### Run Real Evaluations

1. **Start Hermes API**:
   ```bash
   cd ../Hermes
   dotnet run
   ```

2. **Run evaluations** (when REST API mode is fully wired):
   ```bash
   cd ../Hermes.Evals
   dotnet run -- run --scenarios "Scenarios/Definitions/*.yml"
   ```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  YAML Scenario Definition                   │
│  (Turns + Expectations + Mock Data)                         │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│               EvaluationEngine                              │
│  • Loads scenarios via ScenarioLoader                       │
│  • Orchestrates execution via ConversationRunner            │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│            ConversationRunner                               │
│  • Executes turns via REST API or Direct calls             │
│  • Captures metadata (tools, params, responses)             │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│          EvaluatorOrchestrator                              │
│  • Runs 4 evaluators in parallel                            │
│  • Aggregates weighted scores                               │
└──┬─────────┬──────────┬──────────┬──────────────────────────┘
   │         │          │          │
   ▼         ▼          ▼          ▼
┌──────┐ ┌──────┐ ┌──────┐ ┌──────────┐
│Tool  │ │Params│ │Contxt│ │ Response │
│(30%) │ │(30%) │ │(25%) │ │ (15%)    │
└──────┘ └──────┘ └──────┘ └──────────┘
   │         │          │          │
   └─────────┴──────────┴──────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────┐
│                  Reporters                                  │
│  • JSON (CI/CD integration)                                 │
│  • Markdown (human analysis)                                │
│  • Console (real-time feedback)                             │
└─────────────────────────────────────────────────────────────┘
```

## Tool Call Verification

Hermes.Evals verifies that Hermes invokes the correct tools and extracts parameters accurately. This is critical for the **Tool Selection (30%)** and **Parameter Extraction (30%)** evaluators.

### How It Works

**Structured Logging Approach:**

1. **Hermes logs structured data** with session IDs and tool invocations:
   ```
   [OrchestrationStart] SessionId=session-1 UserId=user@microsoft.com QueryLength=45
   [ToolInvocation] Tool=AzureDevOpsTool Operation=GetWorkItemTree Input={"workItemId":123456}
   [ToolResult] Tool=AzureDevOpsTool Operation=GetWorkItemTree Success=true
   ```

2. **LogParser extracts metadata** from console output or log files:
   - Filters log entries by session ID
   - Parses structured log format using regex
   - Extracts: tool name, capability/operation, parameters (JSON)

3. **Evaluators verify accuracy**:
   - ToolSelectionEvaluator: Compares `actualTool` and `actualCapability` against expectations
   - ParameterExtractionEvaluator: Validates `actualParameters` match expected values

### Setup for REST API Mode

**Option 1: Custom Response Headers (Quick Demo)**
- Add custom headers to Hermes API responses: `X-Tool-Name`, `X-Tool-Operation`, `X-Tool-Parameters`
- ConversationRunner extracts metadata from headers
- **Pros:** No file I/O, immediate availability
- **Cons:** Requires Hermes code changes

**Option 2: Log File Parsing (Production)**
- Configure Hermes to log to a known file location
- ConversationRunner uses LogParser to read log file after API calls
- **Pros:** No API changes, works with existing logs
- **Cons:** Requires file access, potential race conditions

**Current Implementation:**
- Supports both header-based and log-based extraction
- Falls back gracefully if metadata unavailable
- See `Core/Execution/LogParser.cs` for implementation

### Logging Requirements

For tool call verification to work, Hermes must log:
- ✅ Session ID (`SessionId={id}`)
- ✅ Tool name (`Tool={name}`)
- ✅ Operation/Capability (`Operation={capability}`)
- ✅ Input parameters (`Input={json}`)

These are now logged automatically in `HermesOrchestrator.InitializeAgentTools()`.

## Directory Structure

```
Hermes.Evals/
├── Core/
│   ├── Models/                    # Data models (25 files, 7 folders)
│   │   ├── Enums/                 # ExecutionMode, DataMode
│   │   ├── Expectations/          # Turn expectations per dimension
│   │   ├── Metrics/               # Aggregated metrics
│   │   ├── MockData/              # Test data models
│   │   ├── Results/               # Turn & scenario results
│   │   ├── Scenario/              # Scenario definitions
│   │   └── Scoring/               # Weighted scoring
│   ├── Evaluation/                # 4 evaluators + orchestrator
│   ├── Execution/                 # Scenario execution engine
│   └── Reporting/                 # JSON, Markdown, Console reporters
├── DataProviders/                 # Mock & Real data providers
├── Scenarios/
│   ├── ScenarioLoader.cs          # YAML/JSON loader
│   └── Definitions/               # YAML scenario files
├── Output/                        # Generated reports
└── Program.cs                     # Application entry point
```

## Writing Scenarios

### Basic Scenario Structure

Create a YAML file in `Scenarios/Definitions/`:

```yaml
name: "Newsletter Generation Test"
description: "Tests newsletter generation for a feature"
executionMode: "RestApi"  # or "DirectOrchestrator"
dataMode: "Mock"          # or "Real"

setup:
  userId: "testuser@microsoft.com"
  mockData:
    workItems:
      - id: 123456
        title: "Test Feature"
        type: "Feature"
        state: "Active"

scoring:
  toolSelection: 0.30
  parameterExtraction: 0.30
  contextRetention: 0.25
  responseQuality: 0.15

stopOnFailure: false

turns:
  - turnNumber: 1
    input: "generate a newsletter for feature 123456"
    expectations:
      toolSelection:
        expectedTool: "AzureDevOpsTool"
        expectedCapability: "GenerateNewsletter"
        allowedAliases: ["GetWorkItemTree", "WorkItemTree"]

      parameterExtraction:
        expectedParameters:
          workItemId: 123456
        requiredParameters: ["workItemId"]

      responseQuality:
        mustContain: ["123456", "Feature"]
        minLength: 100
```

### Multi-Turn with Context Retention

```yaml
turns:
  # Turn 1: Store context
  - turnNumber: 1
    input: "generate a newsletter for feature 123456"
    expectations:
      contextRetention:
        shouldRemember:
          - key: "lastFeatureId"
            value: "123456"

  # Turn 2: Verify context usage
  - turnNumber: 2
    input: "now validate the hierarchy for that feature"
    expectations:
      contextRetention:
        verifyContextUsage:
          - contextKey: "lastFeatureId"
            usedInParameter: "workItemId"
```

## Evaluation Dimensions

### 1. Tool Selection (30% weight)

**What it measures:** Did the LLM select the correct tool and capability?

**Scoring:**
- 100% - Both tool and capability correct
- 50% - Tool correct, capability wrong
- 0% - Wrong tool

**Example:**
```yaml
toolSelection:
  expectedTool: "AzureDevOpsTool"
  expectedCapability: "GetWorkItemTree"
  allowedAliases: ["GetTree", "WorkItemTree"]
```

### 2. Parameter Extraction (30% weight)

**What it measures:** Were all parameters correctly extracted from natural language?

**Scoring:** `(correct parameters / total expected parameters)`

**Example:**
```yaml
parameterExtraction:
  expectedParameters:
    workItemId: 123456
    includeChildren: true
  requiredParameters: ["workItemId"]
```

### 3. Context Retention (25% weight)

**What it measures:** Does the LLM remember information from previous turns?

**Scoring:** `(correct context usage / expected context usage)`

**Stateful:** This evaluator maintains context across turns within a scenario.

**Example:**
```yaml
contextRetention:
  shouldRemember:
    - key: "lastFeatureId"
      value: "123456"
  verifyContextUsage:
    - contextKey: "lastFeatureId"
      usedInParameter: "workItemId"
```

### 4. Response Quality (15% weight)

**What it measures:** Is the response complete, well-formatted, and contains expected content?

**Scoring:** `(passed checks / total checks)`

**Example:**
```yaml
responseQuality:
  mustContain: ["Feature #123456", "Progress"]
  mustNotContain: ["error", "failed"]
  minLength: 200
  structure: ["Summary", "Key Updates"]
```

## Report Formats

### JSON Metrics (`evaluation-results.json`)

Machine-readable format for CI/CD pipelines and baseline comparison.

```json
{
  "summary": {
    "totalScenarios": 2,
    "passedScenarios": 2,
    "successRate": 1.0,
    "overallScore": 0.915
  },
  "metrics": {
    "toolSelectionAccuracy": 1.0,
    "parameterExtractionAccuracy": 1.0,
    "contextRetentionScore": 0.875,
    "responseQualityScore": 0.812
  },
  "performance": {
    "averageExecutionTimeMs": 1230,
    "p95ExecutionTimeMs": 1256,
    "p99ExecutionTimeMs": 1256
  }
}
```

### Markdown Report (`evaluation-results.md`)

Human-readable analysis with:
- Executive summary with pass/fail status
- Metrics table with targets and grades
- Performance comparison against targets
- Detailed scenario results with dimension breakdowns
- Automated recommendations

### Console Output

Real-time xUnit-style progress with color-coded results:

```
═══════════════════════════════════════════════════════════════
  HERMES EVALUATION RESULTS
═══════════════════════════════════════════════════════════════

SUMMARY
───────────────────────────────────────────────────────────────
  Total Scenarios:    2
  Passed:             2 (100.0%)
  Overall Score:      0.915 (Good)

[PASS] Simple Tool Selection Test
       Score: 0.950 | Turns: 1 | Time: 1234ms

✓ ALL TESTS PASSED
```

## Target Thresholds

The framework evaluates metrics against these targets:

| Metric | Target | Grade if Met |
|--------|--------|--------------|
| Tool Selection Accuracy | ≥95% | Excellent ✅ |
| Parameter Extraction | ≥98% | Excellent ✅ |
| Context Retention | ≥80% | Good ✅ |
| Response Quality | ≥75% | Fair ✅ |
| Overall Score | ≥85% | Passing ✅ |
| Avg Execution Time | <1500ms | Good ✅ |
| P95 Execution Time | <2500ms | Good ✅ |

## Development

### Building

```bash
dotnet build Hermes.Evals/Hermes.Evals.csproj --configuration Release
```

### Running Tests

```bash
cd Hermes.Evals
dotnet run --configuration Release
```

### Adding a New Evaluator

1. Create evaluator class implementing `IEvaluator` in `Core/Evaluation/`
2. Register in `Program.cs` DI configuration
3. Add to `EvaluatorOrchestrator` evaluators list
4. Update `ScoringWeights` if changing weight distribution

### Adding a New Reporter

1. Create reporter class implementing `IReporter` in `Core/Reporting/`
2. Register in `Program.cs` DI configuration
3. Call `GenerateReportAsync()` in Program.cs

## Example Scenarios

### Simple Tool Selection
```yaml
name: "Simple Tool Selection Test"
turns:
  - turnNumber: 1
    input: "generate a newsletter for feature 123456"
    expectations:
      toolSelection:
        expectedTool: "AzureDevOpsTool"
        expectedCapability: "GenerateNewsletter"
```

### Context Retention
```yaml
name: "Newsletter with Follow-up"
turns:
  - turnNumber: 1
    input: "generate a newsletter for feature 3097408"
    expectations:
      contextRetention:
        shouldRemember:
          - key: "lastFeatureId"
            value: "3097408"

  - turnNumber: 2
    input: "now validate the hierarchy for that feature"
    expectations:
      contextRetention:
        verifyContextUsage:
          - contextKey: "lastFeatureId"
            usedInParameter: "workItemId"
```

## CI/CD Integration

### GitHub Actions Example (Future)

```yaml
- name: Run Hermes Evaluations
  run: |
    dotnet run --project Hermes.Evals -- run \
      --scenarios "Scenarios/Definitions/*.yml" \
      --output "TestResults/evaluation.json"

- name: Check Results
  run: |
    # Parse JSON and fail if success rate < 80%
    dotnet run --project Hermes.Evals -- check \
      --baseline "Baselines/latest.json" \
      --threshold 0.80
```

## Roadmap

### Implemented (MVP - Phases 1-6) ✅
- ✅ Core models with organized folder structure
- ✅ 4 weighted evaluators (Tool, Params, Context, Quality)
- ✅ Execution engine with REST API support
- ✅ YAML scenario loading with validation
- ✅ Mock data provider
- ✅ Triple report output (JSON, Markdown, Console)
- ✅ Dependency injection setup
- ✅ Sample scenarios

### Phase 7: CLI Interface
- [ ] System.CommandLine integration
- [ ] `run` command with scenario pattern matching
- [ ] `baseline save` and `baseline compare` commands
- [ ] `--mode`, `--data`, `--output` flags

### Phase 8: CI/CD Integration
- [ ] GitHub Actions workflow
- [ ] PR comment with results
- [ ] Regression detection and blocking

### Future Enhancements
- [ ] RealDataProvider for live Azure DevOps + Microsoft Graph
- [ ] BaselineComparer for regression detection
- [ ] DirectOrchestrator execution mode (in-process)
- [ ] Parallel scenario execution
- [ ] Custom evaluator plugins
- [ ] HTML report format

## Contributing

When adding new scenarios:
1. Place YAML files in `Scenarios/Definitions/`
2. Use descriptive names (e.g., `sla-registration-workflow.yml`)
3. Include all 4 evaluation dimensions where applicable
4. Add mock data for deterministic testing
5. Document expected behavior in scenario description

## Troubleshooting

### Scenario validation fails
- Check YAML syntax with a validator
- Ensure turn numbers are sequential (1, 2, 3...)
- Verify scoring weights sum to 1.0
- Ensure required fields (userId, name) are present

### Build errors
- Run `dotnet restore` to ensure packages are installed
- Check that main Hermes project builds successfully
- Use `--no-dependencies` flag if Hermes.exe is locked

### Reports not generating
- Check Output directory exists and is writable
- Verify logger configuration in Program.cs
- Look for exceptions in console output

## License

Part of the Hermes project. See main repository for license details.

## Related Documentation

- [Main Hermes README](../README.md) - Overall project documentation
- [CLAUDE.md](../CLAUDE.md) - AI agent development guide
- [Hermes Architecture](../README.md#architecture) - System design overview

---

**Version:** MVP (Phases 1-6 Complete)
**Last Updated:** 2026-01-30
**Status:** ✅ Working MVP - Scenario loading & reporting functional
