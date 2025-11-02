using BitFaster.Caching.Lru;
using Hermes.Storage.Core.Models;
using Hermes.Storage.Core.Exceptions;
using System;

namespace Hermes.Storage.Core.InMemory
{
	/// <summary>
	/// In-memory L1 cache storage client using BitFaster.
	/// </summary>
	public class BitFasterStorageClient<T> : IStorageClient<T, string> where T : Document
	{
		private readonly ConcurrentLru<string, T> _cache;

		/// <summary>
		/// Initializes a new instance of the <see cref="BitFasterStorageClient{T}"/> class.
		/// </summary>
		/// <param name="capacity">The maximum number of items to cache.</param>
		public BitFasterStorageClient(int capacity =1000)
		{
			_cache = new ConcurrentLru<string, T>(capacity);
		}

		/// <inheritdoc/>
		public Task CreateAsync(T item)
		{
			_ValidateItem(item);
			try
			{
				_cache.AddOrUpdate(item.Id, item);
			}
			catch (Exception ex)
			{
				_HandleException(ex, "create");
			}
			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public Task<T?> ReadAsync(string key, string partitionKey)
		{
			_ValidateKey(key);
			try
			{
				_cache.TryGet(key, out var value);
				return Task.FromResult(value);
			}
			catch (Exception ex)
			{
				_HandleException(ex, "read");
				return Task.FromResult<T?>(null);
			}
		}

		/// <inheritdoc/>
		public Task UpdateAsync(string key, T item)
		{
			_ValidateKey(key);
			_ValidateItem(item);
			try
			{
				_cache.AddOrUpdate(key, item);
			}
			catch (Exception ex)
			{
				_HandleException(ex, "update");
			}
			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public Task DeleteAsync(string key, string partitionKey)
		{
			_ValidateKey(key);
			try
			{
				_cache.TryRemove(key);
			}
			catch (Exception ex)
			{
				_HandleException(ex, "delete");
			}
			return Task.CompletedTask;
		}

		/// <summary>
		/// Validates that the item is not null and has a valid Id.
		/// </summary>
		private void _ValidateItem(T item)
		{
			if (item == null)
				throw new StorageException("Item cannot be null.", StorageExceptionTypes.ErrorCode.InvalidInput);
			if (string.IsNullOrWhiteSpace(item.Id))
				throw new StorageException("Item Id cannot be null or empty.", StorageExceptionTypes.ErrorCode.InvalidInput);
		}

		/// <summary>
		/// Validates that the key is not null or empty.
		/// </summary>
		private void _ValidateKey(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new StorageException("Key cannot be null or empty.", StorageExceptionTypes.ErrorCode.InvalidInput);
		}

		/// <summary>
		/// Handles and wraps exceptions with StorageException.
		/// </summary>
		private void _HandleException(Exception ex, string operation)
		{
			if (ex is ArgumentException)
				throw new StorageException($"Invalid argument during {operation}: " + ex.Message, ex, StorageExceptionTypes.ErrorCode.InvalidInput);
			if (ex is InvalidOperationException)
				throw new StorageException($"Operation failed during {operation}: " + ex.Message, ex, StorageExceptionTypes.ErrorCode.OperationFailed);
			throw new StorageException($"Unexpected error during {operation} operation: " + ex.Message, ex, StorageExceptionTypes.ErrorCode.OperationFailed);
		}
	}
}
