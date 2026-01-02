using Xunit;
using Moq;
using Hermes.Storage.Repositories.HermesInstructions;
using Hermes.Storage.Core;
using Hermes.Storage.Core.Exceptions;
using Hermes.Storage.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace Hermes.Tests.Storage.Repositories.HermesInstructions
{
    public class HermesInstructionsRepositoryTests
    {
        private static HermesInstructionsRepository CreateRepository(
            List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>? primaryInitial = null,
            string? fileInstructionText = null,
            bool useFileClient = false)
        {
            var primaryMock = new Mock<IStorageClient<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions, string>>();
            primaryInitial ??= new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>();

            primaryMock.Setup(s => s.ReadAllByPartitionKeyAsync(It.IsAny<string>()))
                .ReturnsAsync((string pk) => primaryInitial.FindAll(x => x.PartitionKey == pk));
            primaryMock.Setup(s => s.CreateAsync(It.IsAny<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>()))
                .Returns(Task.CompletedTask)
                .Callback<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>(entity => primaryInitial.Add(entity));
            primaryMock.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Callback<string, string>((id, pk) => primaryInitial.RemoveAll(x => x.Id == id));
            primaryMock.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>()))
                .Returns(Task.CompletedTask)
                .Callback<string, Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>((id, entity) =>
                {
                    var idx = primaryInitial.FindIndex(x => x.Id == id);
                    if (idx >= 0) primaryInitial[idx] = entity;
                });

            IStorageClient<FileDocument, string>? fileClient = null;

            if (useFileClient)
            {
                var fileMock = new Mock<IStorageClient<FileDocument, string>>();

                // Match the repository's file client usage: ReadAsync("${partitionKey}_Instructions", "Instructions")
                fileMock.Setup(s => s.ReadAsync("ProjectAssistant_Instructions", "Instructions"))
                    .ReturnsAsync(new FileDocument
                    {
                        Id = "ProjectAssistant_Instructions",
                        PartitionKey = "Instructions",
                        Data = System.Text.Encoding.UTF8.GetBytes(fileInstructionText ?? string.Empty)
                    });

                fileClient = fileMock.Object;
            }

            return new HermesInstructionsRepository(primaryMock.Object, fileClient);
        }

        [Fact]
        public async Task GetByInstructionTypeAsync_ReturnsLatestOrSpecificVersion()
        {
            var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            try
            {
                var repo = CreateRepository(new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions> {
                    new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions("inst1", HermesInstructionType.ProjectAssistant,1),
                    new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions("inst2", HermesInstructionType.ProjectAssistant,2)
                });

                var latest = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant);
                var v1 = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant,1);

                Assert.NotNull(latest);
                Assert.NotNull(v1);
                Assert.Equal(2, latest!.Version);
                Assert.Equal(1, v1!.Version);
            }
            finally
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
            }
        }

        [Fact]
        public async Task GetByInstructionTypeAsync_UsesFileClientInDevelopment()
        {
            var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            try
            {
                var primaryInitial = new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions>();
                var fileInstruction = "fileInst";

                var repo = CreateRepository(primaryInitial, fileInstruction, useFileClient: true);

                var result = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant);

                Assert.NotNull(result);
                Assert.Equal("fileInst", result!.Instruction);
                Assert.Equal(1, result.Version);
                Assert.Equal(HermesInstructionType.ProjectAssistant, result.InstructionType);
            }
            finally
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
            }
        }

        [Fact]
        public async Task GetByInstructionTypeAsync_ReturnsNullIfNotFound()
        {
            var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            try
            {
                var repo = CreateRepository();

                var result = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant);

                Assert.Null(result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
            }
        }

        [Fact]
        public async Task CreateInstructionAsync_CreatesNewInstruction()
        {
            var repo = CreateRepository();

            await repo.CreateInstructionAsync("inst", HermesInstructionType.ProjectAssistant,1);
            var result = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant,1);

            Assert.NotNull(result);
            Assert.Equal("inst", result!.Instruction);
        }

        [Fact]
        public async Task CreateInstructionAsync_ThrowsIfExists()
        {
            var repo = CreateRepository(new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions> {
                new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions("inst", HermesInstructionType.ProjectAssistant,1)
            });

            await Assert.ThrowsAsync<StorageException>(() => repo.CreateInstructionAsync("inst", HermesInstructionType.ProjectAssistant,1));
        }

        [Fact]
        public async Task DeleteInstructionAsync_DeletesInstruction()
        {
            var repo = CreateRepository(new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions> {
                new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions("inst", HermesInstructionType.ProjectAssistant,1)
            });

            await repo.DeleteInstructionAsync(HermesInstructionType.ProjectAssistant,1);
            var result = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant,1);

            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteInstructionAsync_ThrowsIfNotFound()
        {
            var repo = CreateRepository();

            await Assert.ThrowsAsync<StorageException>(() => repo.DeleteInstructionAsync(HermesInstructionType.ProjectAssistant,1));
        }

        [Fact]
        public async Task UpdateInstructionAsync_UpdatesInstruction()
        {
            var repo = CreateRepository(new List<Hermes.Storage.Repositories.HermesInstructions.HermesInstructions> {
                new Hermes.Storage.Repositories.HermesInstructions.HermesInstructions("old", HermesInstructionType.ProjectAssistant,1)
            });

            await repo.UpdateInstructionAsync(HermesInstructionType.ProjectAssistant, "new",1);
            var result = await repo.GetByInstructionTypeAsync(HermesInstructionType.ProjectAssistant,1);

            Assert.NotNull(result);
            Assert.Equal("new", result!.Instruction);
        }

        [Fact]
        public async Task UpdateInstructionAsync_ThrowsIfNotFound()
        {
            var repo = CreateRepository();

            await Assert.ThrowsAsync<StorageException>(() => repo.UpdateInstructionAsync(HermesInstructionType.ProjectAssistant, "new",1));
        }
    }
}
