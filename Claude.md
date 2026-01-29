# Claude.md - AI Agent Guide for Hermes Repository

This file provides context and conventions for AI agents (like Claude) working on the Hermes codebase.

---

## Project Context

**Hermes** is an AI-powered project assistant that integrates with Azure DevOps to generate executive communications and validate work item hierarchies. It operates as both a Microsoft Teams bot and REST/WebSocket API.

**Tech Stack:** .NET 8.0, ASP.NET Core, Autofac, Azure OpenAI, Azure Cosmos DB, BitFaster.Caching, Azure DevOps SDK

**Git Branch:** Work on `main` branch for PRs unless user specifies otherwise.

---

## Architecture Quick Reference

```
Channels (Teams/REST/WebSocket)
    ↓
HermesOrchestrator (IAgentOrchestrator)
    ↓
Tools (IAgentTool) → Capabilities (IAgentToolCapability<TInput>)
    ↓
Integration Clients (IAzureDevOpsWorkItemClient)
    ↓
Storage Layer (L1: BitFaster → L2: CosmosDB)
```

**Key Principle:** Capability-based architecture where tools expose strongly-typed, self-contained capabilities.

---

## File Organization

```
Hermes/
├── Orchestrator/               # Core orchestration (HermesOrchestrator)
│   └── Prompts/               # Prompt composition (AgentPromptComposer)
├── Storage/
│   ├── Core/                  # Storage abstractions & implementations
│   │   ├── Models/            # Document base class
│   │   ├── CosmosDB/          # L2 persistent storage
│   │   ├── InMemory/          # L1 cache (BitFaster)
│   │   ├── File/              # File-based storage
│   │   └── HierarchicalStorageClient.cs  # L1→L2 composite
│   └── Repositories/          # Domain repositories
│       └── {Domain}/          # One folder per repository
│           ├── {Domain}Repository.cs
│           ├── I{Domain}Repository.cs
│           └── {Domain}Document.cs
├── Tools/
│   └── {ToolName}/            # One folder per tool
│       ├── {ToolName}Tool.cs  # Tool facade
│       └── Capabilities/      # Individual operations
│           ├── {Operation}Capability.cs
│           └── Inputs/
│               └── {Operation}Input.cs
├── Integrations/
│   └── {ServiceName}/         # External service clients
│       ├── I{ServiceName}Client.cs
│       └── {ServiceName}Client.cs
├── Channels/
│   └── Teams/                 # Teams bot implementation
├── Controllers/               # REST API endpoints
├── Authentication/            # Auth middleware
├── DI/                        # Autofac modules
│   ├── HermesModule.cs        # Parent module
│   ├── StorageModule.cs       # Storage DI
│   ├── AgentToolsModule.cs    # Tool/capability DI
│   ├── IntegrationsModule.cs  # External services DI
│   └── TeamsModule.cs         # Teams channel DI
└── Resources/
    └── Instructions/          # AI agent prompts
        └── ProjectAssistant/
            ├── agentspec.json # Capability manifest
            ├── agent.txt      # Base agent instructions
            └── Capabilities/  # Per-capability instructions
                └── {CapabilityName}.txt

Hermes.Tests/                  # xUnit + Moq tests
└── {MirrorStructure}/         # Mirrors main project structure
```

---

## Naming Conventions (STRICT)

### Classes & Interfaces

| Type | Pattern | Example |
|------|---------|---------|
| Interface | `I{Name}` | `IStorageClient`, `IAgentTool` |
| Implementation | `{Name}` | `CosmosDbStorageClient` |
| Base Class | `{Name}Base` | `RepositoryBase<T>` |
| Document Model | `{Domain}Document` | `ConversationHistoryDocument` |
| Repository | `{Domain}Repository` | `ConversationHistoryRepository` |
| Capability | `{Operation}Capability` | `GetWorkItemTreeCapability` |
| Capability Input | `{Operation}CapabilityInput` | `GetWorkItemTreeCapabilityInput` |
| DI Module | `{Layer}Module` | `StorageModule` |

### General Naming Rules

- **No abbreviations:** Use full words in class names, not shorthand
  - ✅ `LocalDevelopmentAuthentication`
  - ❌ `LocalDevelopmentAuth`
- **Be descriptive:** Prefer clarity over brevity in naming
- **Consistency:** Follow established patterns in the codebase
- **One class per file:** Each class, interface, or enum must be in its own file
  - File name must match the class/interface name exactly
  - ✅ `SchedulingConfiguration.cs` contains only `SchedulingConfiguration` class
  - ✅ `JobConfiguration.cs` contains only `JobConfiguration` class
  - ❌ Multiple classes in a single file (except for nested classes within their parent)

