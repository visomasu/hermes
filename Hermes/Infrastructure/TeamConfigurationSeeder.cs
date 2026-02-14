using Hermes.Storage.Repositories.TeamConfiguration;
using Hermes.Storage.Repositories.TeamConfiguration.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hermes.Infrastructure
{
	/// <summary>
	/// Seeds team configurations from appsettings.json into CosmosDB on application startup.
	/// Optional fallback for development environments. Production should use REST API for team management.
	/// </summary>
	public class TeamConfigurationSeeder
	{
		private readonly ILogger<TeamConfigurationSeeder> _logger;
		private readonly ITeamConfigurationRepository _repository;
		private readonly IConfiguration _configuration;

		public TeamConfigurationSeeder(
			ILogger<TeamConfigurationSeeder> logger,
			ITeamConfigurationRepository repository,
			IConfiguration configuration)
		{
			_logger = logger;
			_repository = repository;
			_configuration = configuration;
		}

		/// <summary>
		/// Seeds team configurations from appsettings.json into the database.
		/// Upserts all teams from the configuration (create if new, update if exists).
		/// </summary>
		public async Task SeedTeamsAsync()
		{
			try
			{
				var teamsSection = _configuration.GetSection("WorkItemUpdateSla:Teams");
				var teams = teamsSection.Get<List<TeamSettings>>();

				if (teams == null || !teams.Any())
				{
					_logger.LogInformation(
						"No teams found in configuration at WorkItemUpdateSla:Teams. Skipping appsettings.json seeding. Use REST API to add teams.");
					return;
				}

				_logger.LogInformation(
					"Seeding {Count} team configurations from appsettings.json", teams.Count);

				foreach (var team in teams)
				{
					if (string.IsNullOrWhiteSpace(team.TeamId))
					{
						_logger.LogWarning(
							"Skipping team with empty TeamId: {TeamName}", team.TeamName);
						continue;
					}

					var document = new TeamConfigurationDocument
					{
						Id = team.TeamId,
						PartitionKey = team.TeamId,
						TeamId = team.TeamId,
						TeamName = team.TeamName,
						IterationPath = team.IterationPath,
						AreaPaths = team.AreaPaths,
						SlaOverrides = team.SlaOverrides ?? new Dictionary<string, int>(),
						CreatedAt = DateTime.UtcNow
					};

					await _repository.UpsertAsync(document);

					_logger.LogInformation(
						"Seeded team configuration: TeamId={TeamId}, TeamName={TeamName}, AreaPaths={AreaPaths}",
						team.TeamId,
						team.TeamName,
						string.Join(", ", team.AreaPaths));
				}

				_logger.LogInformation(
					"Successfully seeded {Count} team configurations", teams.Count);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error seeding team configurations from appsettings.json");
				throw;
			}
		}
	}
}
