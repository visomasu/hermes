# Hermes Tools

This folder contains utility scripts for managing Hermes configuration and operations.

## HttpYac/team-configuration.http

HTTP requests for managing team configurations via REST API.

### Prerequisites

1. **httpyac** (VS Code extension) or **REST Client** (VS Code extension)
2. **Hermes running locally**: `dotnet run --project Hermes`
3. **CosmosDB Emulator** running

### Usage

#### Using httpyac (Recommended)

1. Install the [httpyac VS Code extension](https://marketplace.visualstudio.com/items?itemName=anweber.vscode-httpyac)
2. Open `Tools/HttpYac/team-configuration.http`
3. Click "Send Request" above any request, or use:
   - `Ctrl+Alt+R` (Windows/Linux) or `Cmd+Alt+R` (Mac) to send the request under cursor
   - Right-click → "Send Request"

#### Using REST Client

1. Install the [REST Client VS Code extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
2. Open `Tools/HttpYac/team-configuration.http`
3. Click "Send Request" above any request

#### Using curl

```bash
# Create team
curl -X POST http://localhost:3978/api/teamconfiguration \
  -H "Content-Type: application/json" \
  -d '{
    "teamId": "contact-center-ai",
    "teamName": "Contact Center AI",
    "iterationPath": "OneCRM\\FY26\\Q3\\1Wk\\1Wk33",
    "areaPaths": ["OneCRM\\AI\\ContactCenter"],
    "slaOverrides": {"Task": 3}
  }'

# Get all teams
curl http://localhost:3978/api/teamconfiguration

# Get specific team
curl http://localhost:3978/api/teamconfiguration/contact-center-ai

# Update team
curl -X PUT http://localhost:3978/api/teamconfiguration/contact-center-ai \
  -H "Content-Type: application/json" \
  -d '{
    "teamId": "contact-center-ai",
    "teamName": "Contact Center AI",
    "iterationPath": "OneCRM\\FY26\\Q3\\1Wk\\1Wk34",
    "areaPaths": ["OneCRM\\AI\\ContactCenter"],
    "slaOverrides": {"Task": 3}
  }'

# Delete team
curl -X DELETE http://localhost:3978/api/teamconfiguration/contact-center-ai
```

## TeamConfiguration API Endpoints

### GET /api/teamconfiguration
Retrieves all team configurations.

**Response**: `200 OK` with array of team configurations

### GET /api/teamconfiguration/{teamId}
Retrieves a specific team configuration.

**Response**:
- `200 OK` with team configuration
- `404 Not Found` if team doesn't exist

### POST /api/teamconfiguration
Creates a new team configuration.

**Request Body**:
```json
{
  "teamId": "string",
  "teamName": "string",
  "iterationPath": "string",
  "areaPaths": ["string"],
  "slaOverrides": { "WorkItemType": days }
}
```

**Response**:
- `201 Created` with created team
- `400 Bad Request` if TeamId is missing
- `409 Conflict` if team already exists

### PUT /api/teamconfiguration/{teamId}
Updates an existing team configuration.

**Request Body**: Same as POST

**Response**:
- `200 OK` with updated team
- `400 Bad Request` if TeamId mismatch
- `404 Not Found` if team doesn't exist

### DELETE /api/teamconfiguration/{teamId}
Deletes a team configuration.

**Response**:
- `204 No Content` on success
- `404 Not Found` if team doesn't exist

## Production Deployment

For production environments:

1. **Remove appsettings.json Teams section** (optional) - The REST API is the primary way to manage teams
2. **Seed teams via CI/CD pipeline** using httpyac or curl commands
3. **Use authentication** (configure in future if needed)
4. **Backup team configurations** periodically from CosmosDB
