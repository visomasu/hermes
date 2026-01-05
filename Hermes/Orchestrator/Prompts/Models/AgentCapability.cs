using System.Text.Json.Serialization;

namespace Hermes.Orchestrator.Prompts.Models
{
	/// <summary>
	/// Describes a single capability entry from an agentspec.json configuration.
	/// </summary>
	/// <remarks>
	/// Each capability links a logical identifier and description to a concrete
	/// instruction file on disk via <see cref="RelativePath"/>. The
	/// <c>AgentPromptComposer</c> reads these entries and loads the referenced
	/// instruction files when building the prompt for an agent.
	/// </remarks>
	public sealed class AgentCapability
	{
		/// <summary>
		/// Gets or sets the unique identifier for the capability (for example, "newsletter-generation").
		/// </summary>
		[JsonPropertyName("id")]
		public string? Id { get; set; }

		/// <summary>
		/// Gets or sets the human-readable name of the capability.
		/// </summary>
		[JsonPropertyName("name")]
		public string? Name { get; set; }

		/// <summary>
		/// Gets or sets an optional description of what the capability does.
		/// </summary>
		[JsonPropertyName("description")]
		public string? Description { get; set; }
	}
}
