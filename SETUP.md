# Hermes Setup Guide

This guide walks you through setting up the Hermes AI-powered project assistant for local development.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Azure OpenAI Setup](#azure-openai-setup)
3. [Azure DevOps PAT Configuration](#azure-devops-pat-configuration)
4. [Agents Playground Setup](#agents-playground-setup)
5. [Hermes Configuration](#hermes-configuration)
6. [Running Hermes Locally](#running-hermes-locally)
7. [Troubleshooting](#troubleshooting)

---

## Prerequisites

Before starting, ensure you have:

- **.NET 8.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** or **VS Code** with C# extension
- **Azure subscription** with permissions to create Azure OpenAI resources
- **Azure DevOps account** with access to your organization/project
- **Git** for version control

---

## Azure OpenAI Setup

Hermes uses Azure OpenAI for AI agent capabilities. Follow these steps to create and configure your Azure OpenAI resource.

### Step 1: Create Azure OpenAI Resource

1. **Navigate to Azure Portal**: Go to [portal.azure.com](https://portal.azure.com)

2. **Create a new resource**:
   - Click **"Create a resource"**
   - Search for **"Azure OpenAI"**
   - Click **"Create"**

3. **Configure the resource**:
   ```
   Subscription:       [Your Azure Subscription]
   Resource Group:     [Create new or select existing, e.g., "rg-hermes"]
   Region:             East US 2 (or region with GPT-4 availability)
   Name:               [Unique name, e.g., "visomasu-project-hermes"]
   Pricing Tier:       Standard S0
   ```

4. **Review + Create**: Click **"Review + create"**, then **"Create"**

5. **Wait for deployment**: This typically takes 2-3 minutes

### Step 2: Deploy GPT Model

1. **Navigate to your Azure OpenAI resource**:
   - Go to the resource you just created
   - Click **"Go to Azure OpenAI Studio"** or navigate to [oai.azure.com](https://oai.azure.com)

2. **Create a deployment**:
   - In Azure OpenAI Studio, click **"Deployments"** in the left menu
   - Click **"+ Create new deployment"**

   Configure the deployment:
   ```
   Model:              gpt-4o (or gpt-4o-mini for cost optimization)
   Deployment name:    gpt-4o-deployment
   Model version:      Latest (auto-update enabled)
   Deployment type:    Standard
   Tokens per minute:  10K (adjust based on expected load)
   Content filter:     DefaultV2
   ```

3. **Create embedding deployment** (for conversation context):
   - Click **"+ Create new deployment"** again

   Configure:
   ```
   Model:              text-embedding-3-small
   Deployment name:    text-embedding-3-small
   Model version:      Latest
   Tokens per minute:  10K
   ```

4. **Note the deployment names**: You'll need these for configuration

### Step 3: Get Azure OpenAI Credentials

1. **Get the Endpoint**:
   - In Azure Portal, navigate to your Azure OpenAI resource
   - Click **"Keys and Endpoint"** in the left menu
   - Copy the **"Endpoint"** (e.g., `https://visomasu-project-hermes.openai.azure.com/`)

2. **Get the API Key**:
   - On the same page, copy **"KEY 1"** or **"KEY 2"**
   - ⚠️ **Important**: Keep this key secure, do not commit it to source control

3. **Store credentials securely**:
   - For local development, you can use `appsettings.Development.json` (not committed)
   - For production, use **Azure Key Vault** or environment variables

---

## Azure DevOps PAT Configuration

Hermes needs access to Azure DevOps to retrieve work items, hierarchies, and project data.

### Step 1: Create Personal Access Token (PAT)

1. **Navigate to Azure DevOps**:
   - Go to [dev.azure.com](https://dev.azure.com)
   - Sign in to your organization

2. **Open User Settings**:
   - Click your profile icon in the top-right corner
   - Select **"Personal access tokens"**

3. **Create new token**:
   - Click **"+ New Token"**

   Configure the token:
   ```
   Name:               Hermes Bot Access
   Organization:       [Your organization, e.g., "dynamicscrm"]
   Expiration:         90 days (or custom)
   Scopes:             Custom defined
   ```

4. **Select required scopes**:

   Check these permissions:
   ```
   ✅ Work Items: Read
   ✅ Work Items: Write (if bot will update work items)
   ✅ Project and Team: Read
   ✅ Analytics: Read (for querying work items)
   ```

5. **Create and copy token**:
   - Click **"Create"**
   - ⚠️ **IMPORTANT**: Copy the token immediately (it won't be shown again)
   - Store it securely (password manager or Azure Key Vault)

### Step 2: Identify Organization and Project

1. **Get Organization name**:
   - Look at your Azure DevOps URL: `https://dev.azure.com/{organization}`
   - Example: `https://dev.azure.com/dynamicscrm` → Organization is `dynamicscrm`

2. **Get Project name**:
   - Navigate to your project
   - The project name appears in the URL: `https://dev.azure.com/{organization}/{project}`
   - Example: `https://dev.azure.com/dynamicscrm/OneCRM` → Project is `OneCRM`

---

## Agents Playground Setup

Agents Playground is Microsoft's testing client for the Microsoft 365 Agents SDK. It allows you to test your bot locally without deploying to Azure.

### Step 1: Install Agents Playground

**Option A: Install via Windows Package Manager** (Recommended for Windows)

```bash
winget install agentsplayground
```

**Option B: Install via npm** (Cross-platform)

1. **Prerequisites**: Install Node.js from [nodejs.org](https://nodejs.org/)

2. **Install globally**:
   ```bash
   npm install -g @microsoft/m365agentsplayground
   ```

**Option C: Install on Linux**

```bash
curl -s https://raw.githubusercontent.com/OfficeDev/microsoft-365-agents-toolkit/dev/.github/scripts/install-agentsplayground-linux.sh | bash
```

### Step 2: Launch Agents Playground

Before launching Agents Playground, make sure Hermes is running (see [Running Hermes Locally](#running-hermes-locally)).

**Basic Launch (Anonymous Mode - Recommended for Local Development)**

```bash
agentsplayground -e "http://localhost:3978/api/messages" -c "emulator"
```

**Command Options Explained:**
- `-e` or `--app-endpoint`: Your agent's endpoint URL
- `-c` or `--channel-id`: Channel type
  - `emulator` - Simple testing interface (recommended for development)
  - `webchat` - Web chat interface
  - `msteams` - Microsoft Teams interface

**Advanced: Launch with Authentication** (Optional, for production scenarios)

```bash
agentsplayground -e "http://localhost:3978/api/messages" -c "emulator" --client-id "your-client-id" --client-secret "your-client-secret" --tenant-id "your-tenant-id"
```

For local development with Hermes, authentication is **not required** as `TokenValidation` is disabled in development mode.

### Step 3: Test Your First Message

1. **In the Agents Playground window, type a test message**:
   ```
   Hello, Hermes!
   ```

2. **Expected response**:
   - You should receive a welcome message describing Hermes capabilities
   - If you see an error, check the [Troubleshooting](#troubleshooting) section

### Step 4: Alternative - Using Environment Variables

Instead of passing command-line options every time, you can set environment variables:

**Windows (PowerShell):**
```powershell
$env:BOT_ENDPOINT="http://localhost:3978/api/messages"
$env:DEFAULT_CHANNEL_ID="emulator"
agentsplayground
```

**Linux/macOS:**
```bash
export BOT_ENDPOINT="http://localhost:3978/api/messages"
export DEFAULT_CHANNEL_ID="emulator"
agentsplayground
```

**Note:** Command-line options take priority over environment variables.

### Step 5: View All Options

To see all available options:
```bash
agentsplayground --help
```

---

## Hermes Configuration

Configure Hermes to use your Azure OpenAI and Azure DevOps credentials.

### Step 1: Create Local Configuration File

Create a file named `appsettings.Development.json` in the `Hermes/` directory:

```bash
cd Hermes
# Create the file (it's already in .gitignore)
```

### Step 2: Configure Settings

Add the following configuration to `appsettings.Development.json`:

```json
{
  "TokenValidation": {
    "Enabled": false
  },
  "OpenAI": {
    "Endpoint": "https://[YOUR-RESOURCE-NAME].openai.azure.com/",
    "ApiKey": "[YOUR-AZURE-OPENAI-KEY]"
  },
  "AzureDevOps": {
    "Organization": "[YOUR-ORGANIZATION]",
    "Project": "[YOUR-PROJECT]",
    "PersonalAccessToken": "[YOUR-PAT]"
  },
  "ConversationContext": {
    "RelevanceThreshold": 0.70,
    "MaxContextTurns": 10,
    "MinRecentTurns": 1,
    "EnableSemanticFiltering": true,
    "EmbeddingModel": "text-embedding-3-small",
    "EnableQueryDeduplication": true,
    "QueryDuplicationThreshold": 0.95
  },
  "Scheduling": {
    "EnableScheduler": false
  }
}
```

### Step 3: Replace Placeholder Values

Update the following values:

1. **OpenAI Configuration**:
   ```json
   "Endpoint": "https://visomasu-project-hermes.openai.azure.com/",
   "ApiKey": "abc123def456..."
   ```
   - Use the endpoint and key from [Azure OpenAI Setup Step 3](#step-3-get-azure-openai-credentials)

2. **Azure DevOps Configuration**:
   ```json
   "Organization": "dynamicscrm",
   "Project": "OneCRM",
   "PersonalAccessToken": "xyz789abc123..."
   ```
   - Use values from [Azure DevOps PAT Configuration Step 2](#step-2-identify-organization-and-project)

### Step 4: Verify Configuration

Run a quick check to ensure the file is valid:

```bash
dotnet build Hermes
```

If there are JSON syntax errors, the build will fail with details.

---

## Running Hermes Locally

### Step 1: Restore Dependencies

```bash
cd Hermes
dotnet restore
```

### Step 2: Build the Project

```bash
dotnet build
```

Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Step 3: Run Hermes

```bash
dotnet run --project Hermes
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:3978
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### Step 4: Verify Hermes is Running

Open a browser and navigate to:
```
http://localhost:3978/api/health
```

*(Note: If you don't have a health endpoint, you can verify by checking that Agents Playground can connect)*

### Step 5: Connect with Agents Playground

1. **Launch Agents Playground** (see [Agents Playground Setup](#agents-playground-setup))

2. **Configure connection**:
   ```
   Bot Endpoint: http://localhost:3978/api/messages
   ```

3. **Send a test message**:
   ```
   What can you do?
   ```

4. **Expected response**: Hermes should describe its capabilities related to Azure DevOps work items and project management

---

## Troubleshooting

### Issue: "Failed to connect to bot endpoint"

**Cause**: Hermes is not running, or the endpoint is incorrect

**Solution**:
1. Verify Hermes is running: Check the console for `Now listening on: http://localhost:3978`
2. Check the endpoint in Agents Playground: It should be `http://localhost:3978/api/messages` (note the `/api/messages` path)
3. Ensure no other application is using port 3978

---

### Issue: "Unauthorized" or "401" error

**Cause**: Token validation is enabled in development mode

**Solution**:
1. Open `appsettings.Development.json`
2. Set `"TokenValidation": { "Enabled": false }`
3. Restart Hermes

---

### Issue: "Azure OpenAI API error: 401 Unauthorized"

**Cause**: Invalid or expired Azure OpenAI API key

**Solution**:
1. Go to Azure Portal → Your Azure OpenAI resource → "Keys and Endpoint"
2. Copy **KEY 1** or regenerate keys if needed
3. Update `appsettings.Development.json` with the correct key
4. Restart Hermes

---

### Issue: "Azure DevOps PAT authentication failed"

**Cause**: Invalid, expired, or insufficient permissions on PAT

**Solution**:
1. Go to Azure DevOps → User Settings → Personal Access Tokens
2. Verify the token is not expired
3. Check that the token has **"Work Items: Read"** scope
4. If needed, create a new token and update `appsettings.Development.json`
5. Restart Hermes

---

### Issue: "Deployment not found" when calling Azure OpenAI

**Cause**: The model deployment name in code doesn't match your Azure deployment

**Solution**:
1. Check your deployment name in Azure OpenAI Studio → Deployments
2. Update the deployment name in Hermes code if needed (typically in `HermesOrchestrator.cs`)
3. Common deployment names:
   - `gpt-4o-deployment`
   - `gpt-4o-mini`
   - `text-embedding-3-small`

---

### Issue: "SSL certificate validation failed"

**Cause**: Self-signed certificate or certificate validation issue in development

**Solution**:
This is already handled in `Program.cs` for development mode:
```csharp
if (builder.Environment.IsDevelopment())
{
    System.Net.ServicePointManager.ServerCertificateValidationCallback =
        (sender, certificate, chain, sslPolicyErrors) => true;
}
```

If you still have issues:
1. Verify you're running in Development mode
2. Check `launchSettings.json` for the `ASPNETCORE_ENVIRONMENT` variable

---

### Issue: Agents Playground shows "Bot returned empty response"

**Cause**: Exception in Hermes orchestration or configuration issue

**Solution**:
1. Check Hermes console output for error messages
2. Verify `appsettings.Development.json` is correctly formatted (use a JSON validator)
3. Check that all required services are configured:
   - Azure OpenAI endpoint and key
   - Azure DevOps organization, project, and PAT
4. Enable detailed logging:
   ```json
   "Logging": {
     "LogLevel": {
       "Default": "Debug",
       "Microsoft.AspNetCore": "Information"
     }
   }
   ```

---

### Issue: "Rate limit exceeded" from Azure OpenAI

**Cause**: Too many requests to Azure OpenAI

**Solution**:
1. In Azure Portal, increase the **Tokens Per Minute (TPM)** limit:
   - Go to Azure OpenAI Studio → Deployments
   - Edit your deployment
   - Increase TPM to 30K or 50K
2. Implement retry logic with exponential backoff (already in Hermes)
3. Consider using `gpt-4o-mini` for development (lower cost, higher limits)

---

## Next Steps

Once you have Hermes running locally:

1. **Test basic capabilities**:
   - "What can you do?"
   - "Get work item 123456" (use a real work item ID from your Azure DevOps)

2. **Test hierarchy operations**:
   - "Show me the hierarchy for epic 123456"
   - "Get work items in area path 'MyProject\\MyTeam'"

3. **Test newsletter generation**:
   - "Generate a newsletter for epic 123456"

4. **Review logs**: Check the console output for any warnings or errors

5. **Explore the code**: See [CLAUDE.md](CLAUDE.md) for architecture details

---

## Additional Resources

- **Microsoft 365 Agents SDK**: [microsoft.com/agents-sdk](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)
- **Azure OpenAI Documentation**: [learn.microsoft.com/azure/ai-services/openai](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- **Azure DevOps REST API**: [learn.microsoft.com/rest/api/azure/devops](https://learn.microsoft.com/en-us/rest/api/azure/devops/)
- **Hermes Architecture**: See [README.md](README.md) and [CLAUDE.md](CLAUDE.md)

---

## Security Best Practices

⚠️ **Never commit secrets to source control**

1. **Use `.gitignore`**:
   - `appsettings.Development.json` is already in `.gitignore`
   - Never commit files with API keys or PATs

2. **For production deployments**:
   - Use **Azure Key Vault** for secrets
   - Configure Managed Identity for Azure resources
   - Rotate PATs regularly (every 90 days)

3. **Limit PAT scope**:
   - Only grant minimum required permissions
   - Use separate PATs for different environments

4. **Monitor access**:
   - Review Azure OpenAI usage in Azure Portal
   - Check Azure DevOps audit logs for PAT usage

---

## Support

For issues or questions:
- **Architecture questions**: See [CLAUDE.md](CLAUDE.md)
- **API documentation**: See inline XML comments in code
- **Bug reports**: Create an issue in the repository

---

_Last updated: 2026-01-23_
