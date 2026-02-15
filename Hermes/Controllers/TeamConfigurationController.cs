using Hermes.Storage.Repositories.TeamConfiguration;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Controllers
{
	/// <summary>
	/// Controller for managing team configurations (CRUD operations).
	/// Allows runtime management of teams without application restart.
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	public class TeamConfigurationController : ControllerBase
	{
		private readonly ILogger<TeamConfigurationController> _logger;
		private readonly ITeamConfigurationRepository _repository;

		public TeamConfigurationController(
			ILogger<TeamConfigurationController> logger,
			ITeamConfigurationRepository repository)
		{
			_logger = logger;
			_repository = repository;
		}

		/// <summary>
		/// GET /api/teamconfiguration
		/// Retrieves all team configurations.
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> GetAllTeams()
		{
			try
			{
				_logger.LogInformation("Retrieving all team configurations");
				var teams = await _repository.GetAllTeamsAsync();
				return Ok(teams);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving team configurations");
				return StatusCode(500, new { error = "Failed to retrieve team configurations", message = ex.Message });
			}
		}

		/// <summary>
		/// GET /api/teamconfiguration/{teamId}
		/// Retrieves a specific team configuration by ID.
		/// </summary>
		[HttpGet("{teamId}")]
		public async Task<IActionResult> GetTeamById(string teamId)
		{
			try
			{
				_logger.LogInformation("Retrieving team configuration: {TeamId}", teamId);
				var team = await _repository.GetByTeamIdAsync(teamId);

				if (team == null)
				{
					return NotFound(new { error = $"Team '{teamId}' not found" });
				}

				return Ok(team);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving team configuration: {TeamId}", teamId);
				return StatusCode(500, new { error = "Failed to retrieve team configuration", message = ex.Message });
			}
		}

		/// <summary>
		/// POST /api/teamconfiguration
		/// Creates a new team configuration.
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> CreateTeam([FromBody] TeamConfigurationDocument team)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(team.TeamId))
				{
					return BadRequest(new { error = "TeamId is required" });
				}

				_logger.LogInformation("Creating team configuration: {TeamId}", team.TeamId);

				// Check if team already exists
				var existing = await _repository.GetByTeamIdAsync(team.TeamId);
				if (existing != null)
				{
					return Conflict(new { error = $"Team '{team.TeamId}' already exists. Use PUT to update." });
				}

				var created = await _repository.UpsertAsync(team);
				return CreatedAtAction(nameof(GetTeamById), new { teamId = created.TeamId }, created);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating team configuration");
				return StatusCode(500, new { error = "Failed to create team configuration", message = ex.Message });
			}
		}

		/// <summary>
		/// PUT /api/teamconfiguration/{teamId}
		/// Updates an existing team configuration.
		/// </summary>
		[HttpPut("{teamId}")]
		public async Task<IActionResult> UpdateTeam(string teamId, [FromBody] TeamConfigurationDocument team)
		{
			try
			{
				if (teamId != team.TeamId)
				{
					return BadRequest(new { error = "TeamId in URL must match TeamId in body" });
				}

				_logger.LogInformation("Updating team configuration: {TeamId}", teamId);

				// Verify team exists
				var existing = await _repository.GetByTeamIdAsync(teamId);
				if (existing == null)
				{
					return NotFound(new { error = $"Team '{teamId}' not found. Use POST to create." });
				}

				var updated = await _repository.UpsertAsync(team);
				return Ok(updated);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating team configuration: {TeamId}", teamId);
				return StatusCode(500, new { error = "Failed to update team configuration", message = ex.Message });
			}
		}

		/// <summary>
		/// DELETE /api/teamconfiguration/{teamId}
		/// Deletes a team configuration.
		/// </summary>
		[HttpDelete("{teamId}")]
		public async Task<IActionResult> DeleteTeam(string teamId)
		{
			try
			{
				_logger.LogInformation("Deleting team configuration: {TeamId}", teamId);

				// Verify team exists
				var existing = await _repository.GetByTeamIdAsync(teamId);
				if (existing == null)
				{
					return NotFound(new { error = $"Team '{teamId}' not found" });
				}

				await _repository.DeleteAsync(teamId, teamId);
				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting team configuration: {TeamId}", teamId);
				return StatusCode(500, new { error = "Failed to delete team configuration", message = ex.Message });
			}
		}
	}
}