### Methods

- **Private methods:** Prefix with underscore: `_ValidateEntity()`, `_BuildWorkItemTreeAsync()`
- **Async methods:** Suffix with `Async`: `CreateAsync()`, `ReadAsync()`

### Namespaces

- `Hermes.{Layer}.{Component}` (e.g., `Hermes.Storage.Repositories.ConversationHistory`)
- Each repository/tool/integration in its own namespace

---

## Core Patterns & Conventions

### 1. Storage Layer Pattern

**All storage entities extend `Document`:**

```csharp
public class MyDomainDocument : Document
{
    public string MyProperty { get; set; } = string.Empty;
}
```

**Document base class provides:**
- `Id` (string)
- `PartitionKey` (string)
- `Etag` (string?)
- `TTL` (int? - default 8 hours)

**Storage hierarchy:**
```
IStorageClient<T, TKey>
├── BitFasterStorageClient<T> (L1)
├── CosmosDbStorageClient<T> (L2)
└── HierarchicalStorageClient<T> (L1→L2 composite)
```

**Repository pattern:**
```csharp
public class MyRepository : RepositoryBase<MyDocument>, IMyRepository
{
    public MyRepository(IStorageClient<MyDocument, string> storage)
        : base(storage) { }

    // Add domain-specific methods here
}
```

**IMPORTANT:** Repositories live in `Hermes.Storage.Repositories.{Domain}` with 3 files:
- `{Domain}Repository.cs`
- `I{Domain}Repository.cs`
- `{Domain}Document.cs`

### 2. Tool & Capability Pattern

**Tools are facades that route to capabilities:**

```csharp
public class MyTool : IAgentTool
{
    private readonly MyCapability _myCapability;

    public string Name => "MyTool";
    public IReadOnlyList<string> Capabilities => new[] { _myCapability.Name };

    public async Task<string> ExecuteAsync(string operation, string input)
    {
        // Route to appropriate capability based on operation name
    }
}
```

**Capabilities are strongly-typed, self-contained operations:**

```csharp
public sealed class MyCapability : IAgentToolCapability<MyCapabilityInput>
{
    public string Name => "MyOperation";
    public string Description => "Brief description";

    public async Task<string> ExecuteAsync(MyCapabilityInput input)
    {
        // Implementation
        return JsonSerializer.Serialize(result);
    }
}
```

**Capability inputs extend `ToolCapabilityInputBase`:**

```csharp
public class MyCapabilityInput : ToolCapabilityInputBase
{
    public int WorkItemId { get; set; }
    public string Filter { get; set; } = string.Empty;
}
```

**CRITICAL:** Capabilities always return JSON strings.

### 3. Capability Matching Pattern

**CapabilityMatcher provides flexible operation name resolution with multiple strategies:**

```csharp
// Define aliases for each capability (one-time setup in tool class)
private static readonly IReadOnlyDictionary<string, string[]> CapabilityAliases = new Dictionary<string, string[]>
{
    { "GetWorkItemTree", new[] { "GetTree", "WorkItemTree", "FetchTree" } },
    { "RegisterSlaNotifications", new[] { "RegisterSLA", "RegisterForSLA", "Register" } },
    { "CheckSlaViolations", new[] { "CheckViolations", "CheckSLA", "SLACheck" } }
};

// Use in ExecuteAsync method
public async Task<string> ExecuteAsync(string operation, string input)
{
    if (!CapabilityMatcher.TryResolve(operation, CapabilityAliases, out var canonicalName))
    {
        throw new NotSupportedException(
            CapabilityMatcher.FormatNotSupportedError(operation, Name, CapabilityAliases.Keys));
    }

    return canonicalName switch
    {
        "GetWorkItemTree" => await ExecuteGetWorkItemTreeAsync(input),
        "RegisterSlaNotifications" => await ExecuteRegisterAsync(input),
        "CheckSlaViolations" => await ExecuteCheckViolationsAsync(input),
        _ => throw new InvalidOperationException($"Unhandled canonical operation: {canonicalName}")
    };
}
```

**Matching strategies (applied in order of specificity):**

1. **ExactMatch**: Case-insensitive exact match with canonical name
   ```csharp
   "GetWorkItemTree" → "GetWorkItemTree" ✅
   "getworkitemtree" → "GetWorkItemTree" ✅
   ```

