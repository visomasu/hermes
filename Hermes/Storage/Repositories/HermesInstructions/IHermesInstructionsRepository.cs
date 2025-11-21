using Hermes.Storage.Core.Models;

namespace Hermes.Storage.Repositories.HermesInstructions
{
	/// <summary>
	/// Interface for repository operations on HermesInstructions entities.
	/// </summary>
	public interface IHermesInstructionsRepository
	{
		/// <summary>
		/// Retrieves a HermesInstructions entity by its instruction type.
		/// Returns the latest version by default, or a specific version if provided.
		/// </summary>
		/// <param name="instructionType">The type of instruction to retrieve.</param>
		/// <param name="version">The specific version to retrieve (optional).</param>
		/// <returns>A task representing the asynchronous operation, with the HermesInstructions entity matching the specified type and version, or the latest version if not specified. Returns null if not found.</returns>
		Task<HermesInstructions?> GetByInstructionTypeAsync(HermesInstructionType instructionType, int? version = null);

		/// <summary>
		/// Creates a new HermesInstructions entity for the given instruction type and version if one does not already exist.
		/// Throws StorageException if a record already exists for the instruction type and version.
		/// </summary>
		/// <param name="instruction">The instruction string for the agent.</param>
		/// <param name="instructionType">The type of instruction for the agent.</param>
		/// <param name="version">The version number for the instruction.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		/// <exception cref="StorageException">Thrown if a record with the same instruction type and version already exists.</exception>
		Task CreateInstructionAsync(string instruction, HermesInstructionType instructionType, int version);

		/// <summary>
		/// Deletes a HermesInstructions entity by its instruction type and version.
		/// Throws StorageException if the record does not exist.
		/// </summary>
		/// <param name="instructionType">The type of instruction to delete.</param>
		/// <param name="version">The version number of the instruction to delete.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		/// <exception cref="StorageException">Thrown if the record does not exist.</exception>
		Task DeleteInstructionAsync(HermesInstructionType instructionType, int version);

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
		Task UpdateInstructionAsync(HermesInstructionType instructionType, string newInstruction, int? version = null);
	}
}
