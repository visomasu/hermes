namespace Hermes.Storage.Core
{
	/// <summary>
	/// Interface for basic CRUD operations for storage.
	/// </summary>
	/// <typeparam name="T">The type of the document being stored.</typeparam>
	/// <typeparam name="TKey">The type of the key used to identify documents.</typeparam>
	public interface IStorageClient<T, TKey>
	{
		/// <summary>
		/// Creates a new item in the storage.
		/// </summary>
		/// <param name="item">The item to create.</param>
		Task CreateAsync(T item);

		/// <summary>
		/// Reads an item from the storage by key and partition key.
		/// </summary>
		/// <param name="key">The key of the item to read.</param>
		/// <param name="partitionKey">The partition key of the item.</param>
		/// <returns>The item if found, otherwise null.</returns>
		Task<T?> ReadAsync(TKey key, string partitionKey);

		/// <summary>
		/// Updates an existing item in the storage.
		/// </summary>
		/// <param name="key">The key of the item to update.</param>
		/// <param name="item">The updated item.</param>
		Task UpdateAsync(TKey key, T item);

		/// <summary>
		/// Deletes an item from the storage by key and partition key.
		/// </summary>
		/// <param name="key">The key of the item to delete.</param>
		/// <param name="partitionKey">The partition key of the item.</param>
		Task DeleteAsync(TKey key, string partitionKey);

		/// <summary>
		/// Reads all items from the storage under a given partition key.
		/// </summary>
		/// <param name="partitionKey">The partition key to filter items.</param>
		/// <returns>A task representing the asynchronous operation, with a list of items in the partition.</returns>
		Task<IReadOnlyList<T>?> ReadAllByPartitionKeyAsync(string partitionKey);
	}
}
