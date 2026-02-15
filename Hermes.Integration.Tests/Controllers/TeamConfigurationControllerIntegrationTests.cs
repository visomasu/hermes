using System.Net;
using System.Net.Http.Json;
using Hermes.Storage.Repositories.TeamConfiguration;
using Xunit;

namespace Hermes.Integration.Tests.Controllers;

public class TeamConfigurationControllerIntegrationTests : IClassFixture<HermesWebApplicationFactory>
{
	private readonly HttpClient _client;

	public TeamConfigurationControllerIntegrationTests(HermesWebApplicationFactory factory)
	{
		_client = factory.CreateClient();
	}

	[Fact]
	public async Task GetAllTeams_ReturnsSuccessStatusCode()
	{
		// Act
		var response = await _client.GetAsync("/api/teamconfiguration");

		// Assert
		response.EnsureSuccessStatusCode();
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task GetAllTeams_ReturnsJsonArray()
	{
		// Act
		var response = await _client.GetAsync("/api/teamconfiguration");
		var teams = await response.Content.ReadFromJsonAsync<List<TeamConfigurationDocument>>();

		// Assert
		Assert.NotNull(teams);
		Assert.IsType<List<TeamConfigurationDocument>>(teams);
	}

	[Fact]
	public async Task CreateTeam_WithValidData_ReturnsCreated()
	{
		// Arrange
		var newTeam = new TeamConfigurationDocument
		{
			TeamId = $"test-team-{Guid.NewGuid()}",
			TeamName = "Test Team",
			IterationPath = "TestProject\\Sprint1",
			AreaPaths = new List<string> { "TestProject\\TestArea" },
			SlaOverrides = new Dictionary<string, int> { { "Task", 3 } }
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/teamconfiguration", newTeam);

		// Assert
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);

		var createdTeam = await response.Content.ReadFromJsonAsync<TeamConfigurationDocument>();
		Assert.NotNull(createdTeam);
		Assert.Equal(newTeam.TeamId, createdTeam.TeamId);
		Assert.Equal(newTeam.TeamName, createdTeam.TeamName);

		// Cleanup
		await _client.DeleteAsync($"/api/teamconfiguration/{newTeam.TeamId}");
	}

	[Fact]
	public async Task CreateTeam_WithEmptyTeamId_ReturnsBadRequest()
	{
		// Arrange
		var invalidTeam = new TeamConfigurationDocument
		{
			TeamId = "",
			TeamName = "Test Team"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/teamconfiguration", invalidTeam);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task CreateTeam_DuplicateTeamId_ReturnsConflict()
	{
		// Arrange
		var teamId = $"duplicate-test-{Guid.NewGuid()}";
		var team = new TeamConfigurationDocument
		{
			TeamId = teamId,
			TeamName = "Duplicate Test",
			IterationPath = "Test\\Sprint1",
			AreaPaths = new List<string> { "Test\\Area" },
			SlaOverrides = new Dictionary<string, int>()
		};

		// Create first team
		await _client.PostAsJsonAsync("/api/teamconfiguration", team);

		// Act - Try to create duplicate
		var response = await _client.PostAsJsonAsync("/api/teamconfiguration", team);

		// Assert
		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

		// Cleanup
		await _client.DeleteAsync($"/api/teamconfiguration/{teamId}");
	}

	[Fact]
	public async Task GetTeamById_ExistingTeam_ReturnsTeam()
	{
		// Arrange
		var teamId = $"get-test-{Guid.NewGuid()}";
		var team = new TeamConfigurationDocument
		{
			TeamId = teamId,
			TeamName = "Get Test Team",
			IterationPath = "Test\\Sprint1",
			AreaPaths = new List<string> { "Test\\Area" },
			SlaOverrides = new Dictionary<string, int>()
		};

		await _client.PostAsJsonAsync("/api/teamconfiguration", team);

		// Act
		var response = await _client.GetAsync($"/api/teamconfiguration/{teamId}");

		// Assert
		response.EnsureSuccessStatusCode();
		var retrievedTeam = await response.Content.ReadFromJsonAsync<TeamConfigurationDocument>();
		Assert.NotNull(retrievedTeam);
		Assert.Equal(teamId, retrievedTeam.TeamId);

		// Cleanup
		await _client.DeleteAsync($"/api/teamconfiguration/{teamId}");
	}

	[Fact]
	public async Task GetTeamById_NonExistentTeam_ReturnsNotFound()
	{
		// Act
		var response = await _client.GetAsync("/api/teamconfiguration/non-existent-team");

		// Assert
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task UpdateTeam_ExistingTeam_ReturnsOk()
	{
		// Arrange
		var teamId = $"update-test-{Guid.NewGuid()}";
		var team = new TeamConfigurationDocument
		{
			TeamId = teamId,
			TeamName = "Original Name",
			IterationPath = "Test\\Sprint1",
			AreaPaths = new List<string> { "Test\\Area" },
			SlaOverrides = new Dictionary<string, int>()
		};

		await _client.PostAsJsonAsync("/api/teamconfiguration", team);

		// Modify team
		team.TeamName = "Updated Name";
		team.SlaOverrides = new Dictionary<string, int> { { "Bug", 5 } };

		// Act
		var response = await _client.PutAsJsonAsync($"/api/teamconfiguration/{teamId}", team);

		// Assert
		response.EnsureSuccessStatusCode();
		var updatedTeam = await response.Content.ReadFromJsonAsync<TeamConfigurationDocument>();
		Assert.NotNull(updatedTeam);
		Assert.Equal("Updated Name", updatedTeam.TeamName);
		Assert.Single(updatedTeam.SlaOverrides);

		// Cleanup
		await _client.DeleteAsync($"/api/teamconfiguration/{teamId}");
	}

	[Fact]
	public async Task UpdateTeam_TeamIdMismatch_ReturnsBadRequest()
	{
		// Arrange
		var team = new TeamConfigurationDocument
		{
			TeamId = "team1",
			TeamName = "Test",
			IterationPath = "Test\\Sprint1",
			AreaPaths = new List<string>(),
			SlaOverrides = new Dictionary<string, int>()
		};

		// Act
		var response = await _client.PutAsJsonAsync("/api/teamconfiguration/team2", team);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task UpdateTeam_NonExistentTeam_ReturnsNotFound()
	{
		// Arrange
		var team = new TeamConfigurationDocument
		{
			TeamId = "non-existent",
			TeamName = "Test",
			IterationPath = "Test\\Sprint1",
			AreaPaths = new List<string>(),
			SlaOverrides = new Dictionary<string, int>()
		};

		// Act
		var response = await _client.PutAsJsonAsync("/api/teamconfiguration/non-existent", team);

		// Assert
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task DeleteTeam_ExistingTeam_ReturnsNoContent()
	{
		// Arrange
		var teamId = $"delete-test-{Guid.NewGuid()}";
		var team = new TeamConfigurationDocument
		{
			TeamId = teamId,
			TeamName = "Delete Test",
			IterationPath = "Test\\Sprint1",
			AreaPaths = new List<string>(),
			SlaOverrides = new Dictionary<string, int>()
		};

		await _client.PostAsJsonAsync("/api/teamconfiguration", team);

		// Act
		var response = await _client.DeleteAsync($"/api/teamconfiguration/{teamId}");

		// Assert
		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

		// Verify deletion
		var getResponse = await _client.GetAsync($"/api/teamconfiguration/{teamId}");
		Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
	}

	[Fact]
	public async Task DeleteTeam_NonExistentTeam_ReturnsNotFound()
	{
		// Act
		var response = await _client.DeleteAsync("/api/teamconfiguration/non-existent");

		// Assert
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task FullCrudWorkflow_CreatesUpdatesAndDeletes()
	{
		var teamId = $"crud-test-{Guid.NewGuid()}";

		// Create
		var team = new TeamConfigurationDocument
		{
			TeamId = teamId,
			TeamName = "CRUD Test",
			IterationPath = "Test\\Sprint1",
			AreaPaths = new List<string> { "Test\\Area1" },
			SlaOverrides = new Dictionary<string, int> { { "Task", 3 } }
		};

		var createResponse = await _client.PostAsJsonAsync("/api/teamconfiguration", team);
		Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

		// Read
		var getResponse = await _client.GetAsync($"/api/teamconfiguration/{teamId}");
		Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
		var retrievedTeam = await getResponse.Content.ReadFromJsonAsync<TeamConfigurationDocument>();
		Assert.NotNull(retrievedTeam);

		// Update
		retrievedTeam.TeamName = "CRUD Test Updated";
		retrievedTeam.AreaPaths = new List<string> { "Test\\Area1", "Test\\Area2" };
		var updateResponse = await _client.PutAsJsonAsync($"/api/teamconfiguration/{teamId}", retrievedTeam);
		Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

		// Verify update
		var getAfterUpdate = await _client.GetAsync($"/api/teamconfiguration/{teamId}");
		var updatedTeam = await getAfterUpdate.Content.ReadFromJsonAsync<TeamConfigurationDocument>();
		Assert.Equal("CRUD Test Updated", updatedTeam!.TeamName);
		Assert.Equal(2, updatedTeam.AreaPaths.Count);

		// Delete
		var deleteResponse = await _client.DeleteAsync($"/api/teamconfiguration/{teamId}");
		Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

		// Verify deletion
		var getAfterDelete = await _client.GetAsync($"/api/teamconfiguration/{teamId}");
		Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
	}
}
