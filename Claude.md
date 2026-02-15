# CLAUDE.md - AI Agent Guide for Hermes Repository

Comprehensive guide for AI agents working on the Hermes codebase.

---

## Project Overview

**Hermes** is an AI-powered project assistant integrating with Azure DevOps and Microsoft Graph to:
- Generate executive newsletters from work item hierarchies
- Validate work item parent hierarchies
- Monitor SLA violations with multi-team support
- Provide PR activity insights

**Tech Stack:** .NET 8.0, ASP.NET Core, Autofac, Azure OpenAI, Azure Cosmos DB, BitFaster.Caching, Azure DevOps SDK, Microsoft Graph

**Current Status:** 153 source files, 67 test files, 417 tests passing

---

## Architecture

### System Flow
```
User (Teams/REST/WebSocket)
    ↓
HermesOrchestrator (Prompts + Tool Routing)
    ↓
Tools (IAgentTool) → Capabilities (IAgentToolCapability<TInput>)
    ↓
Integration Clients (Azure DevOps, Microsoft Graph)
    ↓
Storage Layer (L1: BitFaster Cache → L2: CosmosDB)
```

### Key Principles
1. **Capability-based architecture** - Tools expose strongly-typed, self-contained operations
2. **Repository pattern** - Domain-specific storage abstractions over generic storage clients
3. **Interface-driven design** - All major components have interfaces for testability
4. **Async throughout** - Use async/await patterns consistently

---

## Directory Structure

```
Hermes/
├── Orchestrator/               # Core LLM orchestration
├── Storage/
│   ├── Core/                   # Storage abstractions (L1/L2)
│   └── Repositories/           # Domain repositories (one folder per domain)
│       └── {Domain}/
│           ├── {Domain}Repository.cs
│           ├── I{Domain}Repository.cs
│           └── {Domain}Document.cs
├── Tools/                      # LLM-invokable tools
│   └── {ToolName}/
│       ├── {ToolName}Tool.cs
│       └── Capabilities/
│           ├── {Operation}Capability.cs
│           └── Inputs/{Operation}CapabilityInput.cs
├── Integrations/               # External service clients
│   └── {ServiceName}/
│       ├── I{ServiceName}Client.cs
│       └── {ServiceName}Client.cs
├── Domain/                     # Business logic & domain models
├── Notifications/              # Message formatting for Teams
├── Infrastructure/             # Seeders, scheduled jobs
├── Controllers/                # REST API endpoints
├── Channels/Teams/             # Teams bot adapter
├── DI/                         # Autofac dependency injection modules
└── Resources/Instructions/     # LLM prompts & capability specs
    └── ProjectAssistant/
        ├── agentspec.json      # Capability manifest
        ├── agent.txt           # Base instructions
        └── Capabilities/       # Per-capability prompts

Hermes.Tests/                   # xUnit + Moq tests (mirrors main structure)
Hermes.Evals/                   # LLM evaluation framework
```

---

## Naming Conventions

### Classes & Interfaces

| Type | Pattern | Example |
|------|---------|---------|
| Interface | `I{Name}` | `IStorageClient`, `IAgentTool` |
| Implementation | `{Name}` | `CosmosDbStorageClient` |
| Base Class | `{Name}Base` | `RepositoryBase<T>` |
| Document Model | `{Domain}Document` | `TeamConfigurationDocument` |
| Repository | `{Domain}Repository` | `TeamConfigurationRepository` |
| Capability | `{Operation}Capability` | `GenerateNewsletterCapability` |
| Capability Input | `{Operation}CapabilityInput` | `GenerateNewsletterCapabilityInput` |
| DI Module | `{Layer}Module` | `StorageModule`, `AgentToolsModule` |

### Rules
- **No abbreviations** in class names (✅ `LocalDevelopmentAuthentication` ❌ `LocalDevelopmentAuth`)
- **One class per file** (file name matches class name exactly)
- **Private methods** prefix with underscore: `_ValidateEntity()`, `_BuildQueryAsync()`
- **Async methods** suffix with `Async`: `CreateAsync()`, `ExecuteAsync()`
- **ILogger first** in constructor parameters: `MyClass(ILogger<MyClass> logger, ...)`

---

## Core Patterns

### 1. Storage Layer

**All entities extend `Document`:**
```csharp
public class MyDomainDocument : Document
{
    public string MyProperty { get; set; } = string.Empty;
}
```

