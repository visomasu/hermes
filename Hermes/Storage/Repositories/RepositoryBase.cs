using Hermes.Storage.Core;
using Hermes.Storage.Core.Models;
using Hermes.Storage.Core.Exceptions;

namespace Hermes.Storage.Repositories
{
	/// <summary>
	/// Abstract base repository that uses a hierarchical storage client for persistence.
	/// </summary>
	/// <typeparam name="T">The type of the entity, must inherit from Document.</typeparam>
	public abstract class RepositoryBase<T> : IRepository<T> where T : Document
	{
		/// <summary>
		/// The storage client used for persistence.
		/// </summary>
		protected readonly IStorageClient<T, string> _storage;

		/// <summary>
		/// Object type code for this repository's documents.
		/// Used to prevent partition key collisions between different document types.
		/// Must be implemented by derived classes.
		/// Examples: "conv", "notif-generic", "notif-workitem", "user-config"
		/// </summary>
		protected abstract string ObjectTypeCode { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="RepositoryBase{T}"/> class.
		/// </summary>
		/// <param name="storage">The storage client to use.</param>
		protected RepositoryBase(IStorageClient<T, string> storage)
		{
			_storage = storage;
		}

		/// <summary>
		/// Applies the object type code prefix to a partition key value.
		/// Format: "{objectTypeCode}:{partitionKey}" (e.g., "conv:user-123")
		/// </summary>
		/// <param name="partitionKey">The raw partition key value.</param>
		/// <returns>Prefixed partition key for storage.</returns>
		private string _ApplyPartitionKeyPrefix(string partitionKey)
		{
			// Check if already prefixed to avoid double-prefixing (e.g., when updating documents from storage)
		var prefix = $"{ObjectTypeCode}:";
		if (partitionKey.StartsWith(prefix, StringComparison.Ordinal))
		{
			return partitionKey;
		}
		return $"{ObjectTypeCode}:{partitionKey}";
		}

		/// <inheritdoc/>
		public virtual Task CreateAsync(T entity)
		{
			_ValidateEntity(entity);

			// Apply partition key prefix before storing
			entity.PartitionKey = _ApplyPartitionKeyPrefix(entity.PartitionKey);

			return _storage.CreateAsync(entity);
		}

		/// <summary>
		/// Reads an entity from the repository by key and partition key.
		/// </summary>
		/// <param name="key">The string key of the entity to read.</param>
		/// <param name="partitionKey">The raw partition key value (without prefix).</param>
		/// <returns>The entity if found, otherwise null.</returns>
		public virtual Task<T?> ReadAsync(string key, string partitionKey)
		{
			_ValidateKey(key);
			_ValidateKey(partitionKey);

			// Apply partition key prefix before reading
			var prefixedPartitionKey = _ApplyPartitionKeyPrefix(partitionKey);
			return _storage.ReadAsync(key, prefixedPartitionKey);
		}

		/// <inheritdoc/>
		public virtual Task UpdateAsync(string key, T entity)
		{
			_ValidateKey(key);
			_ValidateEntity(entity);

			// Apply partition key prefix before updating
			entity.PartitionKey = _ApplyPartitionKeyPrefix(entity.PartitionKey);

			return _storage.UpdateAsync(key, entity);
		}

		/// <summary>
		/// Deletes an entity from the repository by key and partition key.
		/// </summary>
		/// <param name="key">The string key of the entity to delete.</param>
		/// <param name="partitionKey">The raw partition key value (without prefix).</param>
		public virtual Task DeleteAsync(string key, string partitionKey)
		{
			_ValidateKey(key);
			_ValidateKey(partitionKey);

			// Apply partition key prefix before deleting
			var prefixedPartitionKey = _ApplyPartitionKeyPrefix(partitionKey);
			return _storage.DeleteAsync(key, prefixedPartitionKey);
		}

        /// <inheritdoc/>
        public virtual Task<IReadOnlyList<T>?> ReadAllByPartitionKeyAsync(string partitionKey)
		{
			if (string.IsNullOrWhiteSpace(partitionKey))
				throw new StorageException("Partition key cannot be null or empty.", StorageExceptionTypes.ErrorCode.InvalidInput);

			// Apply partition key prefix before reading all
			var prefixedPartitionKey = _ApplyPartitionKeyPrefix(partitionKey);
			return _storage.ReadAllByPartitionKeyAsync(prefixedPartitionKey);
		}

		/// <summary>
		/// Validates that the entity is not null and has a valid Id.
		/// </summary>
		private void _ValidateEntity(T entity)
		{
			if (entity == null)
				throw new StorageException("Entity cannot be null.", StorageExceptionTypes.ErrorCode.InvalidInput);
			if (string.IsNullOrWhiteSpace(entity.Id))
				throw new StorageException("Entity Id cannot be null or empty.", StorageExceptionTypes.ErrorCode.InvalidInput);
		}

		/// <summary>
		/// Validates that the key is not null or empty.
		/// </summary>
		private void _ValidateKey(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new StorageException("Key cannot be null or empty.", StorageExceptionTypes.ErrorCode.InvalidInput);
		}
	}
}