2. **AliasMatch**: Case-insensitive match with registered alias
   ```csharp
   "GetTree" → "GetWorkItemTree" ✅
   "registersla" → "RegisterSlaNotifications" ✅
   ```

3. **PatternMatch**: Match after removing common affixes (Get, Capability)
   ```csharp
   "UserProfile" → "GetUserProfile" ✅
   "RegisterSlaNotifications" → "RegisterSlaNotificationsCapability" ✅
   ```

4. **PartialMatch**: Substring match (only if unambiguous - exactly one match)
   ```csharp
   "Violations" → "CheckSlaViolations" ✅ (only one capability contains "Violations")
   "SLA" → No match ❌ (ambiguous - matches multiple capabilities)
   ```

**Benefits:**
- ✅ Consistent matching logic across all tools
- ✅ LLM-friendly: accepts common variations without strict naming
- ✅ Predictable: well-defined precedence (exact > alias > pattern > partial)
- ✅ Testable: isolated utility with comprehensive unit tests
- ✅ Maintainable: aliases defined once per tool
- ✅ Self-documenting: aliases show alternative names

**When to add aliases:**
- Common abbreviations: "SLA" for "SlaNotifications"
- Variations with/without prefixes: "Register" vs "RegisterFor"
- Natural language alternatives: "Check", "Get", "Fetch"

**Example tool implementation:**

```csharp
public class UserManagementTool : IAgentTool
{
    // Define aliases once
    private static readonly IReadOnlyDictionary<string, string[]> CapabilityAliases = new Dictionary<string, string[]>
    {
        { "RegisterSlaNotifications", new[] { "RegisterSLA", "RegisterForSLA", "Register" } },
        { "UnregisterSlaNotifications", new[] { "UnregisterSLA", "UnregisterForSLA", "Unregister" } }
    };

    public async Task<string> ExecuteAsync(string operation, string input)
    {
        // Use CapabilityMatcher for flexible resolution
        if (!CapabilityMatcher.TryResolve(operation, CapabilityAliases, out var canonicalName))
        {
            throw new NotSupportedException(
                CapabilityMatcher.FormatNotSupportedError(operation, Name, CapabilityAliases.Keys));
        }

        return canonicalName switch
        {
            "RegisterSlaNotifications" => await ExecuteRegisterAsync(input),
            "UnregisterSlaNotifications" => await ExecuteUnregisterAsync(input),
            _ => throw new InvalidOperationException($"Unhandled canonical operation: {canonicalName}")
        };
    }
}
```

### 4. Dependency Injection Pattern

**Lifecycle rules:**
- **SingleInstance:** Storage clients, repositories, tools, orchestrators (stateful/expensive)
- **InstancePerDependency:** Capabilities (lightweight/stateless)

**Named registrations for multiple implementations:**

```csharp
// L1 cache (named)
builder.RegisterGeneric(typeof(BitFasterStorageClient<>))
    .Named("l1", typeof(IStorageClient<,>))
    .SingleInstance();

// Default (uses L1+L2)
builder.RegisterGeneric(typeof(HierarchicalStorageClient<>))
    .As(typeof(IStorageClient<,>))
    .SingleInstance();
```

**Capability registration (always InstancePerDependency):**

```csharp
builder.RegisterType<MyCapability>()
    .As<IAgentToolCapability<MyCapabilityInput>>()
    .AsSelf()
    .InstancePerDependency();
```

### 4. Integration Client Pattern

**Always create interface + implementation:**

```csharp
// Interface
public interface IMyServiceClient
{
    Task<string> GetDataAsync(string id);
}

// Implementation
public class MyServiceClient : IMyServiceClient
{
    // Wrap external SDK
    // Return JSON strings
}
```

**IMPORTANT:** Integration clients return JSON strings for consistency with tools.

---

## Common Operations

### Adding a New Capability

1. **Create input model** in `Hermes/Tools/{ToolName}/Capabilities/Inputs/`
   ```csharp
   public class MyOperationCapabilityInput : ToolCapabilityInputBase
   {
       public int WorkItemId { get; set; }
   }
   ```

2. **Implement capability** in `Hermes/Tools/{ToolName}/Capabilities/`
   ```csharp
   public sealed class MyOperationCapability
       : IAgentToolCapability<MyOperationCapabilityInput>
   {
       private readonly IAzureDevOpsWorkItemClient _client;

       public string Name => "MyOperation";
       public string Description => "What it does";

       public async Task<string> ExecuteAsync(MyOperationCapabilityInput input)
       {
           // Implementation
           return JsonSerializer.Serialize(result);
       }
   }
   ```

