using Hermes.Storage.Repositories.HermesInstructions;
using System.Text.Json.Serialization;

namespace Hermes.Controllers.Models.Instructions
{
	/// <summary>
	/// Represents a request to update an existing Hermes instruction.
	/// </summary>
	/// <remarks>
	/// This model is used to submit updated instruction data to the Hermes API, including the new instruction text, type, and optional version.
	/// </remarks>
	public class UpdateInstructionRequest
	{
        /// <summary>
        /// The new instruction text to be applied.
        /// </summary>
        [JsonPropertyName("newInstruction")]
        public required string NewInstruction { get; set; }

        /// <summary>
        /// The type of instruction to update, as defined by <see cref="HermesInstructionType"/>.
        /// </summary>
        [JsonPropertyName("instructionType")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HermesInstructionType InstructionType { get; set; }

		/// <summary>
		/// The optional version number for the updated instruction.
		/// </summary>
		[JsonPropertyName("version")]
		public int? Version { get; set; }
	}
}
