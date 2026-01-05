using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hermes.Orchestrator.Prompts.Models
{
	/// <summary>
	/// Represents the root configuration for an agent as defined in an agentspec.json file.
	/// </summary>
	/// <remarks>
	/// An agent spec typically describes the high-level purpose of the agent and the
	/// list of capabilities it supports. The <see cref="AgentPromptComposer"/> uses
	/// this model to discover which capability instruction files to include when
	/// composing the final system prompt.
	/// </remarks>
	public sealed class AgentSpec
	{
		/// <summary>
		/// Gets or sets the logical name of the agent (for example, "ProjectAssistant").
		/// </summary>
		[JsonPropertyName("name")]
		public string? Name { get; set; }

		/// <summary>
		/// Gets or sets a human-readable description of the agent's purpose.
		/// </summary>
		[JsonPropertyName("description")]
		public string? Description { get; set; }

		/// <summary>
		/// Gets or sets the collection of capability definitions declared for the agent.
		/// </summary>
		[JsonPropertyName("capabilities")]
		public List<AgentCapability>? Capabilities { get; set; }
	}
}
