# Hermes Launch Scripts - Quick Reference

## Overview

Hermes provides simple one-command launch scripts that automatically handle all dependencies:
- Cosmos DB Emulator (auto-start if not running)
- Azure CLI authentication check
- Agents Playground (auto-launch in separate window)
- Hermes backend (on specified port)

## Launch Options

### Development Mode (Port 3978)

```powershell
# Standard launch
.\start-dev.ps1

# Watch mode (auto-reload on file changes)
.\start-dev.ps1 -Watch

# Skip build (if already built)
.\start-dev.ps1 -NoBuild

# Skip Agents Playground (backend only)
.\start-dev.ps1 -NoPlayground

# Combine flags
.\start-dev.ps1 -Watch -NoPlayground
```

**Double-click option**: `start-dev.bat`

### Production Mode (Port 3979)

```powershell
# Standard launch
.\start-prod.ps1

# Skip build (if already built)
.\start-prod.ps1 -NoBuild

# Skip Agents Playground (backend only)
.\start-prod.ps1 -NoPlayground

# Combine flags
.\start-prod.ps1 -NoBuild -NoPlayground
```

**Double-click option**: `start-prod.bat`

## What Gets Started

1. **Cosmos DB Emulator** (if not already running)
   - Auto-detects running instance
   - Starts with `/NoExplorer` and `/NoUI` flags
   - Waits up to 60 seconds for readiness
   - Health check: `https://localhost:8081/_explorer/emulator.pem`

2. **Azure CLI Authentication Check**
   - Verifies `az login` status
   - Shows warning if not authenticated
   - Does not block startup (backend will fail on first request)

3. **Agents Playground** (unless `-NoPlayground` specified)
   - Launches in separate PowerShell window
   - Connects to correct port:
     - Dev: `http://localhost:3978/api/messages`
     - Prod: `http://localhost:3979/api/messages`
   - Uses emulator configuration (`-c "emulator"`)

4. **Hermes Backend**
   - Dev: Port 3978, Development environment
   - Prod: Port 3979, Release configuration, Production environment

## Running Multiple Instances

You can run dev and prod simultaneously since they use different ports:

```powershell
# Terminal 1
.\start-dev.ps1

# Terminal 2
.\start-prod.ps1
```

Each will have its own Agents Playground window connected to the correct backend.

## Prerequisites

- **Cosmos DB Emulator**: Must be installed at `C:\Program Files\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe`
  - Download: https://aka.ms/cosmosdb-emulator
- **Azure CLI**: Must be installed and authenticated (`az login`)
- **Agents Playground** (optional): Install with:
  ```bash
  winget install agentsplayground
  # OR
  npm install -g @microsoft/m365agentsplayground
  ```

## Troubleshooting

### Cosmos DB won't start
- Check if already running: Open Task Manager and look for "CosmosDB.Emulator"
- Manually start: `"C:\Program Files\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe"`
- Verify installation path matches script

### Azure CLI not authenticated
```bash
az login
```

### Agents Playground not installed
The script will show a warning but continue. Install with:
```bash
winget install agentsplayground
```

### Port already in use
- Dev (3978): Check if another Hermes dev instance is running
- Prod (3979): Check if another Hermes prod instance is running
- Use Task Manager or `netstat -ano | findstr :3978` to find processes

### Build errors
First time setup:
```bash
dotnet build Hermes/Hermes.csproj
dotnet build Hermes/Hermes.csproj --configuration Release
```

## For More Details

See `NATIVE-LAUNCH.md` for comprehensive documentation including:
- Architecture and design decisions
- Advanced configuration options
- Integration testing guide
- Detailed troubleshooting
