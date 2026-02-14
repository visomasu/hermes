using Hermes.Controllers;
using Hermes.Storage.Repositories.TeamConfiguration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.Tests.Controllers
{
	public class TeamConfigurationControllerTests
	{
		private readonly Mock<ILogger<TeamConfigurationController>> _loggerMock;
		private readonly Mock<ITeamConfigurationRepository> _repositoryMock;
		private readonly TeamConfigurationController _controller;

		public TeamConfigurationControllerTests()
		{
			_loggerMock = new Mock<ILogger<TeamConfigurationController>>();
			_repositoryMock = new Mock<ITeamConfigurationRepository>();
			_controller = new TeamConfigurationController(_loggerMock.Object, _repositoryMock.Object);
		}

		#region GetAllTeams Tests

		[Fact]
		public async Task GetAllTeams_ReturnsOkResult_WithTeamList()
		{
			// Arrange
			var teams = new List<TeamConfigurationDocument>
			{
				new TeamConfigurationDocument
				{
					TeamId = "team1",
					TeamName = "Team 1",
					IterationPath = "Path1",
					AreaPaths = new List<string> { "Area1" },
					SlaOverrides = new Dictionary<string, int>()
				},
				new TeamConfigurationDocument
				{
					TeamId = "team2",
					TeamName = "Team 2",
					IterationPath = "Path2",
					AreaPaths = new List<string> { "Area2" },
					SlaOverrides = new Dictionary<string, int>()
				}
			};
			_repositoryMock.Setup(r => r.GetAllTeamsAsync(default)).ReturnsAsync(teams);

			// Act
			var result = await _controller.GetAllTeams();

			// Assert
			var okResult = Assert.IsType<OkObjectResult>(result);
			var returnedTeams = Assert.IsAssignableFrom<List<TeamConfigurationDocument>>(okResult.Value);
			Assert.Equal(2, returnedTeams.Count);
			_repositoryMock.Verify(r => r.GetAllTeamsAsync(default), Times.Once);
		}

		[Fact]
		public async Task GetAllTeams_ReturnsEmptyList_WhenNoTeams()
		{
			// Arrange
			_repositoryMock.Setup(r => r.GetAllTeamsAsync(default)).ReturnsAsync(new List<TeamConfigurationDocument>());

			// Act
			var result = await _controller.GetAllTeams();

			// Assert
			var okResult = Assert.IsType<OkObjectResult>(result);
			var returnedTeams = Assert.IsAssignableFrom<List<TeamConfigurationDocument>>(okResult.Value);
			Assert.Empty(returnedTeams);
		}

		[Fact]
		public async Task GetAllTeams_ReturnsInternalServerError_OnException()
		{
			// Arrange
			_repositoryMock.Setup(r => r.GetAllTeamsAsync(default)).ThrowsAsync(new Exception("Database error"));

			// Act
			var result = await _controller.GetAllTeams();

			// Assert
			var statusResult = Assert.IsType<ObjectResult>(result);
			Assert.Equal(500, statusResult.StatusCode);
		}

		#endregion

		#region GetTeamById Tests

		[Fact]
		public async Task GetTeamById_ReturnsOkResult_WhenTeamExists()
		{
			// Arrange
			var team = new TeamConfigurationDocument
			{
				TeamId = "contact-center-ai",
				TeamName = "Contact Center AI",
				IterationPath = "OneCRM\\FY26\\Q3\\1Wk\\1Wk33",
				AreaPaths = new List<string> { "OneCRM\\AI\\ContactCenter" },
				SlaOverrides = new Dictionary<string, int> { { "Task", 3 } }
			};
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("contact-center-ai", default)).ReturnsAsync(team);

			// Act
			var result = await _controller.GetTeamById("contact-center-ai");

			// Assert
			var okResult = Assert.IsType<OkObjectResult>(result);
			var returnedTeam = Assert.IsType<TeamConfigurationDocument>(okResult.Value);
			Assert.Equal("contact-center-ai", returnedTeam.TeamId);
			Assert.Equal("Contact Center AI", returnedTeam.TeamName);
		}

		[Fact]
		public async Task GetTeamById_ReturnsNotFound_WhenTeamDoesNotExist()
		{
			// Arrange
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("nonexistent", default)).ReturnsAsync((TeamConfigurationDocument?)null);

			// Act
			var result = await _controller.GetTeamById("nonexistent");

			// Assert
			var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
			Assert.NotNull(notFoundResult.Value);
		}

		[Fact]
		public async Task GetTeamById_ReturnsInternalServerError_OnException()
		{
			// Arrange
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("team1", default)).ThrowsAsync(new Exception("Database error"));

			// Act
			var result = await _controller.GetTeamById("team1");

			// Assert
			var statusResult = Assert.IsType<ObjectResult>(result);
			Assert.Equal(500, statusResult.StatusCode);
		}

		#endregion

		#region CreateTeam Tests

		[Fact]
		public async Task CreateTeam_ReturnsCreatedResult_WhenTeamIsNew()
		{
			// Arrange
			var newTeam = new TeamConfigurationDocument
			{
				TeamId = "new-team",
				TeamName = "New Team",
				IterationPath = "Path",
				AreaPaths = new List<string> { "Area" },
				SlaOverrides = new Dictionary<string, int>()
			};
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("new-team", default)).ReturnsAsync((TeamConfigurationDocument?)null);
			_repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<TeamConfigurationDocument>(), default)).ReturnsAsync(newTeam);

			// Act
			var result = await _controller.CreateTeam(newTeam);

			// Assert
			var createdResult = Assert.IsType<CreatedAtActionResult>(result);
			Assert.Equal(nameof(TeamConfigurationController.GetTeamById), createdResult.ActionName);
			var returnedTeam = Assert.IsType<TeamConfigurationDocument>(createdResult.Value);
			Assert.Equal("new-team", returnedTeam.TeamId);
		}

		[Fact]
		public async Task CreateTeam_ReturnsBadRequest_WhenTeamIdIsEmpty()
		{
			// Arrange
			var team = new TeamConfigurationDocument
			{
				TeamId = "",
				TeamName = "Team"
			};

			// Act
			var result = await _controller.CreateTeam(team);

			// Assert
			var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
			Assert.NotNull(badRequestResult.Value);
		}

		[Fact]
		public async Task CreateTeam_ReturnsConflict_WhenTeamAlreadyExists()
		{
			// Arrange
			var existingTeam = new TeamConfigurationDocument
			{
				TeamId = "existing-team",
				TeamName = "Existing Team"
			};
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("existing-team", default)).ReturnsAsync(existingTeam);

			var newTeam = new TeamConfigurationDocument
			{
				TeamId = "existing-team",
				TeamName = "New Team"
			};

			// Act
			var result = await _controller.CreateTeam(newTeam);

			// Assert
			var conflictResult = Assert.IsType<ConflictObjectResult>(result);
			Assert.NotNull(conflictResult.Value);
		}

		[Fact]
		public async Task CreateTeam_ReturnsInternalServerError_OnException()
		{
			// Arrange
			var team = new TeamConfigurationDocument
			{
				TeamId = "team1",
				TeamName = "Team 1"
			};
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("team1", default)).ThrowsAsync(new Exception("Database error"));

			// Act
			var result = await _controller.CreateTeam(team);

			// Assert
			var statusResult = Assert.IsType<ObjectResult>(result);
			Assert.Equal(500, statusResult.StatusCode);
		}

		#endregion

		#region UpdateTeam Tests

		[Fact]
		public async Task UpdateTeam_ReturnsOkResult_WhenTeamExists()
		{
			// Arrange
			var existingTeam = new TeamConfigurationDocument
			{
				TeamId = "team1",
				TeamName = "Old Name"
			};
			var updatedTeam = new TeamConfigurationDocument
			{
				TeamId = "team1",
				TeamName = "New Name"
			};
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("team1", default)).ReturnsAsync(existingTeam);
			_repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<TeamConfigurationDocument>(), default)).ReturnsAsync(updatedTeam);

			// Act
			var result = await _controller.UpdateTeam("team1", updatedTeam);

			// Assert
			var okResult = Assert.IsType<OkObjectResult>(result);
			var returnedTeam = Assert.IsType<TeamConfigurationDocument>(okResult.Value);
			Assert.Equal("New Name", returnedTeam.TeamName);
		}

		[Fact]
		public async Task UpdateTeam_ReturnsBadRequest_WhenTeamIdMismatch()
		{
			// Arrange
			var team = new TeamConfigurationDocument
			{
				TeamId = "team2",
				TeamName = "Team"
			};

			// Act
			var result = await _controller.UpdateTeam("team1", team);

			// Assert
			var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
			Assert.NotNull(badRequestResult.Value);
		}

		[Fact]
		public async Task UpdateTeam_ReturnsNotFound_WhenTeamDoesNotExist()
		{
			// Arrange
			var team = new TeamConfigurationDocument
			{
				TeamId = "nonexistent",
				TeamName = "Team"
			};
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("nonexistent", default)).ReturnsAsync((TeamConfigurationDocument?)null);

			// Act
			var result = await _controller.UpdateTeam("nonexistent", team);

			// Assert
			var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
			Assert.NotNull(notFoundResult.Value);
		}

		[Fact]
		public async Task UpdateTeam_ReturnsInternalServerError_OnException()
		{
			// Arrange
			var team = new TeamConfigurationDocument
			{
				TeamId = "team1",
				TeamName = "Team"
			};
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("team1", default)).ThrowsAsync(new Exception("Database error"));

			// Act
			var result = await _controller.UpdateTeam("team1", team);

			// Assert
			var statusResult = Assert.IsType<ObjectResult>(result);
			Assert.Equal(500, statusResult.StatusCode);
		}

		#endregion

		#region DeleteTeam Tests

		[Fact]
		public async Task DeleteTeam_ReturnsNoContent_WhenTeamExists()
		{
			// Arrange
			var team = new TeamConfigurationDocument
			{
				TeamId = "team1",
				TeamName = "Team 1"
			};
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("team1", default)).ReturnsAsync(team);
			_repositoryMock.Setup(r => r.DeleteAsync("team1", "team1")).Returns(Task.CompletedTask);

			// Act
			var result = await _controller.DeleteTeam("team1");

			// Assert
			Assert.IsType<NoContentResult>(result);
			_repositoryMock.Verify(r => r.DeleteAsync("team1", "team1"), Times.Once);
		}

		[Fact]
		public async Task DeleteTeam_ReturnsNotFound_WhenTeamDoesNotExist()
		{
			// Arrange
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("nonexistent", default)).ReturnsAsync((TeamConfigurationDocument?)null);

			// Act
			var result = await _controller.DeleteTeam("nonexistent");

			// Assert
			var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
			Assert.NotNull(notFoundResult.Value);
			_repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task DeleteTeam_ReturnsInternalServerError_OnException()
		{
			// Arrange
			_repositoryMock.Setup(r => r.GetByTeamIdAsync("team1", default)).ThrowsAsync(new Exception("Database error"));

			// Act
			var result = await _controller.DeleteTeam("team1");

			// Assert
			var statusResult = Assert.IsType<ObjectResult>(result);
			Assert.Equal(500, statusResult.StatusCode);
		}

		#endregion
	}
}