**Document provides:** `Id`, `PartitionKey`, `Etag`, `TTL` (default 8 hours)

**Storage hierarchy:**
- `BitFasterStorageClient<T>` - L1 in-memory cache
- `CosmosDbStorageClient<T>` - L2 persistent storage
- `HierarchicalStorageClient<T>` - L1→L2 composite (default)

**Repository pattern:**
```csharp
public class MyRepository : RepositoryBase<MyDocument>, IMyRepository
{
    public MyRepository(IStorageClient<MyDocument, string> storage)
        : base(storage) { }

    // Domain-specific methods
    public async Task<MyDocument?> GetByCustomFieldAsync(string field) { }
}
```

**Location:** `Hermes/Storage/Repositories/{Domain}/` with 3 files:
- `{Domain}Repository.cs`
- `I{Domain}Repository.cs`
- `{Domain}Document.cs`

### 2. Tool & Capability Pattern

**Tools** route operations to capabilities:
```csharp
public class MyTool : IAgentTool
{
    private static readonly IReadOnlyDictionary<string, string[]> CapabilityAliases =
        new Dictionary<string, string[]>
        {
            { "MyOperation", new[] { "Alias1", "Alias2" } }
        };

    public string Name => "MyTool";
    public IReadOnlyList<string> Capabilities => new[] { _myCapability.Name };

    public async Task<string> ExecuteAsync(string operation, string input)
    {
        if (!CapabilityMatcher.TryResolve(operation, CapabilityAliases, out var canonical))
            throw new NotSupportedException(
                CapabilityMatcher.FormatNotSupportedError(operation, Name, CapabilityAliases.Keys));

        return canonical switch
        {
            "MyOperation" => await ExecuteMyOperationAsync(input),
            _ => throw new InvalidOperationException($"Unhandled: {canonical}")
        };
    }
}
```

**Capabilities** are strongly-typed operations:
```csharp
public sealed class MyOperationCapability : IAgentToolCapability<MyOperationCapabilityInput>
{
    private readonly ILogger<MyOperationCapability> _logger;
    private readonly IMyServiceClient _client;

    public string Name => "MyOperation";
    public string Description => "Brief description";

    public async Task<string> ExecuteAsync(MyOperationCapabilityInput input)
    {
        // Implementation - always return JSON
        return JsonSerializer.Serialize(result);
    }
}
```

**Capability inputs** extend `ToolCapabilityInputBase`:
```csharp
public class MyOperationCapabilityInput : ToolCapabilityInputBase
{
    public int WorkItemId { get; set; }
    public string? Filter { get; set; }
}
```

**CRITICAL:**
- Capabilities always return JSON strings
- Register as `InstancePerDependency` (stateless)
- Tools register as `SingleInstance` (stateful)

### 3. CapabilityMatcher

Provides flexible LLM-friendly operation name resolution:

**Matching strategies** (in precedence order):
1. **ExactMatch** - Case-insensitive exact: `"GetWorkItemTree"` → `"GetWorkItemTree"`
2. **AliasMatch** - Registered aliases: `"GetTree"` → `"GetWorkItemTree"`
3. **PatternMatch** - Remove common affixes: `"UserProfile"` → `"GetUserProfile"`
4. **PartialMatch** - Substring (if unambiguous): `"Violations"` → `"CheckSlaViolations"`

**Benefits:**
- Handles LLM variations without strict naming
- Testable utility with comprehensive tests
- Self-documenting via aliases

### 4. Dependency Injection

**Lifecycle rules:**
- `SingleInstance`: Storage clients, repositories, tools, orchestrators
- `InstancePerDependency`: Capabilities, evaluators

**Registration patterns:**
```csharp
// Repository
builder.RegisterType<MyRepository>()
    .As<IMyRepository>()
    .SingleInstance();

// Capability
builder.RegisterType<MyCapability>()
    .As<IAgentToolCapability<MyCapabilityInput>>()
    .AsSelf()
    .InstancePerDependency();

// Named registration (L1 cache example)
builder.RegisterGeneric(typeof(BitFasterStorageClient<>))
    .Named("l1", typeof(IStorageClient<,>))
    .SingleInstance();
```

---

## Common Operations

### Adding a Capability

**9-step process (all required):**

