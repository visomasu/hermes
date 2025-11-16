using Hermes.Storage.Core.Exceptions;
using Hermes.Storage.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hermes.Storage.Core
{
    /// <summary>
    /// Hierarchical storage client that uses L1 (in-memory) and L2 (CosmosDB) storage resiliently.
    /// </summary>
    public class HierarchicalStorageClient<T> : IStorageClient<T, string> where T : Document
    {
        private readonly IStorageClient<T, string> _l1;
        private readonly IStorageClient<T, string> _l2;

        public HierarchicalStorageClient(IStorageClient<T, string> l1, IStorageClient<T, string> l2)
        {
            _l1 = l1;
            _l2 = l2;
        }

        /// <inheritdoc/>
        public async Task CreateAsync(T item)
        {
            await _l2.CreateAsync(item); // Always write to L2 first for durability
            await _l1.CreateAsync(item); // Then update L1
        }

        /// <inheritdoc/>
        public async Task<T?> ReadAsync(string key, string partitionKey)
        {
            try
            {
                var l1Result = await _l1.ReadAsync(key, partitionKey);

                if (l1Result != null)
                    return l1Result;
            }
            catch (StorageException)
            {
                // Ignore L1 errors, fallback to L2
            }

            var l2Result = await _l2.ReadAsync(key, partitionKey);
            if (l2Result != null)
            {
                // Populate L1 cache for future reads
                await _l1.UpdateAsync(key, l2Result);
            }

            return l2Result;
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(string key, T item)
        {
            await _l2.UpdateAsync(key, item); // Always update L2 first
            await _l1.UpdateAsync(key, item); // Then update L1
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string key, string partitionKey)
        {
            await _l2.DeleteAsync(key, partitionKey); // Always delete from L2 first
            await _l1.DeleteAsync(key, partitionKey); // Then delete from L1
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<T>?> ReadAllByPartitionKeyAsync(string partitionKey)
        {
            try
            {
                var l1Results = await _l1.ReadAllByPartitionKeyAsync(partitionKey);
                if (l1Results != null && l1Results.Count > 0)
                    return l1Results;
            }
            catch (StorageException)
            {
                // Ignore L1 errors, fallback to L2
            }

            var l2Results = await _l2.ReadAllByPartitionKeyAsync(partitionKey);
            if (l2Results != null && l2Results.Count > 0)
            {
                // Optionally populate L1 cache for future reads
                foreach (var item in l2Results)
                {
                    await _l1.UpdateAsync(item.Id, item);
                }
            }
            return l2Results;
        }
    }
}
