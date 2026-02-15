using System.Net;
using System.Net.Http.Json;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;
using Xunit;

namespace Hermes.Integration.Tests.Controllers;

public class UserConfigurationControllerIntegrationTests : IClassFixture<HermesWebApplicationFactory>
{
	private readonly HttpClient _client;

	public UserConfigurationControllerIntegrationTests(HermesWebApplicationFactory factory)
	{
		_client = factory.CreateClient();
	}

	[Fact]
	public async Task GetUserConfiguration_NonExistentUser_ReturnsNotFound()
	{
		// Act
		var response = await _client.GetAsync("/api/user-config/nonexistent@test.com");

		// Assert
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task CreateUserConfiguration_WithValidData_ReturnsOk()
	{
		// Arrange
		var userId = $"testuser-{Guid.NewGuid()}@test.com";
		var config = new
		{
			notifications = new
			{
				slaViolationNotifications = true,
				workItemUpdateNotifications = true,
				maxNotificationsPerHour = 5,
				maxNotificationsPerDay = 20,
				timeZoneId = "Pacific Standard Time",
				quietHours = new
				{
					enabled = true,
					startTime = "22:00",
					endTime = "07:00"
				}
			}
		};

		// Act
		var response = await _client.PutAsJsonAsync($"/api/user-config/{userId}", config);

		// Assert
		response.EnsureSuccessStatusCode();
		var result = await response.Content.ReadFromJsonAsync<object>();
		Assert.NotNull(result);

		// Cleanup
		await _client.DeleteAsync($"/api/user-config/{userId}");
	}

	[Fact]
	public async Task GetUserConfiguration_AfterCreate_ReturnsConfig()
	{
		// Arrange
		var userId = $"testuser-{Guid.NewGuid()}@test.com";
		var config = new
		{
			notifications = new
			{
				slaViolationNotifications = true,
				workItemUpdateNotifications = false,
				maxNotificationsPerHour = 10,
				maxNotificationsPerDay = 50,
				timeZoneId = "UTC"
			}
		};

		await _client.PutAsJsonAsync($"/api/user-config/{userId}", config);

		// Act
		var response = await _client.GetAsync($"/api/user-config/{userId}");

		// Assert
		response.EnsureSuccessStatusCode();
		var userConfig = await response.Content.ReadFromJsonAsync<UserConfigurationDocument>();
		Assert.NotNull(userConfig);
		Assert.Equal(userId, userConfig.TeamsUserId);
		Assert.True(userConfig.Notifications.SlaViolationNotifications);
		Assert.False(userConfig.Notifications.WorkItemUpdateNotifications);

		// Cleanup
		await _client.DeleteAsync($"/api/user-config/{userId}");
	}

	[Fact]
	public async Task UpdateUserConfiguration_ExistingUser_UpdatesValues()
	{
		// Arrange
		var userId = $"testuser-{Guid.NewGuid()}@test.com";
		var initialConfig = new
		{
			notifications = new
			{
				slaViolationNotifications = true,
				workItemUpdateNotifications = true,
				maxNotificationsPerHour = 5,
				maxNotificationsPerDay = 20,
				timeZoneId = "UTC"
			}
		};

		await _client.PutAsJsonAsync($"/api/user-config/{userId}", initialConfig);

		// Update config
		var updatedConfig = new
		{
			notifications = new
			{
				slaViolationNotifications = false,
				workItemUpdateNotifications = false,
				maxNotificationsPerHour = 10,
				maxNotificationsPerDay = 50,
				timeZoneId = "Pacific Standard Time"
			}
		};

		// Act
		var response = await _client.PutAsJsonAsync($"/api/user-config/{userId}", updatedConfig);

		// Assert
		response.EnsureSuccessStatusCode();

		// Verify update
		var getResponse = await _client.GetAsync($"/api/user-config/{userId}");
		var userConfig = await getResponse.Content.ReadFromJsonAsync<UserConfigurationDocument>();
		Assert.NotNull(userConfig);
		Assert.False(userConfig.Notifications.SlaViolationNotifications);
		Assert.Equal(10, userConfig.Notifications.MaxNotificationsPerHour);

		// Cleanup
		await _client.DeleteAsync($"/api/user-config/{userId}");
	}

	[Fact]
	public async Task DeleteUserConfiguration_ExistingUser_ReturnsOk()
	{
		// Arrange
		var userId = $"testuser-{Guid.NewGuid()}@test.com";
		var config = new
		{
			notifications = new
			{
				slaViolationNotifications = true,
				workItemUpdateNotifications = true,
				maxNotificationsPerHour = 5,
				maxNotificationsPerDay = 20,
				timeZoneId = "UTC"
			}
		};

		await _client.PutAsJsonAsync($"/api/user-config/{userId}", config);

		// Act
		var response = await _client.DeleteAsync($"/api/user-config/{userId}");

		// Assert
		response.EnsureSuccessStatusCode();

		// Verify deletion
		var getResponse = await _client.GetAsync($"/api/user-config/{userId}");
		Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
	}

	[Fact]
	public async Task DeleteUserConfiguration_NonExistentUser_ReturnsNotFound()
	{
		// Act
		var response = await _client.DeleteAsync("/api/user-config/nonexistent@test.com");

		// Assert
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task PutUserConfiguration_WithEmptyUserId_ReturnsBadRequest()
	{
		// Arrange
		var config = new
		{
			notifications = new
			{
				slaViolationNotifications = true,
				workItemUpdateNotifications = true,
				maxNotificationsPerHour = 5,
				maxNotificationsPerDay = 20,
				timeZoneId = "UTC"
			}
		};

		// Act
		var response = await _client.PutAsJsonAsync("/api/user-config/ ", config);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task FullCrudWorkflow_UserConfiguration()
	{
		var userId = $"testuser-{Guid.NewGuid()}@test.com";

		// Create
		var createConfig = new
		{
			notifications = new
			{
				slaViolationNotifications = true,
				workItemUpdateNotifications = true,
				maxNotificationsPerHour = 5,
				maxNotificationsPerDay = 20,
				timeZoneId = "UTC",
				quietHours = new
				{
					enabled = true,
					startTime = "22:00",
					endTime = "07:00"
				}
			}
		};

		var createResponse = await _client.PutAsJsonAsync($"/api/user-config/{userId}", createConfig);
		Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

		// Read
		var getResponse = await _client.GetAsync($"/api/user-config/{userId}");
		Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
		var config = await getResponse.Content.ReadFromJsonAsync<UserConfigurationDocument>();
		Assert.NotNull(config);
		Assert.True(config.Notifications.QuietHours!.Enabled);

		// Update
		var updateConfig = new
		{
			notifications = new
			{
				slaViolationNotifications = false,
				workItemUpdateNotifications = false,
				maxNotificationsPerHour = 10,
				maxNotificationsPerDay = 50,
				timeZoneId = "Pacific Standard Time",
				quietHours = new
				{
					enabled = false,
					startTime = "23:00",
					endTime = "08:00"
				}
			}
		};

		var updateResponse = await _client.PutAsJsonAsync($"/api/user-config/{userId}", updateConfig);
		Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

		// Verify update
		var getAfterUpdate = await _client.GetAsync($"/api/user-config/{userId}");
		var updatedConfig = await getAfterUpdate.Content.ReadFromJsonAsync<UserConfigurationDocument>();
		Assert.False(updatedConfig!.Notifications.SlaViolationNotifications);
		Assert.False(updatedConfig.Notifications.QuietHours!.Enabled);

		// Delete
		var deleteResponse = await _client.DeleteAsync($"/api/user-config/{userId}");
		Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

		// Verify deletion
		var getAfterDelete = await _client.GetAsync($"/api/user-config/{userId}");
		Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
	}
}
