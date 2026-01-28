# Hermes Native Launch Guide

## Overview

This guide covers the native (non-Docker) launch approach for running Hermes locally on Windows. The native approach was chosen for its simplicity and direct access to Azure CLI credentials without the complexity of Docker volume mounting or token pass-through mechanisms.

## Architecture

### Port Configuration

Hermes supports running multiple instances simultaneously on different ports:

| Environment | Port | Configuration | ASP.NET Environment |
|------------|------|---------------|-------------------|
| Development | 3978 | Debug | Development |
| Production | 3979 | Release | Production |

This allows developers to:
- Run dev and prod configurations side-by-side
- Test production behavior locally before deployment
- Compare behavior across environments
- Maintain separate Agents Playground connections

### Launch Script Architecture

The launch scripts follow this sequence:

```
1. Check/Start Cosmos DB Emulator
   ├─ Detect if already running
   ├─ Start if needed (/NoExplorer /NoUI flags)
   ├─ Wait for readiness (max 60 seconds)
   └─ Health check: https://localhost:8081/_explorer/emulator.pem

2. Verify Azure CLI Authentication
   ├─ Run: az account show
   ├─ Display current user
   └─ Warn if not authenticated (non-blocking)

3. Launch Agents Playground (unless -NoPlayground)
   ├─ Check if agentsplayground command exists
   ├─ Start in new PowerShell window
   ├─ Connect to correct backend port
   └─ Use emulator configuration

4. Start Hermes Backend
   ├─ Navigate to Hermes/ directory
   ├─ Run dotnet with appropriate flags
   └─ Use correct port and environment
```

## Launch Scripts

### start-dev.ps1

**Purpose**: Launch Hermes in Development mode on port 3978

**Parameters**:
- `-NoBuild`: Skip building, use existing binaries
- `-Watch`: Enable hot-reload on file changes (dotnet watch)
- `-NoPlayground`: Skip launching Agents Playground

**Usage Examples**:
```powershell
# Standard development launch
.\start-dev.ps1

# Watch mode for active development
.\start-dev.ps1 -Watch

# Quick restart without rebuild
.\start-dev.ps1 -NoBuild

# Backend only (no playground)
.\start-dev.ps1 -NoPlayground

# Watch mode, backend only
.\start-dev.ps1 -Watch -NoPlayground
```

**What It Does**:
1. Starts Cosmos DB Emulator if needed
2. Checks Azure CLI authentication
3. Launches Agents Playground connected to `http://localhost:3978/api/messages`
4. Runs: `dotnet run --urls "http://localhost:3978"` (or `dotnet watch run` with `-Watch`)

### start-prod.ps1

**Purpose**: Launch Hermes in Production mode on port 3979

**Parameters**:
- `-NoBuild`: Skip building, use existing Release binaries
- `-NoPlayground`: Skip launching Agents Playground

**Usage Examples**:
```powershell
# Standard production launch
.\start-prod.ps1

# Quick restart without rebuild
.\start-prod.ps1 -NoBuild

# Backend only (no playground)
.\start-prod.ps1 -NoPlayground
```

**What It Does**:
1. Starts Cosmos DB Emulator if needed
2. Checks Azure CLI authentication
3. Launches Agents Playground connected to `http://localhost:3979/api/messages`
4. Runs: `dotnet run --configuration Release --urls "http://localhost:3979" --environment Production`

### Batch File Wrappers

For convenience, batch file wrappers allow double-click launching:

- **start-dev.bat**: Executes `start-dev.ps1` with PowerShell
- **start-prod.bat**: Executes `start-prod.ps1` with PowerShell

These automatically handle PowerShell execution policy and pass through any command-line arguments.

## Dependencies

### Cosmos DB Emulator

**Installation**:
- Download: https://aka.ms/cosmosdb-emulator
- Default path: `C:\Program Files\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe`

