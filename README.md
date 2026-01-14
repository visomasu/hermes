# Hermes - AI-Powered Project Assistant

Hermes is an AI-powered chatbot that integrates with Azure DevOps to generate executive communications, validate work item hierarchies, and assist with project management tasks. It operates as both a Microsoft Teams bot and REST/WebSocket API.

---

## Table of Contents
- [Architecture Overview](#architecture-overview)
- [Class Hierarchy & Conventions](#class-hierarchy--conventions)
  - [Core Abstractions](#core-abstractions)
  - [Storage Layer](#storage-layer)
  - [Tools & Capabilities](#tools--capabilities)
  - [Orchestration Layer](#orchestration-layer)
  - [Integration Layer](#integration-layer)
  - [Channels](#channels)
- [Dependency Injection Structure](#dependency-injection-structure)
- [Naming Conventions](#naming-conventions)
- [Extension Patterns](#extension-patterns)

---

## Architecture Overview

Hermes follows a **layered architecture** with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation Layer (Channels)                               │
│  - Teams Agent                                               │
│  - REST API Controllers                                      │
│  - WebSocket Endpoints                                       │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│  Orchestration Layer                                         │
│  - HermesOrchestrator (IAgentOrchestrator)                   │
│  - AgentPromptComposer (IAgentPromptComposer)                │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│  Tools Layer (Capabilities Pattern)                          │
│  - IAgentTool                                                │
│  - IAgentToolCapability<TInput>                              │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│  Integration Layer                                           │
│  - Azure DevOps Client (IAzureDevOpsWorkItemClient)          │
│  - External Service Integrations                             │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│  Storage Layer (L1/L2 Hierarchical Caching)                  │
│  - IStorageClient<T, TKey>                                   │
│  - IRepository<T> (Domain-specific)                          │
│    ├─ L1: BitFasterStorageClient (In-Memory LRU)            │
│    └─ L2: CosmosDbStorageClient (Persistent)                │
└─────────────────────────────────────────────────────────────┘
```

---

## Class Hierarchy & Conventions

### Core Abstractions

The foundation of Hermes is built on interface-driven design for testability and extensibility.

#### Document Base Class
**Location:** `Hermes/Storage/Core/Models/Document.cs`

All data models inherit from `Document`:

```csharp
public abstract class Document
{
    string Id { get; set; }           // Unique identifier
    string PartitionKey { get; set; }  // NoSQL partition key
    string? Etag { get; set; }         // Concurrency control
    int? TTL { get; set; }             // Time-to-live (default: 8 hours)
}
```

**Convention:** All storage entities must extend `Document` to work with the storage layer.

**Examples:**
- `ConversationHistoryDocument` (Hermes/Storage/Repositories/ConversationHistory/)
- `HermesInstructionsDocument` (Hermes/Storage/Repositories/HermesInstructions/)
- `FileDocument` (Hermes/Storage/Core/Models/)

---

### Storage Layer

The storage layer uses a **hierarchical L1/L2 caching pattern** with generic abstractions.

#### IStorageClient<T, TKey>
**Location:** `Hermes/Storage/Core/IStorageClient.cs`

```csharp
public interface IStorageClient<T, TKey>
{
    Task CreateAsync(T item);
    Task<T?> ReadAsync(TKey key, string partitionKey);
    Task UpdateAsync(TKey key, T item);
    Task DeleteAsync(TKey key, string partitionKey);
    Task<IReadOnlyList<T>?> ReadAllByPartitionKeyAsync(string partitionKey);
}
```

**Convention:** Storage clients are **generic** and work with any `T` that extends `Document`.

#### Implementations

```
IStorageClient<T, string>
├── BitFasterStorageClient<T>           (L1 - In-Memory LRU Cache)
├── CosmosDbStorageClient<T>            (L2 - Persistent NoSQL)
├── FileStorageClient                   (File-based for instructions)
└── HierarchicalStorageClient<T>        (Composite: L1 → L2 fallback)
```

**HierarchicalStorageClient Pattern:**
- **Reads:** Check L1 → On miss, read L2 → Populate L1
- **Writes:** Write to L2 first (durability) → Then update L1
- **Convention:** Always write to L2 before L1 to ensure data persistence

#### Repository Pattern
**Location:** `Hermes/Storage/Repositories/RepositoryBase.cs`

```csharp
public abstract class RepositoryBase<T> : IRepository<T> where T : Document
{
    protected readonly IStorageClient<T, string> _storage;

    // Provides domain-specific operations on top of storage client
    public virtual Task CreateAsync(T entity);
    public virtual Task<T?> ReadAsync(string key, string partitionKey);
    public virtual Task UpdateAsync(string key, T entity);
    public virtual Task DeleteAsync(string key, string partitionKey);
    public virtual Task<IReadOnlyList<T>?> ReadAllByPartitionKeyAsync(string partitionKey);
}
```

**Convention:**
- Repository classes extend `RepositoryBase<T>` and implement domain-specific interfaces
- Repositories add business logic on top of raw storage operations
- Each repository lives in its own namespace: `Hermes.Storage.Repositories.{DomainName}`

**Example Implementation:**

```csharp
// Hermes/Storage/Repositories/ConversationHistory/ConversationHistoryRepository.cs
public class ConversationHistoryRepository
    : RepositoryBase<ConversationHistoryDocument>,
      IConversationHistoryRepository
{
    public ConversationHistoryRepository(
        IStorageClient<ConversationHistoryDocument, string> storage)
        : base(storage)
    {
    }

    // Domain-specific methods
    public async Task<string?> GetConversationHistoryAsync(string conversationId);
    public async Task WriteConversationHistoryAsync(string conversationId,
                                                    List<ConversationMessage> history);
}
```

**Storage Hierarchy:**
```
IRepository<T>
└── RepositoryBase<T>
    ├── ConversationHistoryRepository : IConversationHistoryRepository
    ├── HermesInstructionsRepository : IHermesInstructionsRepository
    └── SampleRepository : IRepository<SampleRepositoryModel>
```

---

### Tools & Capabilities

Hermes uses a **capability-based architecture** where tools expose multiple independent capabilities.

#### IAgentTool
**Location:** `Hermes/Tools/IAgentTool.cs`

```csharp
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<string> Capabilities { get; }

    string GetMetadata();
    Task<string> ExecuteAsync(string operation, string input);
}
```

**Convention:** Tools are **facades** that route operations to specific capabilities.

#### IAgentToolCapability<TInput>
**Location:** `Hermes/Tools/IAgentToolCapability.cs`

```csharp
public interface IAgentToolCapability<TInput> where TInput : class
{
    string Name { get; }
    string Description { get; }
    Task<string> ExecuteAsync(TInput input);
}
```

**Convention:**
- Each capability is a **self-contained, strongly-typed** operation
- Input models extend `ToolCapabilityInputBase`
- Capabilities are registered individually in DI for isolated testing
- Output is always JSON string

#### Capability Input Base
**Location:** `Hermes/Tools/Models/ToolCapabilityInputBase.cs`

```csharp
public abstract class ToolCapabilityInputBase
{
    string CorrelationId { get; set; }
}
```

**Convention:** All capability inputs inherit from `ToolCapabilityInputBase` for common metadata.

#### Tool Hierarchy Example

```
IAgentTool
└── AzureDevOpsTool
    ├── IAgentToolCapability<GetWorkItemTreeCapabilityInput>
    │   └── GetWorkItemTreeCapability
    │       ├── Input: GetWorkItemTreeCapabilityInput
    │       ├── Operation: Retrieves hierarchical work item tree
    │       └── Output: JSON string
    │
    └── IAgentToolCapability<GetWorkItemsByAreaPathCapabilityInput>
        └── GetWorkItemsByAreaPathCapability
            ├── Input: GetWorkItemsByAreaPathCapabilityInput
            ├── Operation: Retrieves work items by area path with pagination
            └── Output: JSON string
```

**File Structure:**
```
Hermes/Tools/
├── IAgentTool.cs
├── IAgentToolCapability.cs
├── Models/
│   └── ToolCapabilityInputBase.cs
└── AzureDevOps/
    ├── AzureDevOpsTool.cs                           (Tool facade)
    ├── Capabilities/
    │   ├── GetWorkItemTreeCapability.cs
    │   ├── GetWorkItemsByAreaPathCapability.cs
    │   └── Inputs/
    │       ├── GetWorkItemTreeCapabilityInput.cs
    │       └── GetWorkItemsByAreaPathCapabilityInput.cs
```

**Example Capability Implementation:**

```csharp
// Hermes/Tools/AzureDevOps/Capabilities/GetWorkItemTreeCapability.cs
public sealed class GetWorkItemTreeCapability
    : IAgentToolCapability<GetWorkItemTreeCapabilityInput>
{
    private readonly IAzureDevOpsWorkItemClient _client;

    public string Name => "GetWorkItemTree";
    public string Description => "Retrieves a hierarchical tree of work items";

    public async Task<string> ExecuteAsync(GetWorkItemTreeCapabilityInput input)
    {
        var tree = await BuildWorkItemTreeAsync(input.WorkItemId, input.Depth);
        return JsonSerializer.Serialize(tree);
    }
}
```

---

### Orchestration Layer

The orchestration layer coordinates between the AI agent, tools, and storage.

#### IAgentOrchestrator
**Location:** `Hermes/Orchestrator/IAgentOrchestrator.cs`

```csharp
public interface IAgentOrchestrator
{
    Task<string> OrchestrateAsync(string sessionId, string query);
}
```

**Implementation:** `HermesOrchestrator`
- Loads conversation history from repository
- Builds agent prompts via `IAgentPromptComposer`
- Invokes Azure OpenAI agent with registered tools
- Persists conversation history
- Returns agent response

#### IAgentPromptComposer
**Location:** `Hermes/Orchestrator/Prompts/IAgentPromptComposer.cs`

```csharp
public interface IAgentPromptComposer
{
    Task<string> ComposeAsync(string instructionType);
}
```

**Implementation:** `AgentPromptComposer`
- Loads base instructions from file storage
- Loads capability-specific instructions
- Merges with agentspec.json (capability manifest)
- Produces final prompt for the AI agent

**Orchestration Flow:**

```
User Query
    ↓
HermesOrchestrator.OrchestrateAsync()
    ↓
┌───────────────────────────────────────┐
│ 1. Load conversation history          │ ← IConversationHistoryRepository
│ 2. Build agent prompt                 │ ← IAgentPromptComposer
│ 3. Get or create AI agent (cached)    │ ← Azure OpenAI SDK
│ 4. Execute agent with tools           │ ← IAgentTool[]
│ 5. Persist updated history            │ → IConversationHistoryRepository
└───────────────────────────────────────┘
    ↓
Response
```

---

### Integration Layer

External service clients live in the integration layer.

#### IAzureDevOpsWorkItemClient
**Location:** `Hermes/Integrations/AzureDevOps/IAzureDevOpsWorkItemClient.cs`

```csharp
public interface IAzureDevOpsWorkItemClient
{
    Task<string> GetWorkItemAsync(int id, IEnumerable<string>? fields = null);
    Task<string> GetWorkItemsByAreaPathAsync(string areaPath, int pageSize, int skip);
    // Other Azure DevOps operations...
}
```

**Implementation:** `AzureDevOpsWorkItemClient`
- Wraps Microsoft.TeamFoundationServer.Client SDK
- Handles authentication (PAT-based)
- Converts responses to JSON strings

**Convention:**
- Integration clients live in `Hermes/Integrations/{ServiceName}/`
- Each client has an interface for testability
- Clients return JSON strings for consistency with tool layer

**Integration Hierarchy:**
```
Hermes/Integrations/
└── AzureDevOps/
    ├── IAzureDevOpsWorkItemClient.cs
    └── AzureDevOpsWorkItemClient.cs
```

---

### Channels

Channels are entry points for user interaction.

#### Teams Channel
**Location:** `Hermes/Channels/Teams/`

```csharp
public class HermesTeamsAgent : IAgentWorker
{
    public async Task<AgentResponse> InvokeAsync(AgentCallContext context);
}
```

**Convention:** Teams agents implement `IAgentWorker` from Microsoft.Agents SDK.

#### REST API
**Location:** `Hermes/Controllers/`

```csharp
[ApiController]
[Route("api/[controller]")]
public class HermesController : ControllerBase
{
    [HttpPost("ask")]
    public async Task<IActionResult> AskAsync([FromBody] HermesQueryRequest request);
}
```

**Convention:** Controllers follow standard ASP.NET Core conventions.

---

## Dependency Injection Structure

Hermes uses **Autofac** for dependency injection with modular registration.

### Module Hierarchy

```
HermesModule (Parent)
├── StorageModule
│   ├── BitFasterStorageClient<T> (L1, named "l1")
│   ├── CosmosDbStorageClient<T> (L2, named "l2")
│   ├── HierarchicalStorageClient<T> (default IStorageClient)
│   ├── FileStorageClient (named "file")
│   └── Repositories
│       ├── ConversationHistoryRepository
│       ├── HermesInstructionsRepository
│       └── SampleRepository
│
├── IntegrationsModule
│   └── AzureDevOpsWorkItemClient
│
├── AgentToolsModule
│   ├── GetWorkItemTreeCapability
│   ├── GetWorkItemsByAreaPathCapability
│   └── AzureDevOpsTool
│
└── TeamsModule
    └── HermesTeamsAgent
```

### DI Registration Patterns

#### Generic Storage Client Registration

```csharp
// L1 Cache (Named registration)
builder.RegisterGeneric(typeof(BitFasterStorageClient<>))
    .Named("l1", typeof(IStorageClient<,>))
    .SingleInstance();

// L2 Persistence (Named registration)
builder.RegisterGeneric(typeof(CosmosDbStorageClient<>))
    .Named("l2", typeof(IStorageClient<,>))
    .SingleInstance();

// Hierarchical (Default registration, composes L1 + L2)
builder.RegisterGeneric(typeof(HierarchicalStorageClient<>))
    .As(typeof(IStorageClient<,>))
    .SingleInstance();
```

#### Capability Registration

```csharp
// Each capability registered individually for isolation
builder.RegisterType<GetWorkItemTreeCapability>()
    .As<IAgentToolCapability<GetWorkItemTreeCapabilityInput>>()
    .AsSelf()
    .InstancePerDependency();
```

#### Repository with Named Storage

```csharp
// Repository using file storage (not hierarchical)
builder.RegisterType<HermesInstructionsRepository>()
    .As<IHermesInstructionsRepository>()
    .WithParameter(
        (pi, ctx) => pi.ParameterType == typeof(IStorageClient<FileDocument, string>),
        (pi, ctx) => ctx.ResolveNamed("file", typeof(IStorageClient<FileDocument, string>)))
    .SingleInstance();
```

**Convention:**
- **SingleInstance:** Services that are expensive to create or maintain state (storage clients, repositories, tools)
- **InstancePerDependency:** Lightweight, stateless operations (capabilities)
- **Named registrations:** Used when multiple implementations of same interface exist (L1, L2, file storage)

---

## Naming Conventions

### Classes and Interfaces

| Type | Pattern | Example |
|------|---------|---------|
| Interface | `I{Name}` | `IStorageClient`, `IAgentTool` |
| Implementation | `{Name}` | `CosmosDbStorageClient`, `AzureDevOpsTool` |
| Base Class | `{Name}Base` | `RepositoryBase<T>`, `ToolCapabilityInputBase` |
| Document Model | `{Domain}Document` | `ConversationHistoryDocument` |
| Repository | `{Domain}Repository` | `ConversationHistoryRepository` |
| Capability | `{Operation}Capability` | `GetWorkItemTreeCapability` |
| Capability Input | `{Capability}Input` | `GetWorkItemTreeCapabilityInput` |
| DI Module | `{Layer}Module` | `StorageModule`, `AgentToolsModule` |

### Namespaces

```
Hermes
├── Orchestrator                    // Orchestration logic
│   └── Prompts                     // Prompt composition
├── Storage
│   ├── Core                        // Storage abstractions
│   │   ├── Models                  // Document base classes
│   │   ├── CosmosDB                // CosmosDB implementation
│   │   ├── InMemory                // In-memory cache implementation
│   │   ├── File                    // File storage implementation
│   │   └── Exceptions              // Storage exceptions
│   └── Repositories                // Domain repositories
│       ├── {Domain}                // Each repository in own folder
│       │   ├── {Domain}Repository.cs
│       │   ├── I{Domain}Repository.cs
│       │   └── {Domain}Document.cs
├── Tools                           // Agent tools
│   ├── {ToolName}                  // Each tool in own folder
│   │   ├── {ToolName}Tool.cs
│   │   ├── Capabilities            // Capability implementations
│   │   │   ├── {Operation}Capability.cs
│   │   │   └── Inputs              // Capability input models
│   │   │       └── {Operation}Input.cs
│   └── Models                      // Shared tool models
├── Integrations                    // External service clients
│   └── {ServiceName}
│       ├── I{ServiceName}Client.cs
│       └── {ServiceName}Client.cs
├── Channels                        // User-facing channels
│   └── Teams
├── Controllers                     // REST API controllers
├── Authentication                  // Auth middleware
└── DI                              // Autofac modules
```

### Private Methods

**Convention:** Private helper methods are prefixed with underscore:

```csharp
private void _ValidateEntity(T entity);
private Task<JsonElement> _BuildWorkItemTreeAsync(int id, int depth);
```

---

## Extension Patterns

### Adding a New Capability

1. **Create Input Model:**
   ```csharp
   // Hermes/Tools/AzureDevOps/Capabilities/Inputs/MyNewCapabilityInput.cs
   public class MyNewCapabilityInput : ToolCapabilityInputBase
   {
       public int WorkItemId { get; set; }
       public string Filter { get; set; } = string.Empty;
   }
   ```

2. **Implement Capability:**
   ```csharp
   // Hermes/Tools/AzureDevOps/Capabilities/MyNewCapability.cs
   public sealed class MyNewCapability
       : IAgentToolCapability<MyNewCapabilityInput>
   {
       private readonly IAzureDevOpsWorkItemClient _client;

       public string Name => "MyNewOperation";
       public string Description => "Does something with work items";

       public async Task<string> ExecuteAsync(MyNewCapabilityInput input)
       {
           // Implementation
           return JsonSerializer.Serialize(result);
       }
   }
   ```

3. **Register in DI:**
   ```csharp
   // Hermes/DI/AgentToolsModule.cs
   builder.RegisterType<MyNewCapability>()
       .As<IAgentToolCapability<MyNewCapabilityInput>>()
       .AsSelf()
       .InstancePerDependency();
   ```

4. **Add to Tool:**
   ```csharp
   // Hermes/Tools/AzureDevOps/AzureDevOpsTool.cs
   private readonly MyNewCapability _myNewCapability;

   // Add to Capabilities property and route in ExecuteAsync
   ```

5. **Create Instruction File:**
   ```
   Hermes/Resources/Instructions/ProjectAssistant/Capabilities/MyNewOperation.txt
   ```

6. **Update agentspec.json:**
   ```json
   {
     "capabilities": [
       {
         "name": "MyNewOperation",
         "description": "Does something with work items"
       }
     ]
   }
   ```

### Adding a New Repository

1. **Create Document Model:**
   ```csharp
   // Hermes/Storage/Repositories/MyDomain/MyDomainDocument.cs
   public class MyDomainDocument : Document
   {
       public string MyProperty { get; set; } = string.Empty;
   }
   ```

2. **Create Repository Interface:**
   ```csharp
   // Hermes/Storage/Repositories/MyDomain/IMyDomainRepository.cs
   public interface IMyDomainRepository : IRepository<MyDomainDocument>
   {
       Task<MyDomainDocument?> GetByCustomFieldAsync(string field);
   }
   ```

3. **Implement Repository:**
   ```csharp
   // Hermes/Storage/Repositories/MyDomain/MyDomainRepository.cs
   public class MyDomainRepository
       : RepositoryBase<MyDomainDocument>,
         IMyDomainRepository
   {
       public MyDomainRepository(
           IStorageClient<MyDomainDocument, string> storage)
           : base(storage)
       {
       }

       public async Task<MyDomainDocument?> GetByCustomFieldAsync(string field)
       {
           // Custom implementation
       }
   }
   ```

4. **Register in DI:**
   ```csharp
   // Hermes/DI/StorageModule.cs
   builder.RegisterType<MyDomainRepository>()
       .As<IMyDomainRepository>()
       .SingleInstance();
   ```

### Adding a New Integration Client

1. **Create Interface:**
   ```csharp
   // Hermes/Integrations/MyService/IMyServiceClient.cs
   public interface IMyServiceClient
   {
       Task<string> GetDataAsync(string id);
   }
   ```

2. **Implement Client:**
   ```csharp
   // Hermes/Integrations/MyService/MyServiceClient.cs
   public class MyServiceClient : IMyServiceClient
   {
       private readonly HttpClient _httpClient;

       public async Task<string> GetDataAsync(string id)
       {
           // Implementation using external SDK or HttpClient
       }
   }
   ```

3. **Register in DI:**
   ```csharp
   // Hermes/DI/IntegrationsModule.cs
   builder.RegisterType<MyServiceClient>()
       .As<IMyServiceClient>()
       .SingleInstance();
   ```

---

## Key Design Principles

1. **Interface-Driven:** All major components have interfaces for testability
2. **Generic Where Possible:** Storage layer uses generics to avoid duplication
3. **Separation of Concerns:** Clear boundaries between layers
4. **Dependency Injection:** All dependencies injected, no `new` in business logic
5. **Capability Pattern:** Tools composed of independent, testable capabilities
6. **L1/L2 Caching:** Hierarchical storage for performance and resilience
7. **Repository Pattern:** Domain logic separated from storage mechanics
8. **Single Responsibility:** Each class has one clear purpose

---

## Technology Stack

- **.NET 8.0** - Runtime and web framework
- **ASP.NET Core** - Web API and middleware
- **Autofac 10.0** - Dependency injection container
- **Azure OpenAI 2.1.0** - LLM integration (GPT-5 Mini)
- **Microsoft.Agents.*** - Bot framework for Teams
- **Azure Cosmos DB 3.54.0** - NoSQL persistence (L2)
- **BitFaster.Caching 2.5.4** - High-performance LRU cache (L1)
- **Microsoft.TeamFoundationServer.Client 19.225.1** - Azure DevOps SDK
- **xUnit 2.9.3 + Moq 4.20.72** - Testing framework

---

## Getting Started for Developers

### Prerequisites
- .NET 8.0 SDK
- Azure Cosmos DB Emulator (for local development)
- Azure DevOps account with PAT
- Azure OpenAI endpoint and API key

### Running Locally
1. Start Cosmos DB Emulator
2. Configure `appsettings.Development.json` or use hardcoded dev values
3. Run: `dotnet run --project Hermes`

### Running Tests
```bash
dotnet test Hermes.Tests
```

### Key Entry Points
- **REST API:** `HermesController.AskAsync()` - Main query endpoint
- **Teams Bot:** `HermesTeamsAgent.InvokeAsync()` - Teams message handler
- **Orchestration:** `HermesOrchestrator.OrchestrateAsync()` - Core AI orchestration

---

## Contributing

When adding new features:
1. Follow the naming conventions outlined above
2. Implement interfaces for all major components
3. Add capabilities (not monolithic methods) for new operations
4. Write unit tests using xUnit + Moq
5. Update this README if adding new patterns or layers

---

## License

[Specify your license here]
