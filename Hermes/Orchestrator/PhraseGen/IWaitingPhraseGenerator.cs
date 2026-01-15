namespace Hermes.Orchestrator.PhraseGen
{
	/// <summary>
	/// Interface for generating fun waiting phrases during orchestration.
	/// </summary>
	public interface IWaitingPhraseGenerator
	{
		/// <summary>
		/// Generates a random creative phrase in Claude's style.
		/// </summary>
		/// <returns>A fun phrase like "splendid-soaring-sketch".</returns>
		string GeneratePhrase();
	}
}