**Launch Script Behavior**:
- Detects running instance via process name: `CosmosDB.Emulator`
- Starts with flags: `/NoExplorer /NoUI` (no browser window, hidden UI)
- Window style: Hidden (runs in background)
- Waits up to 60 seconds for readiness
- Health check: Polls `https://localhost:8081/_explorer/emulator.pem` every 2 seconds
- Continues even if not fully ready (shows warning)

**Manual Verification**:
```powershell
# Check if running
Get-Process -Name "CosmosDB.Emulator" -ErrorAction SilentlyContinue

# Open Cosmos DB Explorer
Start-Process "https://localhost:8081/_explorer/index.html"
```

### Azure CLI

**Installation**:
- Download: https://aka.ms/installazurecliwindows
- Verify: `az --version`

**Authentication**:
```bash
# Standard login
az login

# Device code login (if tenant blocks standard login)
az login --use-device-code
```

**Launch Script Behavior**:
- Runs: `az account show 2>$null | ConvertFrom-Json`
- Displays current authenticated user
- Shows warning if not authenticated
- **Non-blocking**: Script continues even if auth fails

**Why Non-Blocking?**
Authentication failures are caught at runtime when making Azure DevOps or Microsoft Graph API calls. This allows:
- Backend to start even if credentials are temporarily unavailable
- Developers to authenticate after startup
- Testing of non-authenticated endpoints

### Agents Playground

**Installation Options**:
```bash
# Option 1: WinGet (recommended)
winget install agentsplayground

# Option 2: npm
npm install -g @microsoft/m365agentsplayground
```

**Verification**:
```powershell
Get-Command agentsplayground
```

**Launch Script Behavior**:
- Checks if `agentsplayground` command exists
- If not installed: Shows warning, continues without it
- If installed: Launches in new PowerShell window with:
  - `-e "<endpoint>"`: Backend endpoint URL
  - `-c "emulator"`: Use emulator configuration
- Window remains open (`-NoExit` flag)

**Manual Launch**:
```powershell
# Development
agentsplayground -e "http://localhost:3978/api/messages" -c "emulator"

# Production
agentsplayground -e "http://localhost:3979/api/messages" -c "emulator"
```

## Authentication Architecture

### DefaultAzureCredential Chain

Hermes uses `DefaultAzureCredential` which tries authentication methods in order:

1. **EnvironmentCredential**: Environment variables
2. **ManagedIdentityCredential**: Azure Managed Identity (production)
3. **AzureCliCredential**: Azure CLI (`az login`) - **Used for local development**
4. **VisualStudioCredential**: Visual Studio credentials

For local development, the Azure CLI credential is the primary method.

### Services Requiring Authentication

1. **Azure DevOps** (`IAzureDevOpsWorkItemClient`)
   - Resource: `499b84ac-1321-427f-aa17-267ca6975798`
   - Used for: Work item queries, hierarchy validation, newsletter generation

2. **Microsoft Graph** (`IMicrosoftGraphClient`)
   - Resource: `https://graph.microsoft.com/.default`
   - Used for: User profiles, direct reports (SLA notifications)

3. **Azure OpenAI** (`IAzureOpenAIEmbeddingClient`)
   - Resource: Azure OpenAI endpoint
   - Used for: Embeddings, conversation context

### Authentication Verification

Use the diagnostics endpoints to verify authentication:

```bash
# Full authentication test (all services)
curl http://localhost:3978/api/diagnostics/auth-test

# Quick Azure CLI status
curl http://localhost:3978/api/diagnostics/az-cli-status
```

See `Hermes/Controllers/DiagnosticsController.cs` for implementation details.

## Running Multiple Instances

### Simultaneous Dev and Prod

Run both environments at once for comparison testing:

```powershell
# Terminal 1: Development
.\start-dev.ps1

# Terminal 2: Production
.\start-prod.ps1
```

Each instance:
- Runs on its own port (3978 vs 3979)
- Has separate Agents Playground window
- Uses same Cosmos DB Emulator (shared data)
- Uses same Azure CLI credentials

### Shared vs Isolated Resources

**Shared**:
- Cosmos DB Emulator (same instance, same data)
- Azure CLI credentials (same authenticated user)
- Azure DevOps organization/project

