# Log Capture Setup for Hermes.Evals

This document explains how to configure Hermes to write logs so that Hermes.Evals can capture tool invocation metadata.

---

## How It Works

**Hermes.Evals** extracts tool metadata (tool name, operation, parameters) from structured logs written by the Hermes API. The evaluation framework uses `LogParser` to parse logs by session ID and correlate tool invocations with API requests.

**Log Format Expected:**
```
[OrchestrationStart] SessionId=session-1 UserId=testuser@microsoft.com QueryLength=41
[ToolInvocation] Tool=AzureDevOpsTool Operation=GetWorkItemTree Input={"workItemId":3097408,"depth":3}
```

---

## Step 1: Configure Hermes to Write Log Files

### **Option A: Using Serilog File Sink (Recommended)**

1. **Add Serilog.Sinks.File** to Hermes project:
   ```bash
   cd Hermes
   dotnet add package Serilog.Sinks.File
   dotnet add package Serilog.Extensions.Logging
   ```

2. **Update `Program.cs`** to configure file logging:
   ```csharp
   using Serilog;
   using Serilog.Events;

   // Configure Serilog before building the host
   Log.Logger = new LoggerConfiguration()
       .MinimumLevel.Debug()
       .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
       .Enrich.FromLogContext()
       .WriteTo.Console()
       .WriteTo.File(
           path: Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
               ".hermes", "logs", $"hermes-.log"),
           rollingInterval: RollingInterval.Day,
           outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
       .CreateLogger();

   builder.Logging.ClearProviders();
   builder.Logging.AddSerilog();
   ```

3. **Verify log file location:**
   - Windows: `C:\Users\<YourUsername>\.hermes\logs\hermes-20260130.log`
   - Linux/Mac: `~/.hermes/logs/hermes-20260130.log`

### **Option B: Using Built-in File Logging Provider**

1. **Add Microsoft.Extensions.Logging.Console**:
   ```bash
   cd Hermes
   dotnet add package Microsoft.Extensions.Logging.File --version 2.3.0
   ```