1. **Create input model** `Hermes/Tools/{ToolName}/Capabilities/Inputs/{Operation}CapabilityInput.cs`
2. **Implement capability** `Hermes/Tools/{ToolName}/Capabilities/{Operation}Capability.cs`
3. **Register in DI** `Hermes/DI/AgentToolsModule.cs`
4. **Update tool facade** - Inject capability, add to Capabilities list, route in ExecuteAsync
5. **Create instruction file** `Hermes/Resources/Instructions/ProjectAssistant/Capabilities/{Operation}.txt`
6. **Update agentspec.json** - Add capability to manifest
7. **⚠️ Create unit tests** `Hermes.Tests/Tools/{ToolName}/Capabilities/{Operation}CapabilityTests.cs`
8. **Update tool tests** - Inject new capability in tool test helpers
9. **Run tests** - `dotnet test --filter "FullyQualifiedName~{Operation}CapabilityTests"`

**Minimum test coverage (5 tests required):**
1. Happy path with valid input
2. Edge cases (empty/null/missing fields)
3. Dependency verification (mocks called correctly)
4. Property validations (Name, Description)
5. Error conditions (invalid input, client failures)

### Adding a Repository

1. **Create folder** `Hermes/Storage/Repositories/{Domain}/`
2. **Create document** `{Domain}Document.cs` extending `Document`
3. **Create interface** `I{Domain}Repository.cs` extending `IRepository<T>`
4. **Create implementation** `{Domain}Repository.cs` extending `RepositoryBase<T>`
5. **Register in DI** `Hermes/DI/StorageModule.cs` as `SingleInstance`
6. **Create tests** `Hermes.Tests/Storage/Repositories/{Domain}/{Domain}RepositoryTests.cs`

### Adding an Integration Client

1. **Create folder** `Hermes/Integrations/{ServiceName}/`
2. **Create interface** `I{ServiceName}Client.cs`
3. **Create implementation** `{ServiceName}Client.cs` - Return JSON strings
4. **Register in DI** `Hermes/DI/IntegrationsModule.cs` as `SingleInstance`
5. **Create tests** `Hermes.Tests/Integrations/{ServiceName}/{ServiceName}ClientTests.cs`

---

## Testing Requirements

### ⚠️ MANDATORY - Tests Required Before Commit

**When tests are required:**
- New capabilities: Dedicated test class with 5+ test cases
- New repositories: CRUD operation coverage
- New integration clients: Mocked external dependencies
- Modified business logic: Update or add tests
- Bug fixes: Regression test demonstrating fix

**Test standards:**
- **Framework:** xUnit + Moq
- **Structure:** Arrange-Act-Assert pattern
- **Naming:** `MethodName_Scenario_ExpectedResult`
- **Mock dependencies:** All external dependencies mocked
- **Mirror structure:** Tests mirror main project structure

**Running tests:**
```bash
# All tests
dotnet test Hermes.Tests

# Specific capability
dotnet test --filter "FullyQualifiedName~MyCapabilityTests"

# Quick verification
dotnet test --no-build --verbosity quiet
```

**Current test count:** 417 passing, 2 skipped

---

## Integration Testing

### Quick REST API Test
```bash
curl -X POST "http://localhost:3978/api/hermes/v1.0/chat" \
  -H "Content-Type: application/json" \
  -H "x-ms-correlation-id: test-001" \
  -d '{"text": "generate a newsletter for feature 12345", "userId": "user@example.com"}'
```

### Hermes.Evals Framework

**Automated evaluation suite** measuring:
- Tool Selection (30%) - Correct tool/capability chosen
- Parameter Extraction (30%) - Parameters extracted correctly
- Context Retention (25%) - Multi-turn context handling
- Response Quality (15%) - Output completeness/formatting

**Running evals:**
```bash
# Start Hermes
cd Hermes && dotnet run --configuration Release

# Run evals (separate terminal)
cd Hermes.Evals && dotnet run --configuration Release
```

**Target:** ≥85% overall score, ≥93% for Phase 5 (current baseline)

---

## Critical Rules

### Security
- ⚠️ **NEVER hardcode credentials** - Use Azure Key Vault or environment variables
- Do not commit secrets to git

### Code Quality
1. **Interface-driven design** - All major components need interfaces
2. **Generic where possible** - Especially for storage layer
3. **Single responsibility** - Each class/method has one purpose
4. **Async all the way** - Use async/await consistently, suffix with `Async`
5. **Nullable reference types** - Enabled, use `?` appropriately