**Isolated**:
- Backend process (separate dotnet process)
- Agents Playground window (separate UI)
- Configuration files (Development vs Production settings)
- Log files (if file logging enabled)

## Configuration Files

### appsettings.json (Development)

Located: `Hermes/appsettings.json`

Key sections:
```json
{
  "OpenAI": {
    "Endpoint": "https://your-openai.openai.azure.com/",
    "Model": "gpt-4o",
    "EmbeddingModel": "text-embedding-3-small"
  },
  "AzureDevOps": {
    "Organization": "your-org",
    "Project": "your-project"
  },
  "Cosmos": {
    "Endpoint": "https://localhost:8081",
    "DatabaseId": "HermesDB"
  },
  "Scheduling": {
    "EnableScheduler": false
  }
}
```

### appsettings.Production.json (Production)

Located: `Hermes/appsettings.Production.json`

Overrides for production environment (same structure as Development).

### Environment Variables

The launch scripts set:
- `ASPNETCORE_ENVIRONMENT`: "Development" or "Production"
- `ASPNETCORE_URLS`: "http://localhost:3978" or "http://localhost:3979"

## Troubleshooting

### Cosmos DB Emulator Issues

**Error: CosmosDB.Emulator.exe not found**
- Verify installation: Check `C:\Program Files\Azure Cosmos DB Emulator\`
- Reinstall: https://aka.ms/cosmosdb-emulator
- Check script path: Update `$cosmosPath` in launch script if installed elsewhere

**Error: Cosmos DB not ready after 60 seconds**
- Script continues with warning
- Manually verify: Open https://localhost:8081/_explorer/index.html
- First launch: May take longer than 60 seconds
- Check logs: `%LOCALAPPDATA%\CosmosDBEmulator\`

**Error: Certificate trust issues**
```powershell
# Export and trust certificate
PowerShell -ExecutionPolicy Unrestricted -File "C:\Program Files\Azure Cosmos DB Emulator\ExportCerts.ps1"
```

### Azure CLI Authentication Issues

**Error: az: command not found**
- Install Azure CLI: https://aka.ms/installazurecliwindows
- Restart terminal after installation

**Error: Not authenticated**
```bash
az login
```

**Error: Tenant blocks command-line login**
- Use device code flow: `az login --use-device-code`
- Contact tenant admin for authentication exceptions

**Error: Token expired**
```bash
# Refresh tokens
az account get-access-token --resource https://graph.microsoft.com/
```

### Agents Playground Issues

**Error: agentsplayground command not found**
```bash
# Install via WinGet
winget install agentsplayground

# OR install via npm
npm install -g @microsoft/m365agentsplayground
```

**Error: Connection refused**
- Ensure Hermes backend is running
- Check port: `http://localhost:3978` or `http://localhost:3979`
- Verify in browser: Navigate to backend URL

**Error: Window closes immediately**
- This is normal for `-NoPlayground` flag
- Otherwise, check agentsplayground installation

### Port Conflict Issues

**Error: Address already in use**
```powershell
# Find process using port 3978
netstat -ano | findstr :3978

# Kill process by PID
taskkill /PID <pid> /F
```

### Build Issues

**Error: Build failed**
```bash
# Clean and rebuild
dotnet clean Hermes/Hermes.csproj
dotnet build Hermes/Hermes.csproj

# For production
dotnet build Hermes/Hermes.csproj --configuration Release
```

**Error: Missing dependencies**
```bash
# Restore NuGet packages
dotnet restore Hermes/Hermes.csproj
```

### Runtime Errors

**Error: Quartz.NET duplicate job registration**
- Already fixed: `EnableScheduler` set to `false` in appsettings files
- Verify: Check `appsettings.json` and `appsettings.Production.json`

**Error: Azure DevOps API failures**
- Verify authentication: `curl http://localhost:3978/api/diagnostics/auth-test`
- Check organization/project configuration in appsettings
- Verify permissions: User needs read access to Azure DevOps

