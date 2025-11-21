using Hermes.Storage.Core.Models;
using System;

namespace Hermes.Storage.Repositories.HermesInstructions
{
	public class HermesInstructions : Document
	{
		/// <summary>
		/// Gets or sets the instruction string for the agent.
		/// </summary>
		public string Instruction { get; set; }

		/// <summary>
		/// Gets or sets the type of instruction for the agent.
		/// </summary>
		public HermesInstructionType InstructionType { get; set; }

		/// <summary>
		/// Gets or sets the version number for the instruction. Used for version control.
		/// </summary>
		public int Version { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="HermesInstructions"/> class with the specified instruction, instruction type, and version.
		/// Sets the partition key as the instruction type.
		/// </summary>
		/// <param name="instruction">The instruction string for the agent.</param>
		/// <param name="instructionType">The type of instruction for the agent.</param>
		/// <param name="version">The version number for the instruction.</param>
		public HermesInstructions(string instruction, HermesInstructionType instructionType, int version)
		{
			Id = Guid.NewGuid().ToString();
			Instruction = instruction ?? throw new ArgumentNullException(nameof(instruction));
			InstructionType = instructionType;
			Version = version;
			PartitionKey = instructionType.ToString();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HermesInstructions"/> class with the specified instruction and instruction type.
		/// Sets the partition key as the instruction type. Version defaults to0.
		/// </summary>
		/// <param name="instruction">The instruction string for the agent.</param>
		/// <param name="instructionType">The type of instruction for the agent.</param>
		public HermesInstructions(string instruction, HermesInstructionType instructionType)
			: this(instruction, instructionType,0) { }
	}
}
