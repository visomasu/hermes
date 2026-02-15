using Hermes.Infrastructure;
using Hermes.Storage.Repositories.TeamConfiguration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.Tests.Infrastructure
{
	/// <summary>
	/// Tests for TeamConfigurationSeeder.
	/// Note: These are simplified tests since IConfiguration's Get<T>() is an extension method
	/// that cannot be mocked. Full integration testing should be done with actual configuration.
	/// </summary>
	public class TeamConfigurationSeederTests
	{
		[Fact]
		public async Task SeedTeamsAsync_LogsWarning_WhenGetSectionReturnsEmpty()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<TeamConfigurationSeeder>>();
			var repositoryMock = new Mock<ITeamConfigurationRepository>();

			// Create in-memory configuration with no teams
			var configBuilder = new ConfigurationBuilder();
			configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "WorkItemUpdateSla:Enabled", "true" }
			});
			var configuration = configBuilder.Build();

			var seeder = new TeamConfigurationSeeder(loggerMock.Object, repositoryMock.Object, configuration);

			// Act
			await seeder.SeedTeamsAsync();

			// Assert
			repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<TeamConfigurationDocument>(), default), Times.Never);
			loggerMock.Verify(
				x => x.Log(
					LogLevel.Information,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No teams found")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);
		}

		[Fact]
		public async Task SeedTeamsAsync_SeedsTeams_WithValidConfiguration()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<TeamConfigurationSeeder>>();
			var repositoryMock = new Mock<ITeamConfigurationRepository>();

			// Create in-memory configuration with teams
			var configBuilder = new ConfigurationBuilder();
			configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "WorkItemUpdateSla:Teams:0:TeamId", "team1" },
				{ "WorkItemUpdateSla:Teams:0:TeamName", "Team 1" },
				{ "WorkItemUpdateSla:Teams:0:IterationPath", "Path1" },
				{ "WorkItemUpdateSla:Teams:0:AreaPaths:0", "Area1" },
				{ "WorkItemUpdateSla:Teams:0:SlaOverrides:Task", "3" },
				{ "WorkItemUpdateSla:Teams:1:TeamId", "team2" },
				{ "WorkItemUpdateSla:Teams:1:TeamName", "Team 2" },
				{ "WorkItemUpdateSla:Teams:1:IterationPath", "Path2" },
				{ "WorkItemUpdateSla:Teams:1:AreaPaths:0", "Area2" }
			});
			var configuration = configBuilder.Build();

			var seeder = new TeamConfigurationSeeder(loggerMock.Object, repositoryMock.Object, configuration);

			// Act
			await seeder.SeedTeamsAsync();

			// Assert
			repositoryMock.Verify(r => r.UpsertAsync(
				It.Is<TeamConfigurationDocument>(d => d.TeamId == "team1" && d.TeamName == "Team 1"),
				default), Times.Once);
			repositoryMock.Verify(r => r.UpsertAsync(
				It.Is<TeamConfigurationDocument>(d => d.TeamId == "team2" && d.TeamName == "Team 2"),
				default), Times.Once);
		}

		[Fact]
		public async Task SeedTeamsAsync_SkipsTeams_WithEmptyTeamId()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<TeamConfigurationSeeder>>();
			var repositoryMock = new Mock<ITeamConfigurationRepository>();

			// Create in-memory configuration with one valid and one invalid team
			var configBuilder = new ConfigurationBuilder();
			configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "WorkItemUpdateSla:Teams:0:TeamId", "" }, // Empty TeamId
				{ "WorkItemUpdateSla:Teams:0:TeamName", "Invalid Team" },
				{ "WorkItemUpdateSla:Teams:1:TeamId", "valid-team" },
				{ "WorkItemUpdateSla:Teams:1:TeamName", "Valid Team" }
			});
			var configuration = configBuilder.Build();

			var seeder = new TeamConfigurationSeeder(loggerMock.Object, repositoryMock.Object, configuration);

			// Act
			await seeder.SeedTeamsAsync();

			// Assert
			repositoryMock.Verify(r => r.UpsertAsync(
				It.Is<TeamConfigurationDocument>(d => d.TeamId == "valid-team"),
				default), Times.Once);
			repositoryMock.Verify(r => r.UpsertAsync(
				It.Is<TeamConfigurationDocument>(d => d.TeamId == ""),
				default), Times.Never);
			loggerMock.Verify(
				x => x.Log(
					LogLevel.Warning,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Skipping team with empty TeamId")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);
		}

		[Fact]
		public async Task SeedTeamsAsync_SetsDocumentProperties_Correctly()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<TeamConfigurationSeeder>>();
			var repositoryMock = new Mock<ITeamConfigurationRepository>();

			// Create in-memory configuration
			var configBuilder = new ConfigurationBuilder();
			configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "WorkItemUpdateSla:Teams:0:TeamId", "contact-center-ai" },
				{ "WorkItemUpdateSla:Teams:0:TeamName", "Contact Center AI" },
				{ "WorkItemUpdateSla:Teams:0:IterationPath", "OneCRM\\FY26\\Q3\\1Wk\\1Wk33" },
				{ "WorkItemUpdateSla:Teams:0:AreaPaths:0", "OneCRM\\AI\\ContactCenter" },
				{ "WorkItemUpdateSla:Teams:0:SlaOverrides:Task", "3" }
			});
			var configuration = configBuilder.Build();

			var seeder = new TeamConfigurationSeeder(loggerMock.Object, repositoryMock.Object, configuration);

			// Act
			await seeder.SeedTeamsAsync();

			// Assert
			repositoryMock.Verify(r => r.UpsertAsync(
				It.Is<TeamConfigurationDocument>(d =>
					d.Id == "contact-center-ai" &&
					d.PartitionKey == "contact-center-ai" &&
					d.TeamId == "contact-center-ai" &&
					d.TeamName == "Contact Center AI" &&
					d.IterationPath == "OneCRM\\FY26\\Q3\\1Wk\\1Wk33" &&
					d.AreaPaths.Count == 1 &&
					d.AreaPaths[0] == "OneCRM\\AI\\ContactCenter" &&
					d.SlaOverrides["Task"] == 3),
				default), Times.Once);
		}

		[Fact]
		public async Task SeedTeamsAsync_ThrowsException_OnRepositoryError()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<TeamConfigurationSeeder>>();
			var repositoryMock = new Mock<ITeamConfigurationRepository>();

			// Create in-memory configuration
			var configBuilder = new ConfigurationBuilder();
			configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "WorkItemUpdateSla:Teams:0:TeamId", "team1" },
				{ "WorkItemUpdateSla:Teams:0:TeamName", "Team 1" }
			});
			var configuration = configBuilder.Build();

			repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<TeamConfigurationDocument>(), default))
				.ThrowsAsync(new Exception("Database error"));

			var seeder = new TeamConfigurationSeeder(loggerMock.Object, repositoryMock.Object, configuration);

			// Act & Assert
			await Assert.ThrowsAsync<Exception>(async () => await seeder.SeedTeamsAsync());
			loggerMock.Verify(
				x => x.Log(
					LogLevel.Error,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error seeding team configurations")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);
		}
	}
}
