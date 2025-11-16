using Hermes.Storage.Core.Models;

namespace Hermes.Storage.Repositories
{
	/// <summary>
	/// Interface for basic repository operations on entities of type Document.
	/// </summary>
	/// <typeparam name="T">The type of the entity, must inherit from Document.</typeparam>
	public interface IRepository<T> where T : Document
	{
		/// <summary>
		/// Creates a new entity in the repository.
		/// </summary>
		/// <param name="entity">The entity to create.</param>
		Task CreateAsync(T entity);

		/// <summary>
		/// Reads an entity from the repository by key.
		/// </summary>
		/// <param name="key">The string key of the entity to read.</param>
		/// <returns>The entity if found, otherwise null.</returns>
		Task<T?> ReadAsync(string key);

		/// <summary>
		/// Updates an existing entity in the repository.
		/// </summary>
		/// <param name="key">The string key of the entity to update.</param>
		/// <param name="entity">The updated entity.</param>
		Task UpdateAsync(string key, T entity);

		/// <summary>
		/// Deletes an entity from the repository by key.
		/// </summary>
		/// <param name="key">The string key of the entity to delete.</param>
		Task DeleteAsync(string key);

		/// <summary>
		/// Reads all entities from the repository under a given partition key.
		/// </summary>
		/// <param name="partitionKey">The partition key to filter entities.</param>
		/// <returns>A task representing the asynchronous operation, with a list of entities in the partition.</returns>
		Task<IReadOnlyList<T>?> ReadAllByPartitionKeyAsync(string partitionKey);
	}
}
