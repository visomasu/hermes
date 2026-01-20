using Hermes.Controllers.Models.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserConfiguration.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Controllers
{
	/// <summary>
	/// Controller for testing user configuration functionality.
	/// </summary>
	[ApiController]
	[Route("api/user-config")]
	public class UserConfigurationController : ControllerBase
	{
		private readonly IUserConfigurationRepository _userConfigRepo;
		private readonly ILogger<UserConfigurationController> _logger;

		public UserConfigurationController(
			IUserConfigurationRepository userConfigRepo,
			ILogger<UserConfigurationController> logger)
		{
			_userConfigRepo = userConfigRepo;
			_logger = logger;
		}

		/// <summary>
		/// Gets user configuration by Teams user ID.
		/// GET /api/user-config/{teamsUserId}
		/// </summary>
		[HttpGet("{teamsUserId}")]
		public async Task<IActionResult> GetUserConfiguration(string teamsUserId)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(teamsUserId))
				{
					return BadRequest(new { error = "Teams user ID is required" });
				}

				var config = await _userConfigRepo.GetByTeamsUserIdAsync(teamsUserId);

				if (config == null)
				{
					return NotFound(new
					{
						message = "No configuration found for user",
						teamsUserId,
						note = "User will use default settings"
					});
				}

				return Ok(config);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving user configuration");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// Creates or updates user configuration.
		/// PUT /api/user-config/{teamsUserId}
		/// Body: { "notifications": { "slaViolationNotifications": true, ... } }
		/// </summary>
		[HttpPut("{teamsUserId}")]
		public async Task<IActionResult> UpdateUserConfiguration(
			string teamsUserId,
			[FromBody] UpdateUserConfigRequest request)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(teamsUserId))
				{
					return BadRequest(new { error = "Teams user ID is required" });
				}

				// Check if configuration exists
				var existingConfig = await _userConfigRepo.GetByTeamsUserIdAsync(teamsUserId);

				if (existingConfig == null)
				{
					// Create new configuration
					var newConfig = new UserConfigurationDocument
					{
						Id = teamsUserId,
						PartitionKey = teamsUserId,
						TeamsUserId = teamsUserId,
						Notifications = request.Notifications ?? new NotificationPreferences(),
						CreatedAt = DateTime.UtcNow,
						UpdatedAt = DateTime.UtcNow
					};

					await _userConfigRepo.CreateAsync(newConfig);

					return Ok(new
					{
						message = "User configuration created",
						configuration = newConfig
					});
				}
				else
				{
					// Update existing configuration
					if (request.Notifications != null)
					{
						existingConfig.Notifications = request.Notifications;
					}
					existingConfig.UpdatedAt = DateTime.UtcNow;

					await _userConfigRepo.UpdateAsync(existingConfig.Id, existingConfig);

					return Ok(new
					{
						message = "User configuration updated",
						configuration = existingConfig
					});
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating user configuration");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// Deletes user configuration (resets to defaults).
		/// DELETE /api/user-config/{teamsUserId}
		/// </summary>
		[HttpDelete("{teamsUserId}")]
		public async Task<IActionResult> DeleteUserConfiguration(string teamsUserId)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(teamsUserId))
				{
					return BadRequest(new { error = "Teams user ID is required" });
				}

				var existingConfig = await _userConfigRepo.GetByTeamsUserIdAsync(teamsUserId);

				if (existingConfig == null)
				{
					return NotFound(new
					{
						message = "No configuration found for user",
						teamsUserId
					});
				}

				await _userConfigRepo.DeleteAsync(teamsUserId, teamsUserId);

				return Ok(new
				{
					message = "User configuration deleted",
					teamsUserId,
					note = "User will now use default settings"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting user configuration");
				return StatusCode(500, new { error = ex.Message });
			}
		}
	}
}
