using Hermes.Storage.Repositories.HermesInstructions;

namespace Hermes.Orchestrator.Prompts
{
	/// <summary>
	/// Composes the full system prompt for a given agent type by combining
	/// common instructions and capability-specific instructions.
	/// </summary>
	public interface IAgentPromptComposer
	{
		/// <summary>
		/// Generates the prompt for the specified instruction type.
		/// </summary>
		/// <param name="instructionType">The logical instruction type (for example, ProjectAssistant).</param>
		/// <returns>A composed prompt string suitable to send as the system prompt to the agent.</returns>
		string ComposePrompt(HermesInstructionType instructionType);
	}
}
