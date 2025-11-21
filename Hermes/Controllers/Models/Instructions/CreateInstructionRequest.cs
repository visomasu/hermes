using Hermes.Storage.Repositories.HermesInstructions;

namespace Hermes.Controllers.Models.Instructions
{
	/// <summary>
	/// Represents a request to create a new Hermes instruction.
	/// </summary>
	/// <remarks>
	/// This model is used to submit instruction data to the Hermes API, including the instruction text, type, and version.
	/// </remarks>
	public class CreateInstructionRequest
	{
		/// <summary>
		/// The instruction text to be created.
		/// </summary>
		public required string Instruction { get; set; }

		/// <summary>
		/// The type of instruction, as defined by <see cref="HermesInstructionType"/>.
		/// </summary>
		public HermesInstructionType InstructionType { get; set; }

		/// <summary>
		/// The version number of the instruction.
		/// </summary>
		public int Version { get; set; }
	}
}
