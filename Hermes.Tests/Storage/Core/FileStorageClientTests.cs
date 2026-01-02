using System;
using System.IO;
using System.Threading.Tasks;
using Hermes.Storage.Core.File;
using Hermes.Storage.Core.Models;
using Hermes.Storage.Core.Exceptions;
using Xunit;

namespace Hermes.Tests.Storage.Core
{
    public class FileStorageClientTests
    {
        private static string CreateTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "Hermes_FileStorageClientTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        [Fact]
        public void Constructor_InvalidRootPath_Throws()
        {
            Assert.Throws<StorageException>(() => new FileStorageClient(""));
        }

        [Fact]
        public async Task ReadAsync_FileExists_ReturnsFileDocument()
        {
            var root = CreateTempRoot();
            var partition = "partition1";
            var id = "doc1.json";
            var partitionFolder = Path.Combine(root, partition);
            Directory.CreateDirectory(partitionFolder);
            var filePath = Path.Combine(partitionFolder, id);

            var expectedBytes = System.Text.Encoding.UTF8.GetBytes("test-content");
            await File.WriteAllBytesAsync(filePath, expectedBytes);

            var client = new FileStorageClient(root);

            var result = await client.ReadAsync(id, partition);

            Assert.NotNull(result);
            Assert.Equal(id, result!.Id);
            Assert.Equal(partition, result.PartitionKey);
            Assert.Equal(expectedBytes, result.Data);
        }

        [Fact]
        public async Task ReadAsync_FileDoesNotExist_ReturnsNull()
        {
            var root = CreateTempRoot();
            var client = new FileStorageClient(root);

            var result = await client.ReadAsync("missing.json", "partition1");

            Assert.Null(result);
        }

        [Fact]
        public async Task ReadAsync_InvalidKey_Throws()
        {
            var root = CreateTempRoot();
            var client = new FileStorageClient(root);

            await Assert.ThrowsAsync<StorageException>(() => client.ReadAsync("", "partition1"));
        }

        [Fact]
        public async Task ReadAsync_InvalidPartitionKey_Throws()
        {
            var root = CreateTempRoot();
            var client = new FileStorageClient(root);

            await Assert.ThrowsAsync<StorageException>(() => client.ReadAsync("doc1.json", ""));
        }
    }
}