**Error: Microsoft Graph API failures**
- Verify authentication: `curl http://localhost:3978/api/diagnostics/auth-test`
- Check permissions: User needs `User.Read.All`, `DirectoryObject.Read.All`

## Testing After Launch

### Quick Health Check

```bash
# Backend health
curl http://localhost:3978/health

# Authentication status
curl http://localhost:3978/api/diagnostics/az-cli-status

# Full authentication test
curl http://localhost:3978/api/diagnostics/auth-test
```

### Test Chat Endpoint

```bash
# Development (port 3978)
curl -X POST "http://localhost:3978/api/hermes/v1.0/chat" \
  -H "Content-Type: application/json" \
  -H "x-ms-correlation-id: test-001" \
  -d "{\"text\": \"Hello Hermes\", \"userId\": \"user@example.com\"}"

# Production (port 3979)
curl -X POST "http://localhost:3979/api/hermes/v1.0/chat" \
  -H "Content-Type: application/json" \
  -H "x-ms-correlation-id: test-002" \
  -d "{\"text\": \"Hello Hermes\", \"userId\": \"user@example.com\"}"
```

### Test Capabilities

See `CLAUDE.md` section "Integration Testing" for comprehensive capability test suite.

## Advanced Usage

### Custom Cosmos DB Path

If Cosmos DB Emulator is installed in a non-default location, edit the launch script:

```powershell
# Change this line in start-dev.ps1 or start-prod.ps1
$cosmosPath = "C:\Custom\Path\CosmosDB.Emulator.exe"
```

### Custom Backend Arguments

Modify the `dotnet run` command in the launch scripts:

```powershell
# Example: Enable detailed logging
dotnet run --urls "http://localhost:3978" -- --Logging:LogLevel:Default=Debug

# Example: Override configuration
dotnet run --urls "http://localhost:3978" -- --OpenAI:Model=gpt-4o-mini
```

### Launch from PowerShell Profile

Add to your PowerShell profile (`$PROFILE`):

```powershell
function Start-HermesDev {
    Set-Location "C:\dev\repos\Hermes"
    .\start-dev.ps1 @args
}

function Start-HermesProd {
    Set-Location "C:\dev\repos\Hermes"
    .\start-prod.ps1 @args
}

# Usage from anywhere:
# Start-HermesDev -Watch
# Start-HermesProd -NoPlayground
```

## Comparison: Native vs Docker

| Aspect | Native (Current) | Docker (Abandoned) |
|--------|------------------|-------------------|
| Setup Complexity | Low | High |
| Azure CLI Auth | Direct (`az login`) | Token pass-through required |
| Cosmos DB | Local emulator | Would need Docker volume |
| Agents Playground | Native Windows app | N/A |
| Port Conflicts | Explicit (3978/3979) | Docker port mapping |
| Hot Reload | Supported (`-Watch`) | Requires volume mounts |
| Debugging | Direct in IDE | Remote debugging |
| Performance | Native speed | Container overhead |

**Why Native Was Chosen**:
1. **Tenant Restrictions**: User's tenant blocks command-line login, complicating Docker authentication
2. **Simplicity**: One-command launch vs multi-step Docker setup
3. **Azure CLI Issues**: Extension permission errors blocked token pass-through approach
4. **Developer Experience**: Faster iteration, better debugging, native tooling

## Related Documentation

- **Quick Reference**: `LAUNCH-README.md` - Common commands and flags
- **Project Guide**: `CLAUDE.md` - Architecture, patterns, and conventions
- **Setup Guide**: `SETUP-README.md` - Initial project setup
- **Diagnostics**: `Hermes/Controllers/DiagnosticsController.cs` - Authentication test endpoints

## Changelog

- **2026-01-28**: Created native launch scripts (start-dev.ps1, start-prod.ps1)
  - Auto-start Cosmos DB Emulator
  - Azure CLI authentication check
  - Agents Playground auto-launch
  - Port separation (3978 dev, 3979 prod)
  - Batch file wrappers for double-click launching

---

_Last updated: 2026-01-28_