3. **Register in DI** at `Hermes/DI/AgentToolsModule.cs`
   ```csharp
   builder.RegisterType<MyOperationCapability>()
       .As<IAgentToolCapability<MyOperationCapabilityInput>>()
       .AsSelf()
       .InstancePerDependency();
   ```

4. **Update tool facade** at `Hermes/Tools/{ToolName}/{ToolName}Tool.cs`
   - Inject capability in constructor
   - Add to `Capabilities` property
   - Route in `ExecuteAsync` method

5. **Create instruction file** at `Hermes/Resources/Instructions/ProjectAssistant/Capabilities/MyOperation.txt`

6. **Update agentspec.json** at `Hermes/Resources/Instructions/ProjectAssistant/agentspec.json`
   ```json
   {
     "capabilities": [
       {
         "name": "MyOperation",
         "description": "What it does"
       }
     ]
   }
   ```

7. **⚠️ MANDATORY: Create unit tests** at `Hermes.Tests/Tools/{ToolName}/Capabilities/MyOperationCapabilityTests.cs`
   ```csharp
   public class MyOperationCapabilityTests
   {
       [Fact]
       public async Task ExecuteAsync_ValidInput_ReturnsExpectedJson()
       {
           // Arrange
           var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
           mockClient.Setup(x => x.GetDataAsync(It.IsAny<int>()))
               .ReturnsAsync("{\"id\": 123}");

           var capability = new MyOperationCapability(mockClient.Object);
           var input = new MyOperationCapabilityInput { WorkItemId = 123 };

           // Act
           var result = await capability.ExecuteAsync(input);

           // Assert
           Assert.NotNull(result);
           Assert.Contains("123", result);
       }

       // Minimum 4 additional test cases required:
       // - Edge cases (empty/null)
       // - Dependency verification
       // - Property validations (Name, Description)
       // - Error conditions
   }
   ```

8. **Update existing tool tests** at `Hermes.Tests/Tools/{ToolName}/{ToolName}ToolTests.cs`
   - Update `CreateTool()` helper to inject new capability
   - Add any tool-level tests if the capability changes tool behavior

9. **Run tests before proceeding**
   ```bash
   dotnet test --filter "FullyQualifiedName~MyOperationCapabilityTests"
   ```

### Adding a New Repository

1. **Create folder** `Hermes/Storage/Repositories/{Domain}/`

2. **Create document model** `{Domain}Document.cs`
   ```csharp
   public class MyDomainDocument : Document
   {
       public string MyProperty { get; set; } = string.Empty;
   }
   ```

3. **Create interface** `I{Domain}Repository.cs`
   ```csharp
   public interface IMyDomainRepository : IRepository<MyDomainDocument>
   {
       Task<MyDomainDocument?> GetByCustomFieldAsync(string field);
   }
   ```

4. **Create implementation** `{Domain}Repository.cs`
   ```csharp
   public class MyDomainRepository
       : RepositoryBase<MyDomainDocument>,
         IMyDomainRepository
   {
       public MyDomainRepository(
           IStorageClient<MyDomainDocument, string> storage)
           : base(storage) { }

       public async Task<MyDomainDocument?> GetByCustomFieldAsync(string field)
       {
           // Implementation
       }
   }
   ```

5. **Register in DI** at `Hermes/DI/StorageModule.cs`
   ```csharp
   builder.RegisterType<MyDomainRepository>()
       .As<IMyDomainRepository>()
       .SingleInstance();
   ```

### Adding a New Integration Client

1. **Create folder** `Hermes/Integrations/{ServiceName}/`

2. **Create interface** `I{ServiceName}Client.cs`
   ```csharp
   public interface IMyServiceClient
   {
       Task<string> GetDataAsync(string id);
   }
   ```

3. **Create implementation** `{ServiceName}Client.cs`
   ```csharp
   public class MyServiceClient : IMyServiceClient
   {
       // Wrap external SDK, return JSON strings
   }
   ```

4. **Register in DI** at `Hermes/DI/IntegrationsModule.cs`
   ```csharp
   builder.RegisterType<MyServiceClient>()
       .As<IMyServiceClient>()
       .SingleInstance();
   ```

### Adding Tests

