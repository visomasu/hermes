using Hermes.Storage.Core;
using Hermes.Storage.Core.Exceptions;
using System.Linq;
using System.Threading.Tasks;

namespace Hermes.Storage.Repositories.HermesInstructions
{
	/// <summary>
	/// In-memory implementation of IHermesInstructionsRepository.
	/// </summary>
	public class HermesInstructionsRepository : RepositoryBase<HermesInstructions>, IHermesInstructionsRepository
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="HermesInstructionsRepository"/> class.
		/// </summary>
		/// <param name="storageClient">The storage client used for persistence.</param>
		public HermesInstructionsRepository(IStorageClient<HermesInstructions, string> storageClient) : base(storageClient) { }

		/// <summary>
		/// Retrieves a HermesInstructions entity by its instruction type.
		/// Returns the latest version by default, or a specific version if provided.
		/// </summary>
		/// <param name="instructionType">The type of instruction to retrieve.</param>
		/// <param name="version">The specific version to retrieve (optional).</param>
		/// <returns>The HermesInstructions entity matching the specified type and version, or the latest version if not specified. Returns null if not found.</returns>
		public async Task<HermesInstructions?> GetByInstructionTypeAsync(HermesInstructionType instructionType, int? version = null)
		{
			var partitionKey = instructionType.ToString();
			var allRecords = await ReadAllByPartitionKeyAsync(partitionKey);
			if (allRecords == null || allRecords.Count ==0)
				return null;

			if (version.HasValue)
			{
				return allRecords.FirstOrDefault(x => x.Version == version.Value);
			}
			// Return the record with the highest version
			return allRecords.OrderBy(x => x.Version).LastOrDefault();
		}

		/// <summary>
		/// Creates a new HermesInstructions entity for the given instruction type and version if one does not already exist.
		/// Throws StorageException if a record already exists for the instruction type and version.
		/// </summary>
		/// <param name="instruction">The instruction string for the agent.</param>
		/// <param name="instructionType">The type of instruction for the agent.</param>
		/// <param name="version">The version number for the instruction.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		/// <exception cref="StorageException">Thrown if a record with the same instruction type and version already exists.</exception>
		public async Task CreateInstructionAsync(string instruction, HermesInstructionType instructionType, int version)
		{
			var partitionKey = instructionType.ToString();
			var allRecords = await ReadAllByPartitionKeyAsync(partitionKey);
			var existing = allRecords?.FirstOrDefault(x => x.Version == version);
			if (existing != null)
			{
				throw new StorageException($"Instruction for type '{instructionType}' and version '{version}' already exists.", StorageExceptionTypes.ErrorCode.AlreadyExists);
			}
			var entity = new HermesInstructions(instruction, instructionType, version);
			await CreateAsync(entity);
		}

		/// <summary>
		/// Deletes a HermesInstructions entity by its instruction type and version.
		/// Throws StorageException if the record does not exist.
		/// </summary>
		/// <param name="instructionType">The type of instruction to delete.</param>
		/// <param name="version">The version number of the instruction to delete.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		/// <exception cref="StorageException">Thrown if the record does not exist.</exception>
		public async Task DeleteInstructionAsync(HermesInstructionType instructionType, int version)
		{
			var record = await GetByInstructionTypeAsync(instructionType, version);
			if (record == null)
			{
				throw new StorageException($"Instruction for type '{instructionType}' and version '{version}' does not exist.", StorageExceptionTypes.ErrorCode.NotFound);
			}
			await DeleteAsync(record.Id, record.PartitionKey);
		}

		/// <summary>
		/// Updates a HermesInstructions entity by its instruction type and version.
		/// If no version is provided, updates the latest version.
		/// Throws StorageException if the record does not exist.
		/// </summary>
		/// <param name="instructionType">The type of instruction to update.</param>
		/// <param name="newInstruction">The new instruction string to update.</param>
		/// <param name="version">The version number of the instruction to update (optional).</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		/// <exception cref="StorageException">Thrown if the record does not exist.</exception>
		public async Task UpdateInstructionAsync(HermesInstructionType instructionType, string newInstruction, int? version = null)
		{
			var record = await GetByInstructionTypeAsync(instructionType, version);
			if (record == null)
			{
				var versionText = version.HasValue ? version.Value.ToString() : "latest";
				throw new StorageException($"Instruction for type '{instructionType}' and version '{versionText}' does not exist.", StorageExceptionTypes.ErrorCode.NotFound);
			}

			record.Instruction = newInstruction;
			await UpdateAsync(record.Id, record);
		}
	}
}
