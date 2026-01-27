using Microsoft.Azure.Cosmos;
using Hermes.Storage.Core.Models;
using Hermes.Storage.Core.Exceptions;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Hermes.Storage.Core.CosmosDB
{
	/// <summary>
	/// CosmosDB implementation of IStorageClient for NoSQL CRUD operations.
	/// </summary>
	public class CosmosDbStorageClient<T> : IStorageClient<T, string> where T : Document
	{
		private readonly CosmosClient _client;
		private Container _container;
		private readonly string _databaseId;
		private readonly string _containerId;
		private bool _initialized = false;
		private readonly object _initLock = new();

		/// <summary>
		/// Initializes a new instance of the <see cref="CosmosDbStorageClient{T}"/> class.
		/// </summary>
		/// <param name="connectionString">Cosmos DB connection string.</param>
		/// <param name="databaseId">Database ID.</param>
		/// <param name="containerId">Container ID.</param>
		/// <remarks>
		/// The constructor sets up the Cosmos DB client and container references, but does not create the database or container. Initialization occurs on first operation.
		/// </remarks>
		public CosmosDbStorageClient(string connectionString, string databaseId, string containerId)
		{
			_client = new CosmosClient(connectionString);
			_databaseId = databaseId;
			_containerId = containerId;
			_container = _client.GetContainer(databaseId, containerId);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CosmosDbStorageClient{T}"/> class for testing or mocking purposes.
		/// </summary>
		/// <param name="container">Existing Cosmos DB container instance.</param>
		public CosmosDbStorageClient(Container container)
		{
			_container = container;
			_client = null!;
			_databaseId = "";
			_containerId = "";
			_initialized = true;
		}

		private async Task InitializeAsync()
		{
			if (_initialized) return;
			lock (_initLock)
			{
				if (_initialized) return;
				_initialized = true;
			}
			var databaseResponse = await _client.CreateDatabaseIfNotExistsAsync(_databaseId);
			var containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(
				new ContainerProperties
				{
					Id = _containerId,
					PartitionKeyPath = "/partitionkey"
				});
			_container = containerResponse.Container;
		}

		private async Task EnsureInitializedAsync()
		{
			if (!_initialized)
				await InitializeAsync();
		}

		/// <inheritdoc/>
		public async Task CreateAsync(T item)
		{
			await EnsureInitializedAsync();
			_ValidateDocument(item);
			try
			{
				var partitionKey = new PartitionKey(item.PartitionKey);
                await _container.CreateItemAsync(item, new PartitionKey(item.PartitionKey));
			}
			catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
			{
				throw new StorageException($"Document with id '{item.Id}' already exists.", ex, StorageExceptionTypes.ErrorCode.AlreadyExists);
			}
			catch (Exception ex)
			{
				_HandleCosmosException(ex, "create");
			}
		}

		/// <inheritdoc/>
		public async Task<T?> ReadAsync(string key, string partitionKey)
		{
			await EnsureInitializedAsync();
			_ValidateKeyAndPartition(key, partitionKey);
			try
			{
				var response = await _container.ReadItemAsync<T>(key, new PartitionKey(partitionKey));
				return response.Resource;
			}
			catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
			{
				return null;
			}
			catch (Exception ex)
			{
				_HandleCosmosException(ex, "read");
				return null; // Unreachable, but required for compilation
			}
		}

		/// <inheritdoc/>
		public async Task UpdateAsync(string key, T item)
		{
			await EnsureInitializedAsync();
			_ValidateDocument(item);
			try
			{
				await _container.UpsertItemAsync(item, new PartitionKey(item.PartitionKey));
			}
			catch (Exception ex)
			{
				_HandleCosmosException(ex, "update");
			}
		}

		/// <inheritdoc/>
		public async Task DeleteAsync(string key, string partitionKey)
		{
			await EnsureInitializedAsync();
			_ValidateKeyAndPartition(key, partitionKey);
			try
			{
				await _container.DeleteItemAsync<T>(key, new PartitionKey(partitionKey));
			}
			catch (Exception ex)
			{
				_HandleCosmosException(ex, "delete");
			}
		}

		/// <inheritdoc/>
		public async Task<IReadOnlyList<T>?> ReadAllByPartitionKeyAsync(string partitionKey)
		{
			await EnsureInitializedAsync();
			if (string.IsNullOrWhiteSpace(partitionKey))
				throw new StorageException("PartitionKey cannot be null or empty.", StorageExceptionTypes.ErrorCode.InvalidInput);

			var query = new QueryDefinition("SELECT * FROM c WHERE c.partitionkey = @partitionKey")
				.WithParameter("@partitionKey", partitionKey);

			var results = new List<T>();
			using (FeedIterator<T> resultSet = _container.GetItemQueryIterator<T>(query))
			{
				while (resultSet.HasMoreResults)
				{
					FeedResponse<T> response = await resultSet.ReadNextAsync();
					results.AddRange(response);
				}
			}
			return results;
		}

	/// <summary>
	/// Executes a cross-partition query with the specified query definition.
	/// WARNING: Cross-partition queries are expensive in CosmosDB and consume significant RUs.
	/// Use sparingly and only when necessary.
	/// </summary>
	/// <param name="queryDefinition">The query definition to execute.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of documents matching the query.</returns>
	public async Task<List<T>> QueryAsync(
		QueryDefinition queryDefinition,
		CancellationToken cancellationToken = default)
	{
		await EnsureInitializedAsync();
		if (queryDefinition == null)
			throw new StorageException("QueryDefinition cannot be null.", StorageExceptionTypes.ErrorCode.InvalidInput);

		var results = new List<T>();
		try
		{
			// Enable cross-partition query
			var queryRequestOptions = new QueryRequestOptions
			{
				MaxItemCount = -1 // Retrieve all items
			};

			using (FeedIterator<T> resultSet = _container.GetItemQueryIterator<T>(
				queryDefinition,
				requestOptions: queryRequestOptions))
			{
				while (resultSet.HasMoreResults)
				{
					FeedResponse<T> response = await resultSet.ReadNextAsync(cancellationToken);
					results.AddRange(response);
				}
			}
		}
		catch (Exception ex)
		{
			_HandleCosmosException(ex, "query");
		}

		return results;
	}

		/// <summary>
		/// Handles and wraps common CosmosDB exceptions with StorageException.
		/// </summary>
		private void _HandleCosmosException(Exception ex, string operation)
		{
			if (ex is CosmosException cosmosEx)
			{
				if (cosmosEx.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
					throw new StorageException($"CosmosDB request timed out during {operation}: " + cosmosEx.Message, cosmosEx, StorageExceptionTypes.ErrorCode.Timeout);
				if (cosmosEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
					throw new StorageException($"CosmosDB permission denied during {operation}: " + cosmosEx.Message, cosmosEx, StorageExceptionTypes.ErrorCode.PermissionDenied);
				if (cosmosEx.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
					throw new StorageException($"CosmosDB service unavailable during {operation}: " + cosmosEx.Message, cosmosEx, StorageExceptionTypes.ErrorCode.ConnectionFailed);
				throw new StorageException($"CosmosDB operation failed during {operation}: " + cosmosEx.Message, cosmosEx, StorageExceptionTypes.ErrorCode.OperationFailed);
			}
			if (ex is JsonSerializationException)
				throw new StorageException($"Serialization error during {operation}: " + ex.Message, ex, StorageExceptionTypes.ErrorCode.SerializationError);
			if (ex is SocketException)
				throw new StorageException($"Network error during {operation}: " + ex.Message, ex, StorageExceptionTypes.ErrorCode.ConnectionFailed);
			if (ex is ArgumentException)
				throw new StorageException($"Invalid argument during {operation}: " + ex.Message, ex, StorageExceptionTypes.ErrorCode.InvalidInput);
			throw new StorageException($"Unexpected error during {operation} operation: " + ex.Message, ex, StorageExceptionTypes.ErrorCode.OperationFailed);
		}

		/// <summary>
		/// Validates that the document and its required properties are not null or empty.
		/// </summary>
		private void _ValidateDocument(T item)
		{
			if (item == null)
				throw new StorageException("Item cannot be null.", StorageExceptionTypes.ErrorCode.InvalidInput);
			if (string.IsNullOrWhiteSpace(item.Id))
				throw new StorageException("Document Id cannot be null or empty.", StorageExceptionTypes.ErrorCode.InvalidInput);
			if (string.IsNullOrWhiteSpace(item.PartitionKey))
				throw new StorageException("PartitionKey cannot be null or empty.", StorageExceptionTypes.ErrorCode.InvalidInput);
		}

		/// <summary>
		/// Validates that the key and partition key are not null or empty.
		/// </summary>
		private void _ValidateKeyAndPartition(string key, string partitionKey)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new StorageException("Document key cannot be null or empty.", StorageExceptionTypes.ErrorCode.InvalidInput);
			if (string.IsNullOrWhiteSpace(partitionKey))
				throw new StorageException("PartitionKey cannot be null or empty.", StorageExceptionTypes.ErrorCode.InvalidInput);
		}
	}
}
