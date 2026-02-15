# Hermes Integration Tests

Comprehensive integration tests for all Hermes API endpoints using `WebApplicationFactory`.

## What's Included

### Test Projects

1. **TeamConfigurationControllerIntegrationTests** (16 tests)
   - GET /api/teamconfiguration (list all teams)
   - GET /api/teamconfiguration/{teamId}
   - POST /api/teamconfiguration (create team)
   - PUT /api/teamconfiguration/{teamId} (update team)
   - DELETE /api/teamconfiguration/{teamId}
   - Full CRUD workflow test
   - Error cases (duplicate, not found, validation)

2. **UserConfigurationControllerIntegrationTests** (10 tests)
   - GET /api/user-config/{userId}
   - PUT /api/user-config/{userId} (create/update)
   - DELETE /api/user-config/{userId}
   - Full CRUD workflow test
   - Error cases (not found, validation)

3. **HealthControllerIntegrationTests** (3 tests)
   - GET /health
   - Response validation

4. **HermesControllerIntegrationTests** (5 tests)
   - POST /api/hermes/v1.0/chat
   - Various input scenarios
   - Correlation ID handling

## Running the Tests

### Prerequisites

1. **Stop all running Hermes processes**:
   ```bash
   # On Windows
   tasklist | findstr Hermes
   taskkill /F /IM Hermes.exe

   # Or from Task Manager, end the Hermes.exe process
   ```

2. **CosmosDB Emulator** must be running (tests use the real backend)

### Run All Integration Tests

```bash
cd Hermes.Integration.Tests
dotnet test
```

### Run Specific Test Class

```bash
dotnet test --filter "TeamConfigurationControllerIntegrationTests"
dotnet test --filter "UserConfigurationControllerIntegrationTests"
dotnet test --filter "HealthControllerIntegrationTests"
dotnet test --filter "HermesControllerIntegrationTests"
```

### Run Specific Test

```bash
dotnet test --filter "GetAllTeams_ReturnsSuccessStatusCode"
dotnet test --filter "FullCrudWorkflow_CreatesUpdatesAndDeletes"
```

### Run with Verbose Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Architecture

### WebApplicationFactory

The tests use `HermesWebApplicationFactory` which:
- Starts the Hermes application in-memory
- Configures Development environment
- Allows service overrides for testing
- Provides an `HttpClient` for API calls

### Test Structure

```csharp
public class MyControllerIntegrationTests : IClassFixture<HermesWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MyControllerIntegrationTests(HermesWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TestName_Scenario_ExpectedBehavior()
    {
        // Arrange
        var requestData = new { ... };

        // Act
        var response = await _client.PostAsJsonAsync("/api/endpoint", requestData);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### Test Data Cleanup

Tests that create data (POST) include cleanup code (DELETE) to prevent test pollution:

```csharp
// Create team
await _client.PostAsJsonAsync("/api/teamconfiguration", team);

// ... test logic ...

// Cleanup
await _client.DeleteAsync($"/api/teamconfiguration/{team.TeamId}");
```

## Test Coverage

### Endpoints Covered

✅ Team Configuration API (full CRUD)
✅ User Configuration API (full CRUD)
✅ Health Endpoint
✅ Chat Endpoint

### Test Scenarios

- ✅ Happy path (successful operations)
- ✅ Not found scenarios (404)
- ✅ Bad request scenarios (400)
- ✅ Conflict scenarios (409)
- ✅ Full CRUD workflows
- ✅ Data validation
- ✅ Response format validation

## Known Issues

### Build Error: "Process cannot access file"

If you see:
```
error MSB3027: Could not copy "apphost.exe" to "Hermes.exe".
The file is locked by: "Hermes (38324)"
```

**Solution**: Stop all running Hermes processes before building/testing:
```bash
# Windows
taskkill /F /IM Hermes.exe

# Or use Task Manager
```

### CosmosDB Connection Issues

If tests fail with CosmosDB connection errors:

1. Ensure CosmosDB Emulator is running
2. Verify connection string in `appsettings.json` or `appsettings.Development.json`
3. Check emulator certificate is trusted

## Continuous Integration

To integrate with CI/CD:

```yaml
# GitHub Actions / Azure DevOps example
- name: Run Integration Tests
  run: |
    # Start CosmosDB Emulator (if not already running)
    dotnet test Hermes.Integration.Tests --logger trx --collect:"XPlat Code Coverage"
```

## Future Enhancements

- [ ] Add integration tests for AzureDevOps endpoints
- [ ] Add integration tests for Microsoft Graph endpoints
- [ ] Add integration tests for Proactive Messaging endpoints
- [ ] Add integration tests for Tools endpoints
- [ ] Mock external dependencies (Azure DevOps, Graph API) for faster tests
- [ ] Add performance/load testing
- [ ] Add WebSocket integration tests

## Troubleshooting

### Tests are slow

Integration tests are slower than unit tests because they:
- Start the full application
- Make real HTTP calls
- Use real databases (CosmosDB)

Consider:
- Running unit tests during development
- Running integration tests before commits/PRs
- Using mocked services for faster feedback

### Tests fail with authentication errors

The integration tests run in Development mode which uses `LocalDevelopmentAuthentication`. Ensure your `appsettings.Development.json` is configured correctly.

### Tests interfere with each other

Each test uses unique IDs (GUIDs) to prevent interference. If you see conflicts:
- Ensure cleanup code is running
- Check CosmosDB for orphaned test data
- Consider using `IAsyncLifetime` for setup/teardown

## Contributing

When adding new endpoints to Hermes:

1. Add corresponding integration tests
2. Follow the naming convention: `{Controller}IntegrationTests`
3. Include happy path, error cases, and full CRUD workflows
4. Add cleanup code for tests that create data
5. Update this README with new test coverage
