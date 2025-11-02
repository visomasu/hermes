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
		/// Initializes a new instance of the <see cref="RepositoryBase{T}"/> class.
		/// </summary>
		/// <param name="storage">The storage client to use.</param>
		protected RepositoryBase(IStorageClient<T, string> storage)
		{
			_storage = storage;
		}

		/// <inheritdoc/>
		public virtual Task CreateAsync(T entity)
		{
			_ValidateEntity(entity);

			return _storage.CreateAsync(entity);
		}

		/// <inheritdoc/>
		public virtual Task<T?> ReadAsync(string key)
		{
			_ValidateKey(key);

			return _storage.ReadAsync(key, key); // Default: use key as partitionKey
		}

		/// <inheritdoc/>
		public virtual Task UpdateAsync(string key, T entity)
		{
			_ValidateKey(key);
			_ValidateEntity(entity);

			return _storage.UpdateAsync(key, entity);
		}

		/// <inheritdoc/>
		public virtual Task DeleteAsync(string key)
		{
			_ValidateKey(key);

			return _storage.DeleteAsync(key, key); // Default: use key as partitionKey
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
