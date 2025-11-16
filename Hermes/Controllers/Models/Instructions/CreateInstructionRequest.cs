using Hermes.Storage.Repositories.HermesInstructions;

namespace Hermes.Controllers.Models.Instructions
{
	public class CreateInstructionRequest
	{
		public string Instruction { get; set; }
		public HermesInstructionType InstructionType { get; set; }
		public int Version { get; set; }
	}
}