### Storage
- **Always write L2 before L1** - Ensures durability
- **Partition keys required** - All storage operations need partition keys
- **TTL default** - 8 hours (28,800 seconds)

### Tools & Capabilities
- **One capability per operation** - Don't create monolithic capabilities
- **Strongly-typed inputs** - Never use `dynamic` or loosely typed inputs
- **JSON output** - Always serialize results to JSON string
- **Stateless capabilities** - Register as `InstancePerDependency`

---

## Common Pitfalls

1. ❌ Manual JSON parsing in capabilities → ✅ Use strongly-typed input models
2. ❌ Inject `IStorageClient` directly → ✅ Use repository abstractions
3. ❌ Register capabilities as `SingleInstance` → ✅ Use `InstancePerDependency`
4. ❌ Skip interfaces → ✅ Every major component needs an interface
5. ❌ Multiple capabilities in one class → ✅ One capability per class (sealed)
6. ❌ Use `Task.Result` or `.Wait()` → ✅ Use async/await throughout
7. ❌ Create Document types without extending `Document` → ✅ Always extend `Document`
8. ❌ **Skip writing unit tests** → ✅ **Tests are mandatory before commit**

---

## Quick Reference

### Key Files

| File | Purpose |
|------|---------|
| `Hermes/Orchestrator/HermesOrchestrator.cs` | Core LLM orchestration logic |
| `Hermes/DI/HermesModule.cs` | Root DI module |
| `Hermes/Storage/Core/HierarchicalStorageClient.cs` | L1→L2 storage pattern |
| `Hermes/Tools/IAgentToolCapability.cs` | Capability interface |
| `Hermes/Resources/Instructions/ProjectAssistant/agentspec.json` | Capability manifest |
| `Hermes/Controllers/HermesController.cs` | REST API entry point |
| `Hermes/Program.cs` | Application startup |

### Where to Find Things

| Task | Location |
|------|----------|
| Add Azure DevOps operation | `Hermes/Tools/AzureDevOps/Capabilities/` |
| Create storage entity | `Hermes/Storage/Repositories/{Domain}/` |
| Modify orchestration | `Hermes/Orchestrator/HermesOrchestrator.cs` |
| Add REST endpoint | `Hermes/Controllers/` |
| Configure DI | `Hermes/DI/{Layer}Module.cs` |
| Update LLM prompts | `Hermes/Resources/Instructions/ProjectAssistant/` |
| Add integration client | `Hermes/Integrations/{ServiceName}/` |
| Add unit tests | `Hermes.Tests/{MirrorPath}/` |

---

## Running Locally

```bash
# Prerequisites
# - Cosmos DB Emulator running
# - Azure CLI authenticated (for Azure DevOps)
# - appsettings.json configured

# Build
dotnet build

# Run
dotnet run --project Hermes

# Run tests
dotnet test Hermes.Tests

# API available at: http://localhost:3978
```

---

## Architecture Decisions

**Why capability pattern?**
- Isolated testing of individual operations
- Strongly-typed inputs prevent runtime errors
- Easy to add/remove without affecting others
- Clear separation of concerns

**Why L1/L2 storage?**
- L1 (BitFaster) provides fast in-memory access
- L2 (CosmosDB) provides durability
- Graceful degradation if L1 fails
- Write-through ensures consistency

**Why repository pattern?**
- Domain-specific operations abstracted from storage mechanics
- Business logic separated from infrastructure
- Additional validation and transformation layer

**Why Autofac?**
- Named registrations for multiple implementations (L1/L2)
- More powerful generic type registration
- Module-based organization

---

## Recent Major Features

### Multi-Team SLA Support
- **TeamConfiguration repository** - Per-team SLA rules and iteration paths
- **Multi-team evaluator** - Check violations across subscribed teams
- **Team-separated messages** - Adaptive formatting for multi-team scenarios
- **Backwards compatible** - Single-team users see unchanged format

---

## Help & Documentation

- **Full architecture:** See `README.md`
- **API docs:** XML doc comments in code
- **Test examples:** `Hermes.Tests/` for patterns
- **Evaluation framework:** `Hermes.Evals/README.md`

---

**Last updated:** February 14, 2026
**Current status:** 153 source files, 67 test files, 417 tests passing
