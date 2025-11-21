using Xunit;
using Moq;
using Hermes.Storage.Repositories.HermesInstructions;
using Hermes.Storage.Core;
using Hermes.Storage.Core.Exceptions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hermes.Tests.Storage.Repositories.HermesInstructions
{
	public class HermesInstructionsRepositoryTests
	{
		private static HermesInstructionsRepository CreateRepository(List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>? initial = null)
		{
			var storageMock = new Mock<IStorageClient<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions, string>>();
			initial ??= new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>();
			storageMock.Setup(s => s.ReadAllByPartitionKeyAsync(It.IsAny<string>()))
				.ReturnsAsync((string pk) => initial.FindAll(x => x.PartitionKey == pk));
			storageMock.Setup(s => s.CreateAsync(It.IsAny<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>()))
				.Returns(Task.CompletedTask)
				.Callback<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>(entity => initial.Add(entity));
			storageMock.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>()))
				.Returns(Task.CompletedTask)
				.Callback<string, string>((id, pk) => initial.RemoveAll(x => x.Id == id));
			storageMock.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>()))
				.Returns(Task.CompletedTask)
				.Callback<string, Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>((id, entity) => {
					var idx = initial.FindIndex(x => x.Id == id);
					if (idx >=0) initial[idx] = entity;
				});
			return new HermesInstructionsRepository(storageMock.Object);
		}

		[Fact]
		public async Task GetByInstructionTypeAsync_ReturnsLatestOrSpecificVersion()
		{
			// Arrange
			var repo = CreateRepository(new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions> {
				new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions("inst1", HermesInstructionType.ProjectAssistant,1),
				new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions("inst2", HermesInstructionType.ProjectAssistant,2)
			});

			// Act
			var latest = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant);
			var v1 = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant,1);

			// Assert
			Assert.NotNull(latest);
			Assert.NotNull(v1);
			Assert.Equal(2, latest.Version);
			Assert.Equal(1, v1.Version);
		}

		[Fact]
		public async Task GetByInstructionTypeAsync_ReturnsNullIfNotFound()
		{
			// Arrange
			var repo = CreateRepository();

			// Act
			var result = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant);

			// Assert
			Assert.Null(result);
		}

		[Fact]
		public async Task CreateInstructionAsync_CreatesNewInstruction()
		{
			// Arrange
			var repo = CreateRepository();

			// Act
			await repo.CreateInstructionAsync("inst", HermesInstructionType.ProjectAssistant,1);
			var result = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant,1);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("inst", result.Instruction);
		}

		[Fact]
		public async Task CreateInstructionAsync_ThrowsIfExists()
		{
			// Arrange
			var repo = CreateRepository(new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions> {
				new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions("inst", HermesInstructionType.ProjectAssistant,1)
			});

			// Act & Assert
			await Assert.ThrowsAsync<StorageException>(() => repo.CreateInstructionAsync("inst", HermesInstructionType.ProjectAssistant,1));
		}

		[Fact]
		public async Task DeleteInstructionAsync_DeletesInstruction()
		{
			// Arrange
			var repo = CreateRepository(new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions> {
				new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions("inst", HermesInstructionType.ProjectAssistant,1)
			});

			// Act
			await repo.DeleteInstructionAsync(HermesInstructionType.ProjectAssistant,1);
			var result = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant,1);

			// Assert
			Assert.Null(result);
		}

		[Fact]
		public async Task DeleteInstructionAsync_ThrowsIfNotFound()
		{
			// Arrange
			var repo = CreateRepository();

			// Act & Assert
			await Assert.ThrowsAsync<StorageException>(() => repo.DeleteInstructionAsync(HermesInstructionType.ProjectAssistant,1));
		}

		[Fact]
		public async Task UpdateInstructionAsync_UpdatesInstruction()
		{
			// Arrange
			var repo = CreateRepository(new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions> {
				new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions("old", HermesInstructionType.ProjectAssistant,1)
			});

			// Act
			await repo.UpdateInstructionAsync(HermesInstructionType.ProjectAssistant, "new",1);
			var result = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant,1);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("new", result.Instruction);
		}

		[Fact]
		public async Task UpdateInstructionAsync_ThrowsIfNotFound()
		{
			// Arrange
			var repo = CreateRepository();

			// Act & Assert
			await Assert.ThrowsAsync<StorageException>(() => repo.UpdateInstructionAsync(HermesInstructionType.ProjectAssistant, "new",1));
		}
	}
}