2. **Update `appsettings.json`**:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Hermes": "Debug"
       },
       "File": {
         "Path": "%USERPROFILE%\\.hermes\\logs\\hermes-{Date}.log",
         "MinLevel": "Debug"
       }
     }
   }
   ```

---

## Step 2: Configure Hermes.Evals to Read Logs

### **Default Behavior**

By default, `ConversationRunner` expects logs at:
```
~/.hermes/logs/hermes-YYYYMMDD.log
```

### **Custom Log Path (via Environment Variable)**

Set the `HERMES_LOG_PATH` environment variable:

**Windows (PowerShell):**
```powershell
$env:HERMES_LOG_PATH = "C:\custom\path\hermes.log"
dotnet run --project Hermes.Evals
```

**Linux/Mac (Bash):**
```bash
export HERMES_LOG_PATH="/custom/path/hermes.log"
dotnet run --project Hermes.Evals
```

### **Programmatic Configuration**

Update `Program.cs` in Hermes.Evals:
```csharp
services.AddSingleton<ConversationRunner>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("HermesApi");
    var evaluatorOrchestrator = sp.GetRequiredService<EvaluatorOrchestrator>();
    var logParser = sp.GetRequiredService<LogParser>();
    var logger = sp.GetRequiredService<ILogger<ConversationRunner>>();

    // Custom log file path
    var logFilePath = @"C:\dev\repos\Hermes\logs\hermes.log";

    return new ConversationRunner(httpClient, evaluatorOrchestrator, logParser, logger, logFilePath);
});
```

---

## Step 3: Verify Log Capture

### **Test Structured Logging**

1. **Start Hermes API:**
   ```bash
   cd Hermes
   dotnet run
   ```

2. **Make a test request:**
   ```bash
   curl -X POST "http://localhost:3978/api/hermes/v1.0/chat" \
     -H "Content-Type: application/json" \
     -d '{"text": "generate a newsletter for feature 3097408", "userId": "testuser@microsoft.com"}'
   ```

3. **Check log file:**
   ```bash
   # Windows (PowerShell)
   Get-Content "$env:USERPROFILE\.hermes\logs\hermes-$(Get-Date -Format 'yyyyMMdd').log" | Select-String -Pattern "\[ToolInvocation\]"

   # Linux/Mac (Bash)
   grep "\[ToolInvocation\]" ~/.hermes/logs/hermes-$(date +%Y%m%d).log
   ```

4. **Expected output:**
   ```
   [ToolInvocation] Tool=AzureDevOpsTool Operation=GetWorkItemTree Input={"workItemId":3097408,"depth":3}
   ```

---

## Step 4: Run Evaluation

Once logging is configured:

```bash
cd Hermes.Evals
dotnet run
```

**What happens:**
1. Hermes.Evals loads scenarios from `Scenarios/Definitions/`
2. For each turn, it sends HTTP request to Hermes API
3. Hermes writes structured logs to log file
4. `ConversationRunner` parses log file by session ID
5. `LogParser` extracts tool name, operation, and parameters
6. Evaluators score tool selection, parameter extraction, etc.
7. Reports are generated in `Output/` directory

---

## Troubleshooting

### **Problem: "Log file not found"**

**Cause:** Hermes is not writing logs to the expected location.

**Solution:**
1. Check Hermes console output - logs should be written
2. Verify log file path configuration in Hermes `Program.cs`
3. Ensure directory exists and is writable
4. Set `HERMES_LOG_PATH` environment variable

### **Problem: "No tool invocation metadata found in logs"**

**Cause:** Logs don't contain structured `[ToolInvocation]` entries.

**Solution:**
1. Verify structured logging is enabled in `HermesOrchestrator.cs` (should already be done)
2. Check log file contains entries like:
   ```
   [OrchestrationStart] SessionId=...
   [ToolInvocation] Tool=... Operation=... Input=...
   ```
3. Ensure session ID in logs matches session ID used by evaluator

### **Problem: "Session ID mismatch"**

**Cause:** Session ID format differs between Hermes API and log parser.

**Solution:**
- Hermes Controller uses format: `userId|sessionId` (e.g., `testuser@microsoft.com|session-1`)
- LogParser extracts the part after `|` (e.g., `session-1`)
- `ConversationRunner` handles this automatically (lines 268-273)

### **Problem: "Logs not available immediately after API call"**

**Cause:** Log buffering or delayed file writes.

**Solution:**
- `ConversationRunner` includes retry logic (3 attempts, 500ms delay)
- Increase `maxRetries` or `delayMs` in `_ParseLogsWithRetryAsync` if needed
- Flush logs immediately by configuring Serilog with `flushToDiskInterval: TimeSpan.Zero`

---

## Performance Considerations

### **Log File Size**

Structured logs can grow large with many evaluations. Options:

1. **Rolling file strategy** (recommended):
   ```csharp
   .WriteTo.File(path, rollingInterval: RollingInterval.Day)
   ```

2. **Retention policy**:
   ```csharp
   .WriteTo.File(path, retainedFileCountLimit: 7) // Keep 7 days
   ```

3. **Size-based rolling**:
   ```csharp
   .WriteTo.File(path, fileSizeLimitBytes: 10_000_000) // 10 MB
   ```

### **Parsing Performance**

`LogParser` reads entire log file on each turn. For large files:

1. Use `grep`/`Select-String` to pre-filter logs by session ID
2. Implement incremental parsing (track file position between turns)
3. Use memory-mapped files for very large logs (>100MB)

---

## Advanced: Distributed Logging

For production scenarios with multiple Hermes instances:

### **Option 1: Centralized Log Storage**

Configure Hermes to write logs to Azure Application Insights, Seq, or Elasticsearch:

```csharp
.WriteTo.ApplicationInsights(telemetryConfiguration, TelemetryConverter.Traces)
```

Then update `ConversationRunner` to query centralized logs via API.

### **Option 2: Correlation Headers**

Add custom response headers to Hermes API (alternative to log parsing):

```csharp
// In HermesController.cs
Response.Headers.Add("X-Tool-Name", "AzureDevOpsTool");
Response.Headers.Add("X-Tool-Operation", "GetWorkItemTree");
Response.Headers.Add("X-Tool-Parameters", JsonSerializer.Serialize(parameters));
```

`ConversationRunner` already supports this as a fallback (lines 218-260).

---

## Summary Checklist

- [ ] Hermes writes logs to `~/.hermes/logs/hermes-YYYYMMDD.log`
- [ ] Logs contain `[OrchestrationStart]` and `[ToolInvocation]` entries
- [ ] Session ID format is consistent (`userId|sessionId` → `sessionId`)
- [ ] Hermes.Evals can read the log file (permissions OK)
- [ ] Test with `curl` request and verify logs are parseable
- [ ] Run `dotnet run` in Hermes.Evals and check output

---

**Next Steps:**
- See `NEWSLETTER-EVALUATION.md` for running your first evaluation
- See `CLAUDE.md` for adding new evaluation scenarios
