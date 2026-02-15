using Hermes.Storage.Core;
using Hermes.Storage.Repositories.TeamConfiguration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.Tests.Storage.Repositories.TeamConfiguration
{
	public class TeamConfigurationRepositoryTests
	{
		[Fact]
		public async Task GetByTeamIdAsync_ReturnsNull_WhenNoDocumentExists()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			storageMock
				.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()))
				.ReturnsAsync((TeamConfigurationDocument?)null);

			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			// Act
			var result = await repo.GetByTeamIdAsync("contact-center-ai");

			// Assert
			Assert.Null(result);
			// Repository should prefix the partition key with ObjectTypeCode
			storageMock.Verify(s => s.ReadAsync("contact-center-ai", "team-config:contact-center-ai"), Times.Once);
		}

		[Fact]
		public async Task GetByTeamIdAsync_ReturnsDocument_WhenExists()
		{
			// Arrange
			var document = new TeamConfigurationDocument
			{
				Id = "contact-center-ai",
				PartitionKey = "contact-center-ai",
				TeamId = "contact-center-ai",
				TeamName = "Contact Center AI",
				IterationPath = "OneCRM\\FY26\\Q3\\1Wk\\1Wk33",
				AreaPaths = new List<string> { "OneCRM\\AI\\ContactCenter" },
				SlaOverrides = new Dictionary<string, int> { { "Task", 3 } },
				CreatedAt = DateTime.UtcNow
			};

			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			// Mock expects prefixed partition key from storage layer
			storageMock
				.Setup(s => s.ReadAsync("contact-center-ai", "team-config:contact-center-ai"))
				.ReturnsAsync(document);

			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			// Act
			var result = await repo.GetByTeamIdAsync("contact-center-ai");

			// Assert
			Assert.NotNull(result);
			Assert.Equal("contact-center-ai", result.TeamId);
			Assert.Equal("Contact Center AI", result.TeamName);
			Assert.Equal("OneCRM\\FY26\\Q3\\1Wk\\1Wk33", result.IterationPath);
			Assert.Single(result.AreaPaths);
			Assert.Equal("OneCRM\\AI\\ContactCenter", result.AreaPaths[0]);
			Assert.Single(result.SlaOverrides);
			Assert.Equal(3, result.SlaOverrides["Task"]);
		}

		[Fact]
		public async Task GetByTeamIdAsync_ReturnsNull_WhenTeamIdIsNull()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			// Act
			var result = await repo.GetByTeamIdAsync(null!);

			// Assert
			Assert.Null(result);
			storageMock.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task GetByTeamIdAsync_ReturnsNull_WhenTeamIdIsEmpty()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			// Act
			var result = await repo.GetByTeamIdAsync(string.Empty);

			// Assert
			Assert.Null(result);
			storageMock.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task GetByTeamIdAsync_ReturnsNull_WhenTeamIdIsWhitespace()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			// Act
			var result = await repo.GetByTeamIdAsync("   ");

			// Assert
			Assert.Null(result);
			storageMock.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task GetAllTeamsAsync_ReturnsEmptyList_WhenStorageIsNotCosmosDb()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			// Act
			var result = await repo.GetAllTeamsAsync();

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
			// Storage client should not be called (cross-partition query not supported on mock)
			storageMock.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task GetAllTeamsAsync_LogsWarning_WhenStorageIsNotCosmosDb()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			// Act
			var result = await repo.GetAllTeamsAsync();

			// Assert
			// Verify warning was logged about storage type
			loggerMock.Verify(
				x => x.Log(
					LogLevel.Warning,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unable to access CosmosDbStorageClient")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.Once);
		}

		[Fact]
		public async Task UpsertAsync_ThrowsException_WhenTeamIdIsNull()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			var document = new TeamConfigurationDocument
			{
				TeamId = null!, // Invalid
				TeamName = "Test Team"
			};

			// Act & Assert
			await Assert.ThrowsAsync<ArgumentException>(
				async () => await repo.UpsertAsync(document));
		}

		[Fact]
		public async Task UpsertAsync_ThrowsException_WhenTeamIdIsEmpty()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			var document = new TeamConfigurationDocument
			{
				TeamId = string.Empty, // Invalid
				TeamName = "Test Team"
			};

			// Act & Assert
			await Assert.ThrowsAsync<ArgumentException>(
				async () => await repo.UpsertAsync(document));
		}

		[Fact]
		public async Task UpsertAsync_SetsIdAndPartitionKey()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			storageMock
				.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<TeamConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			var document = new TeamConfigurationDocument
			{
				TeamId = "contact-center-ai",
				TeamName = "Contact Center AI"
			};

			// Act
			var result = await repo.UpsertAsync(document);

			// Assert
			Assert.Equal("contact-center-ai", result.Id);
			Assert.Equal("team-config:contact-center-ai", result.PartitionKey); // RepositoryBase prefixes with ObjectTypeCode
			Assert.Equal("contact-center-ai", result.TeamId);
		}

		[Fact]
		public async Task UpsertAsync_SetsCreatedAt_WhenNotSet()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			storageMock
				.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<TeamConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			var document = new TeamConfigurationDocument
			{
				TeamId = "contact-center-ai",
				TeamName = "Contact Center AI",
				CreatedAt = default // Not set
			};

			// Act
			var result = await repo.UpsertAsync(document);

			// Assert
			Assert.NotEqual(default, result.CreatedAt);
			Assert.True(result.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
		}

		[Fact]
		public async Task UpsertAsync_UpdatesUpdatedAt()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			storageMock
				.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<TeamConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			var document = new TeamConfigurationDocument
			{
				TeamId = "contact-center-ai",
				TeamName = "Contact Center AI",
				CreatedAt = DateTime.UtcNow.AddDays(-7),
				UpdatedAt = null
			};

			// Act
			var result = await repo.UpsertAsync(document);

			// Assert
			Assert.NotNull(result.UpdatedAt);
			Assert.True(result.UpdatedAt > DateTime.UtcNow.AddMinutes(-1));
		}

		[Fact]
		public async Task UpsertAsync_CallsStorageUpdate()
		{
			// Arrange
			var storageMock = new Mock<IStorageClient<TeamConfigurationDocument, string>>();
			storageMock
				.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<TeamConfigurationDocument>()))
				.Returns(Task.CompletedTask);

			var loggerMock = new Mock<ILogger<TeamConfigurationRepository>>();
			var repo = new TeamConfigurationRepository(loggerMock.Object, storageMock.Object);

			var document = new TeamConfigurationDocument
			{
				TeamId = "contact-center-ai",
				TeamName = "Contact Center AI",
				IterationPath = "OneCRM\\FY26\\Q3\\1Wk\\1Wk33",
				AreaPaths = new List<string> { "OneCRM\\AI\\ContactCenter" },
				SlaOverrides = new Dictionary<string, int> { { "Task", 3 } }
			};

			// Act
			await repo.UpsertAsync(document);

			// Assert
			storageMock.Verify(
				s => s.UpdateAsync("contact-center-ai", It.Is<TeamConfigurationDocument>(d =>
					d.TeamId == "contact-center-ai" &&
					d.TeamName == "Contact Center AI" &&
					d.Id == "contact-center-ai" &&
					d.PartitionKey == "team-config:contact-center-ai")), // Partition key is prefixed by RepositoryBase
				Times.Once);
		}

		[Fact]
		public void TeamConfigurationDocument_DefaultValues_AreCorrect()
		{
			// Arrange & Act
			var document = new TeamConfigurationDocument();

			// Assert
			Assert.Empty(document.TeamId);
			Assert.Empty(document.TeamName);
			Assert.Empty(document.IterationPath);
			Assert.NotNull(document.AreaPaths);
			Assert.Empty(document.AreaPaths);
			Assert.NotNull(document.SlaOverrides);
			Assert.Empty(document.SlaOverrides);
			Assert.Null(document.UpdatedAt);
			Assert.Null(document.TTL); // Persistent configuration
		}

		[Fact]
		public void TeamConfigurationDocument_CanStoreMultipleAreaPaths()
		{
			// Arrange & Act
			var document = new TeamConfigurationDocument
			{
				TeamId = "auth-antifraud",
				TeamName = "Authentication & Anti-Fraud",
				AreaPaths = new List<string>
				{
					"OneCRM\\Security\\Auth",
					"OneCRM\\Security\\AntiFraud"
				}
			};

			// Assert
			Assert.Equal(2, document.AreaPaths.Count);
			Assert.Contains("OneCRM\\Security\\Auth", document.AreaPaths);
			Assert.Contains("OneCRM\\Security\\AntiFraud", document.AreaPaths);
		}

		[Fact]
		public void TeamConfigurationDocument_CanStoreMultipleSlaOverrides()
		{
			// Arrange & Act
			var document = new TeamConfigurationDocument
			{
				TeamId = "auth-antifraud",
				TeamName = "Authentication & Anti-Fraud",
				SlaOverrides = new Dictionary<string, int>
				{
					{ "Task", 3 },
					{ "Feature", 10 },
					{ "Bug", 1 }
				}
			};

			// Assert
			Assert.Equal(3, document.SlaOverrides.Count);
			Assert.Equal(3, document.SlaOverrides["Task"]);
			Assert.Equal(10, document.SlaOverrides["Feature"]);
			Assert.Equal(1, document.SlaOverrides["Bug"]);
		}
	}
}
