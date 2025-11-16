using Hermes.Storage.Repositories.HermesInstructions;

namespace Hermes.Controllers.Models.Instructions
{
	public class UpdateInstructionRequest
	{
		public HermesInstructionType InstructionType { get; set; }
		public string NewInstruction { get; set; }
		public int? Version { get; set; }
	}
}