**Mirror the main project structure in Hermes.Tests/**

Example for capability test:
```csharp
public class MyOperationCapabilityTests
{
    [Fact]
    public async Task ExecuteAsync_ValidInput_ReturnsExpectedJson()
    {
        // Arrange
        var mockClient = new Mock<IAzureDevOpsWorkItemClient>();
        mockClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), null))
            .ReturnsAsync("{\"id\": 123}");

        var capability = new MyOperationCapability(mockClient.Object);
        var input = new MyOperationCapabilityInput { WorkItemId = 123 };

        // Act
        var result = await capability.ExecuteAsync(input);

        // Assert
        Assert.NotNull(result);
        // Additional assertions
    }
}
```

---

## Critical Rules

### Security

⚠️ **NEVER hardcode credentials in code**
- Use Azure Key Vault or environment variables
- Do not commit secrets to git

### Code Quality

1. **Interface-driven design:** All major components need interfaces
2. **Generic where possible:** Especially for storage layer
3. **Single responsibility:** Each class/method has one purpose
4. **Async all the way:** Use async/await, suffix with `Async`
5. **Nullable reference types:** Enabled, use `?` appropriately

### Storage

- **Always write L2 before L1:** Ensures durability
- **Partition keys required:** All storage operations need partition keys
- **TTL default:** 8 hours (28,800 seconds)

### Tools & Capabilities

- **One capability per operation:** Don't create monolithic capabilities
- **Strongly-typed inputs:** Never use `dynamic` or loosely typed inputs
- **JSON output:** Always serialize results to JSON string
- **Stateless capabilities:** Register as InstancePerDependency

### Testing

⚠️ **MANDATORY: Unit tests are REQUIRED before committing new code**

**When tests are required:**
- **New capabilities:** MUST have dedicated test class with minimum 5 test cases
- **New repositories:** MUST have tests covering CRUD operations
- **New integration clients:** MUST have tests with mocked external dependencies
- **Modified business logic:** MUST update existing tests or add new ones
- **Bug fixes:** SHOULD include regression test demonstrating the fix

**Test standards:**
- **Use xUnit + Moq:** Standard testing framework
- **Mock dependencies:** Use `Mock<T>` for all dependencies (IAzureDevOpsWorkItemClient, IStorageClient, etc.)
- **Arrange-Act-Assert:** Clear test structure in every test method
- **Test capabilities in isolation:** Each capability should have dedicated test class
- **Mirror structure:** Test files must mirror main project structure (e.g., `Hermes.Tests/Tools/AzureDevOps/Capabilities/`)
- **Descriptive test names:** Use `MethodName_Scenario_ExpectedResult` pattern
- **Coverage requirements:** Minimum test cases per capability:
  1. Happy path with valid input
  2. Edge cases (empty/null/missing fields)
  3. Dependency interactions (verify mocks called correctly)
  4. Property validations (Name, Description for capabilities)
  5. Error conditions (invalid input, client failures)

**Running tests before commit:**
```bash
# Run all tests
dotnet test Hermes.Tests

# Run specific capability tests
dotnet test --filter "FullyQualifiedName~YourCapabilityTests"

# Run with coverage (if configured)
dotnet test /p:CollectCoverage=true
```

**Test naming examples:**
- `ExecuteAsync_ValidInput_ReturnsExpectedJson`
- `ExecuteAsync_EmptyHierarchy_ReturnsEmptyArray`
- `ExecuteAsync_CallsClientWithCorrectParameters`
- `Name_ReturnsCorrectCapabilityName`

---

## Key Files to Know

| File | Purpose |
|------|---------|
| `Hermes/Orchestrator/HermesOrchestrator.cs` | Core orchestration logic |
| `Hermes/DI/HermesModule.cs` | Root DI module |
| `Hermes/Storage/Core/HierarchicalStorageClient.cs` | L1→L2 storage pattern |
| `Hermes/Storage/Repositories/RepositoryBase.cs` | Base repository implementation |
| `Hermes/Tools/IAgentToolCapability.cs` | Capability interface |
| `Hermes/Controllers/HermesController.cs` | REST API entry point |
| `Hermes/Channels/Teams/HermesTeamsAgent.cs` | Teams bot entry point |
| `Hermes/Resources/Instructions/ProjectAssistant/agentspec.json` | Capability manifest |

---

## Running & Testing

### Run locally
```bash
# Start Cosmos DB Emulator first
dotnet run --project Hermes
```

### Run tests
```bash
dotnet test Hermes.Tests
```

### Build
```bash
dotnet build
```

---

## Integration Testing

⚠️ **MANDATORY: Run integration tests BEFORE every commit to prevent regressions**

Integration tests verify end-to-end capability functionality through the REST API, ensuring LLM routing, tool invocation, and response formatting all work correctly.

### Prerequisites

1. **CosmosDB Emulator** running locally
2. **Azure DevOps** access configured (uses Azure CLI authentication)
3. **Microsoft Graph** credentials in `appsettings.json` (for SLA capabilities)
4. **Application running**: `dotnet run --project Hermes`

### Test Approach

**Use the REST chat endpoint for testing:**
```bash
curl -X POST "http://localhost:3978/api/hermes/v1.0/chat" \
  -H "Content-Type: application/json" \
  -H "x-ms-correlation-id: test-001" \
  -d '{"text": "<natural language request>", "userId": "<user@microsoft.com>"}'
```

**Why REST endpoint?**
- Faster iteration than Teams bot
- Easy to verify LLM routing and prompt effectiveness
- Enables prompt refinement based on test results
- Simulates real user scenarios

### Integration Test Suite

Run ALL capabilities before committing to catch regressions:

#### 1. Newsletter Generation (Azure DevOps Tool)

**Test Command:**
```bash
curl -X POST "http://localhost:3978/api/hermes/v1.0/chat" \
  -H "Content-Type: application/json" \
  -H "x-ms-correlation-id: test-correlation-1" \
  -d '{"text": "generate a newsletter for feature 3097408"}'
```

**Expected Result:**
- ✅ LLM calls `AzureDevOpsTool.Execute("GenerateNewsletter", ...)`
- ✅ Returns formatted newsletter with summary, progress, risks, timeline
- ✅ Includes work item IDs as citations

**Verify in Logs:**
```
info: Hermes.Tools.AzureDevOps.AzureDevOpsTool[0]
      Executing AzureDevOpsTool operation: GenerateNewsletter
```

#### 2. Parent Hierarchy Validation (Azure DevOps Tool)

**Test Command:**
```bash
curl -X POST "http://localhost:3978/api/hermes/v1.0/chat" \
  -H "Content-Type: application/json" \
  -H "x-ms-correlation-id: test-correlation-2" \
  -d '{"text": "validate the parent hierarchy from work item 3097408"}'
```

**Expected Result:**
- ✅ LLM calls `AzureDevOpsTool.Execute("ValidateHierarchy", ...)`
- ✅ Returns validation results with any hierarchy violations
- ✅ Suggests corrections if hierarchy is invalid

**Verify in Logs:**
```
info: Hermes.Tools.AzureDevOps.AzureDevOpsTool[0]
      Executing AzureDevOpsTool operation: ValidateHierarchy
```

#### 3. Register SLA Notifications (User Management Tool)

**Test Command:**
```bash
curl -X POST "http://localhost:3978/api/hermes/v1.0/chat" \
  -H "Content-Type: application/json" \
  -H "x-ms-correlation-id: test-correlation-3" \
  -d '{"text": "register me for SLA notifications", "userId": "testuser@microsoft.com"}'
```

**Expected Result:**
- ✅ LLM calls `UserManagementTool.Execute("RegisterSlaNotifications", {"teamsUserId": "testuser@microsoft.com"})`
- ✅ Microsoft Graph fetches user profile and direct reports
- ✅ UserConfiguration created in CosmosDB with SlaRegistration
- ✅ Response confirms registration (manager: includes direct report count)

**Verify in Logs:**
```
info: Hermes.Tools.UserManagement.UserManagementTool[0]
      Executing UserManagementTool operation: RegisterSlaNotifications
info: Hermes.Integrations.MicrosoftGraph.MicrosoftGraphClient[0]
      Retrieved profile for user testuser@microsoft.com: Email=..., IsManager=True, DirectReports=X
info: Hermes.Tools.UserManagement.Capabilities.RegisterSlaNotificationsCapability[0]
      Successfully registered user testuser@microsoft.com for SLA notifications
```

**Verify in CosmosDB:**
Query CosmosDB to confirm UserConfiguration document exists:
```sql
SELECT * FROM c WHERE c.SlaRegistration != null AND c.SlaRegistration.IsRegistered = true
```

#### 4. Check SLA Violations (Work Item SLA Tool)

**Test Command:**
```bash
curl -X POST "http://localhost:3978/api/hermes/v1.0/chat" \
  -H "Content-Type: application/json" \
  -H "x-ms-correlation-id: test-correlation-4" \
  -d '{"text": "check my SLA violations", "userId": "testuser@microsoft.com"}'
```

**Expected Result:**
- ✅ LLM calls `WorkItemSlaTool.Execute("CheckSlaViolations", {"teamsUserId": "testuser@microsoft.com"})`
- ✅ Returns formatted report with violations grouped by owner (for managers)
- ✅ Includes work item details: ID, title, days since update, SLA threshold
- ✅ Shows top priority items and actionable recommendations

**Verify in Logs:**
```
info: Hermes.Tools.WorkItemSla.WorkItemSlaTool[0]
      Executing WorkItemSlaTool operation: CheckSlaViolations
info: Hermes.Tools.WorkItemSla.Capabilities.CheckSlaViolationsCapability[0]
      Found X SLA violations for user testuser@microsoft.com (Y owners)
```

**Expected Response Format (Manager):**
- Summary section with total violations and breakdown
- Quick owner breakdown table
- Top 5 most stale items prioritized
- Recommended next steps

#### 5. Unregister SLA Notifications (User Management Tool)

**Test Command:**
```bash
curl -X POST "http://localhost:3978/api/hermes/v1.0/chat" \
  -H "Content-Type: application/json" \
  -H "x-ms-correlation-id: test-correlation-5" \
  -d '{"text": "unregister me from SLA notifications", "userId": "testuser@microsoft.com"}'
```

**Expected Result:**
- ✅ LLM calls `UserManagementTool.Execute("UnregisterSlaNotifications", {"teamsUserId": "testuser@microsoft.com"})`
- ✅ UserConfiguration updated: `IsRegistered = false`
- ✅ Response confirms unregistration

**Verify in Logs:**
```
info: Hermes.Tools.UserManagement.UserManagementTool[0]
      Executing UserManagementTool operation: UnregisterSlaNotifications
info: Hermes.Tools.UserManagement.Capabilities.UnregisterSlaNotificationsCapability[0]
      Successfully unregistered user testuser@microsoft.com from SLA notifications
```

**Verify Scheduled Job:**
Wait for next scheduled job (runs every minute) and confirm:
```
info: Hermes.Storage.Repositories.UserConfiguration.UserConfigurationRepository[0]
      GetAllWithSlaRegistrationAsync found 0 registered users
```

### Common Integration Test Issues

**Issue: "TeamsUserId is required" error**
- **Cause:** LLM not extracting userId from User Context system message
- **Check:** Review capability instruction files for clear parameter extraction guidance
- **Workaround:** LLM should self-correct on retry after seeing error message

**Issue: Tool not being called**
- **Cause:** Tool not registered in HermesModule or capability not added to agentspec.json
- **Check:**
  ```bash
  grep -r "RegisterType.*MyCapability" Hermes/DI/
  grep "my-capability" Hermes/Resources/Instructions/ProjectAssistant/agentspec.json
  ```

**Issue: "Operation not supported" error**
- **Cause:** Operation name mismatch between instruction file and tool's switch statement
- **Check:** Tool should include common aliases (e.g., "RegisterSLA", "RegisterSlaNotifications", "RegisterForSlaNotifications")

**Issue: Response timeout**
- **Cause:** Long-running operations or slow external APIs
- **Not critical:** Check logs to verify tool execution succeeded even if response times out

### Pre-Commit Integration Test Checklist

Before committing capability changes, verify:

- [ ] **Unit tests pass**: `dotnet test Hermes.Tests`
- [ ] **Application builds**: `dotnet build Hermes`
- [ ] **Application starts**: `dotnet run --project Hermes` (no startup errors)
- [ ] **Newsletter generation works**: Test with valid epic/feature ID
- [ ] **Hierarchy validation works**: Test with valid work item ID
- [ ] **SLA registration works**: Test with valid user ID, verify in CosmosDB
- [ ] **SLA check works**: Test returns formatted violations report
- [ ] **SLA unregistration works**: Test updates CosmosDB, scheduled job skips user
- [ ] **Logs confirm tool execution**: Check for "Executing [Tool] operation: [Operation]" messages
- [ ] **No regressions**: Previously working capabilities still work

### Automated Integration Test Script

Consider creating `Tools/test-integration.sh`:
```bash
#!/bin/bash
# Integration test suite for Hermes capabilities

BASE_URL="http://localhost:3978/api/hermes/v1.0/chat"
USER_ID="testuser@microsoft.com"

echo "Testing Newsletter Generation..."
curl -X POST "$BASE_URL" -H "Content-Type: application/json" \
  -d '{"text": "generate a newsletter for epic 123456"}' --max-time 45

echo "Testing SLA Registration..."
curl -X POST "$BASE_URL" -H "Content-Type: application/json" \
  -d "{\"text\": \"register me for SLA notifications\", \"userId\": \"$USER_ID\"}" --max-time 45

echo "Testing SLA Check..."
curl -X POST "$BASE_URL" -H "Content-Type: application/json" \
  -d "{\"text\": \"check my SLA violations\", \"userId\": \"$USER_ID\"}" --max-time 45

echo "Testing SLA Unregistration..."
curl -X POST "$BASE_URL" -H "Content-Type: application/json" \
  -d "{\"text\": \"unregister me from SLA notifications\", \"userId\": \"$USER_ID\"}" --max-time 45

echo "Integration tests complete. Check logs for verification."
```

### Log Monitoring During Tests

**Tail logs in separate terminal:**
```bash
# While app is running, monitor logs
dotnet run --project Hermes | tee hermes.log

# In another terminal, grep for specific patterns
tail -f hermes.log | grep -E "Executing.*Tool|Successfully|error|fail"
```

---

## Common Pitfalls to Avoid

1. ❌ Don't create capabilities that take `string` input and manually parse JSON
   ✅ Use strongly-typed input models

2. ❌ Don't inject `IStorageClient` directly in business logic
   ✅ Use repository abstractions

3. ❌ Don't register capabilities as SingleInstance
   ✅ Always use InstancePerDependency

4. ❌ Don't skip creating interfaces for testability
   ✅ Every major component needs an interface

5. ❌ Don't put multiple capabilities in one class
   ✅ One capability per class (sealed)

6. ❌ Don't use `Task.Result` or `.Wait()`
   ✅ Use async/await throughout

7. ❌ Don't create new Document types without extending Document base
   ✅ All storage models extend Document

8. ❌ Don't hardcode field selections in multiple places
   ✅ Use static dictionaries (see `GetWorkItemTreeCapability.FieldsByType`)

9. ❌ **Don't skip writing unit tests**
   ✅ Every new capability, repository, or client MUST have tests before commit

---

## Context for AI Agents

When working on this codebase:

1. **Read before modifying:** Always read the file you're about to modify
2. **Follow patterns:** Look at existing implementations (e.g., `GetWorkItemTreeCapability`) as reference
3. **Update all layers:** Adding a capability requires changes in 9 places (input, capability, DI, tool, instruction, agentspec, tests, tool tests, run tests)
4. **Mirror test structure:** Test files should mirror main project structure
5. **Write tests BEFORE marking work complete:** Tests are mandatory, not optional
6. **Check git status:** `M Hermes/DI/AgentToolsModule.cs` and `M Hermes/DI/IntegrationsModule.cs` are currently modified

---

## Quick Reference: Where to Find Things

| I need to... | Look in... |
|-------------|-----------|
| Add Azure DevOps operation | `Hermes/Tools/AzureDevOps/Capabilities/` |
| Create new storage entity | `Hermes/Storage/Repositories/{Domain}/` |
| Modify orchestration logic | `Hermes/Orchestrator/HermesOrchestrator.cs` |
| Add REST endpoint | `Hermes/Controllers/` |
| Configure DI | `Hermes/DI/{Layer}Module.cs` |
| Update AI prompts | `Hermes/Resources/Instructions/ProjectAssistant/` |
| Add integration client | `Hermes/Integrations/{ServiceName}/` |
| Modify storage pattern | `Hermes/Storage/Core/` |
| Add unit tests | `Hermes.Tests/{MirrorPath}/` |

---

## Architecture Decisions

**Why capability pattern?**
- Enables isolated testing of individual operations
- Strongly-typed inputs prevent runtime errors
- Easy to add/remove capabilities without affecting others
- Clear separation of concerns

**Why L1/L2 storage?**
- L1 (BitFaster) provides fast in-memory access
- L2 (CosmosDB) provides durability
- Graceful degradation if L1 fails
- Write-through ensures consistency

**Why repository pattern on top of storage clients?**
- Domain-specific operations (e.g., `GetConversationHistoryAsync`)
- Business logic separated from storage mechanics
- Additional validation and transformation

**Why Autofac over built-in DI?**
- Named registrations for L1/L2
- More powerful generic type registration
- Module-based organization

---

## Help & Documentation

- **Full architecture details:** See `README.md`
- **API documentation:** Check XML doc comments in code
- **Test examples:** See `Hermes.Tests/` for patterns

---

_Last updated: 2026-01-13_
