# Microsoft Graph API Testing Guide

## Quick Start

### 1. Authenticate with Azure CLI
```bash
az login
```

### 2. Get Your Azure AD Object ID
```bash
az ad signed-in-user show --query id -o tsv
```

Copy the output (e.g., `12345678-1234-1234-1234-123456789abc`)

### 3. Start Hermes Locally
```bash
dotnet run --project Hermes
```

Server will start on `http://localhost:3978`

### 4. Test with httpYac

Open `microsoft-graph.http` in VS Code and:

1. **Replace** `YOUR-AZURE-AD-OBJECT-ID-HERE` with your actual ID from step 2
2. **Execute** the requests using httpYac extension (click "Send Request" above each request)

## Available Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /api/microsoftgraph/status` | Verify client is initialized |
| `GET /api/microsoftgraph/test-az-cli` | Test az CLI integration for local dev fallback |
| `GET /api/microsoftgraph/user/{userId}/email` | Get user's email |
| `GET /api/microsoftgraph/user/{userId}/direct-reports` | Get direct report emails (manager check) |
| `GET /api/microsoftgraph/user/{userId}/profile` | Get complete profile (optimized) |

## Expected Results

### Test az CLI Integration
```json
{
  "success": true,
  "aadObjectId": "6a0b5481-9742-485f-8595-b0c3a89934df",
  "isValidGuid": true,
  "parsedGuid": "6a0b5481-9742-485f-8595-b0c3a89934df",
  "exitCode": 0,
  "durationMs": 2077.007
}
```

This endpoint tests the same az CLI integration used by HermesTeamsAgent for local development fallback.

### For Individual Contributor (IC)
```json
{
  "userId": "12345678-1234-1234-1234-123456789abc",
  "email": "john.doe@example.com",
  "isManager": false,
  "directReportCount": 0,
  "directReports": []
}
```

### For Manager
```json
{
  "userId": "87654321-4321-4321-4321-cba987654321",
  "email": "manager@example.com",
  "isManager": true,
  "directReportCount": 3,
  "directReports": [
    "direct1@example.com",
    "direct2@example.com",
    "direct3@example.com"
  ]
}
```

## Troubleshooting

### "DefaultAzureCredential failed to retrieve token"
**Solution**: Run `az login` and verify with `az account show`

### "Authorization_RequestDenied"
**Solution**: Your account needs `User.Read.All` or `User.ReadBasic.All` permissions. Contact your Azure AD admin.

### "User not found or has no email"
**Solution**: Verify the Azure AD Object ID is correct. User might not have mail attribute set.

## Finding Other Users

```bash
# Search by email
az ad user show --id user@example.com --query id -o tsv

# List all users
az ad user list --query "[].{name:displayName, id:id, email:mail}" -o table
```

## Authentication Methods

- **Local Development**: Uses `az login` credentials
- **Production**: Uses Managed Identity (Azure App Service, Container Apps)
- **No Configuration Needed**: DefaultAzureCredential handles everything automatically!

## Next Steps

Once Microsoft Graph integration is verified:
1. Proceed to **Phase 2**: UserConfiguration Schema Extension
2. Implement user registration capabilities
3. Test end-to-end SLA notification flow
