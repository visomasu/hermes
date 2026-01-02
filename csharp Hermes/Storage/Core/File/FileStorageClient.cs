using Hermes.Storage.Core.Exceptions;
using Hermes.Storage.Core.Models;
using System.Collections.Concurrent;

namespace Hermes.Storage.Core.File
{
    /// <summary>
    /// File-based implementation of IStorageClient specialized for FileDocument.
    /// Currently only supports read operations; other methods are not implemented.
    /// The partition key is treated as a folder path under the configured root, and the key/id
    /// is treated as the full file name (including extension) within that partition folder.
    /// </summary>
    public class FileStorageClient : IStorageClient<FileDocument, string>
    {
        private readonly string _rootPath;
        private readonly ConcurrentDictionary<string, object> _partitionLocks = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStorageClient"/> class with the specified root path.
        /// </summary>
        /// <param name="rootPath">Root folder under which partition-key-based folders and files are located.</param>
        public FileStorageClient(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new StorageException(
                    "Root path for FileStorageClient cannot be null or empty.",
                    StorageExceptionTypes.ErrorCode.InvalidInput);
            }

            _rootPath = Path.GetFullPath(rootPath);

            if (!Directory.Exists(_rootPath))
            {
                Directory.CreateDirectory(_rootPath);
            }
        }

        /// <inheritdoc />
        public Task CreateAsync(FileDocument item)
        {
            throw new NotImplementedException("FileStorageClient currently only supports read operations.");
        }

        /// <inheritdoc />
        public async Task<FileDocument?> ReadAsync(string key, string partitionKey)
        {
            ValidateKeyAndPartition(key, partitionKey);

            var filePath = GetFilePath(key, partitionKey);

            if (!System.IO.File.Exists(filePath))
            {
                return default;
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(filePath).ConfigureAwait(false);

            return new FileDocument
            {
                Id = key,
                PartitionKey = partitionKey,
                Data = bytes
            };
        }

        /// <inheritdoc />
        public Task UpdateAsync(string key, FileDocument item)
        {
            throw new NotImplementedException("FileStorageClient currently only supports read operations.");
        }

        /// <inheritdoc />
        public Task DeleteAsync(string key, string partitionKey)
        {
            throw new NotImplementedException("FileStorageClient currently only supports read operations.");
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<FileDocument>?> ReadAllByPartitionKeyAsync(string partitionKey)
        {
            throw new NotImplementedException("FileStorageClient currently only supports read operations.");
        }

        private string GetPartitionFolder(string partitionKey)
        {
            var safePartition = Sanitize(partitionKey);
            return Path.Combine(_rootPath, safePartition);
        }

        private string GetFilePath(string id, string partitionKey)
        {
            var partitionFolder = GetPartitionFolder(partitionKey);
            if (!Directory.Exists(partitionFolder))
            {
                Directory.CreateDirectory(partitionFolder);
            }

            // Treat key/id as full file name (with extension) but sanitize invalid characters
            var safeFileName = Sanitize(id);
            return Path.Combine(partitionFolder, safeFileName);
        }

        private static string Sanitize(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            foreach (var c in Path.GetInvalidPathChars())
            {
                value = value.Replace(c, '_');
            }

            return value;
        }

        private static void ValidateKeyAndPartition(string key, string partitionKey)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new StorageException(
                    "Document key cannot be null or empty.",
                    StorageExceptionTypes.ErrorCode.InvalidInput);
            }

            if (string.IsNullOrWhiteSpace(partitionKey))
            {
                throw new StorageException(
                    "PartitionKey cannot be null or empty.",
                    StorageExceptionTypes.ErrorCode.InvalidInput);
            }
        }
    }
}