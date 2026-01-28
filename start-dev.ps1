# Hermes Development Launch Script
# Starts Hermes in Development mode on port 3978

param(
    [switch]$NoBuild,
    [switch]$Watch,
    [switch]$NoPlayground
)

$ErrorActionPreference = "Stop"

Write-Host "=== Hermes Development Instance ===" -ForegroundColor Cyan
Write-Host ""

# Check and start Cosmos DB Emulator
Write-Host "Checking Cosmos DB Emulator..." -ForegroundColor Yellow
$cosmosProcess = Get-Process -Name "CosmosDB.Emulator" -ErrorAction SilentlyContinue
if (-not $cosmosProcess) {
    Write-Host "  Cosmos DB Emulator is not running. Starting..." -ForegroundColor Yellow

    # Find Cosmos DB Emulator installation
    $cosmosPath = "${env:ProgramFiles}\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe"
    if (-not (Test-Path $cosmosPath)) {
        Write-Host "ERROR: Cosmos DB Emulator not found at: $cosmosPath" -ForegroundColor Red
        Write-Host "Please install from: https://aka.ms/cosmosdb-emulator" -ForegroundColor Yellow
        exit 1
    }

    # Start Cosmos DB Emulator
    Start-Process -FilePath $cosmosPath -ArgumentList "/NoExplorer", "/NoUI" -WindowStyle Hidden
    Write-Host "  Waiting for Cosmos DB Emulator to start..." -ForegroundColor Yellow

    # Wait for Cosmos DB to be ready (max 60 seconds)
    $maxWaitSeconds = 60
    $waitedSeconds = 0
    $cosmosReady = $false

    while ($waitedSeconds -lt $maxWaitSeconds) {
        Start-Sleep -Seconds 2
        $waitedSeconds += 2

        try {
            # Try to connect to Cosmos DB endpoint
            $response = Invoke-WebRequest -Uri "https://localhost:8081/_explorer/emulator.pem" -UseBasicParsing -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                $cosmosReady = $true
                break
            }
        }
        catch {
            # Still starting up
        }

        Write-Host "  Still waiting... ($waitedSeconds seconds)" -ForegroundColor Gray
    }

    if ($cosmosReady) {
        Write-Host "  Cosmos DB Emulator started successfully" -ForegroundColor Green
    }
    else {
        Write-Host "WARNING: Cosmos DB Emulator may not be fully ready yet" -ForegroundColor Yellow
        Write-Host "  You can verify at: https://localhost:8081/_explorer/index.html" -ForegroundColor Yellow
    }
}
else {
    Write-Host "  Cosmos DB Emulator is already running" -ForegroundColor Green
}

Write-Host ""

# Check Azure CLI authentication
Write-Host "Checking Azure CLI authentication..." -ForegroundColor Yellow
try {
    $account = az account show 2>$null | ConvertFrom-Json
    Write-Host "  Authenticated as: $($account.user.name)" -ForegroundColor Green
}
catch {
    Write-Host "WARNING: Not authenticated with Azure CLI" -ForegroundColor Red
    Write-Host "Run 'az login' to authenticate" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host ""
Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  Environment: Development" -ForegroundColor White
Write-Host "  Backend Port: 3978" -ForegroundColor White
Write-Host "  Backend URL: http://localhost:3978" -ForegroundColor White
Write-Host ""

# Check and launch Agents Playground
if (-not $NoPlayground) {
    Write-Host "Checking Agents Playground..." -ForegroundColor Yellow

    $playgroundInstalled = $false
    try {
        $null = Get-Command agentsplayground -ErrorAction Stop
        $playgroundInstalled = $true
        Write-Host "  Agents Playground is installed" -ForegroundColor Green
    }
    catch {
        Write-Host "  Agents Playground is not installed" -ForegroundColor Yellow
        Write-Host "  Install with: winget install agentsplayground" -ForegroundColor Yellow
        Write-Host "  Or: npm install -g @microsoft/m365agentsplayground" -ForegroundColor Yellow
    }

    if ($playgroundInstalled) {
        Write-Host "  Launching Agents Playground in new window..." -ForegroundColor Green

        # Launch Agents Playground in a new PowerShell window
        $playgroundCommand = "agentsplayground -e `"http://localhost:3978/api/messages`" -c `"emulator`""
        Start-Process powershell -ArgumentList "-NoExit", "-Command", $playgroundCommand

        Write-Host "  Agents Playground launched successfully" -ForegroundColor Green
    }

    Write-Host ""
}

# Navigate to project directory
Push-Location $PSScriptRoot\Hermes

try {
    if ($Watch) {
        Write-Host "Starting Hermes in WATCH mode (auto-reload on file changes)..." -ForegroundColor Cyan
        Write-Host ""
        if ($NoBuild) {
            dotnet watch run --no-build --urls "http://localhost:3978"
        }
        else {
            dotnet watch run --urls "http://localhost:3978"
        }
    }
    else {
        Write-Host "Starting Hermes..." -ForegroundColor Cyan
        Write-Host ""
        if ($NoBuild) {
            dotnet run --no-build --urls "http://localhost:3978"
        }
        else {
            dotnet run --urls "http://localhost:3978"
        }
    }
}
finally {
    Pop-Location
}
