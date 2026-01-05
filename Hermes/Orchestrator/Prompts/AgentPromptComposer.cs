using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Hermes.Orchestrator.Prompts.Models;
using Hermes.Orchestrator.Prompts.Exceptions;
using Hermes.Storage.Repositories.HermesInstructions;

namespace Hermes.Orchestrator.Prompts
{
    /// <summary>
    /// Default implementation of <see cref="IAgentPromptComposer"/> that composes
    /// prompts from instruction files on disk based on the given instruction type.
    /// Note: Currently supported only in development environments.
    /// </summary>
    public sealed class AgentPromptComposer : IAgentPromptComposer
	{
		private readonly string _instructionsRootPath;

		/// <summary>
		/// Creates a new instance of <see cref="AgentPromptComposer"/>.
		/// </summary>
		/// <param name="instructionsRootPath">
		/// Root path where instruction files are stored. If null or empty,
		/// the directory of the executing assembly is used as the base.
		/// </param>
		public AgentPromptComposer(string? instructionsRootPath = null)
		{
			// Determine a sensible default root if not provided.
			var basePath = string.IsNullOrWhiteSpace(instructionsRootPath)
				? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty
				: instructionsRootPath;

			_instructionsRootPath = basePath;
		}

		/// <inheritdoc />
		public string ComposePrompt(HermesInstructionType instructionType)
		{
			var instructionFolderName = instructionType.ToString();
			var sb = new StringBuilder();

			// Load and validate the agent specification for this instruction type.
			var spec = LoadAgentSpec(instructionType, instructionFolderName);
			if (spec == null)
			{
				throw new PromptComposerException($"Failed to read/parse agent spec for instruction type '{instructionType}'.", PromptComposerErrorCode.AgentSpecInvalid);
			}

			// Common instructions for this instruction type.
			var commonPath = Path.Combine(_instructionsRootPath, "Resources", "Instructions", instructionFolderName, $"{instructionFolderName}_Common");
			AppendInstruction(sb, commonPath);

			// Append capability-specific instructions.
			AppendCapabilities(sb, instructionType, instructionFolderName, spec);

			return sb.ToString();
		}

		/// <summary>
		/// Loads and validates the <see cref="AgentSpec"/> for the given instruction type.
		/// Throws <see cref="PromptComposerException"/> when the spec is missing or invalid.
		/// </summary>
		private AgentSpec? LoadAgentSpec(HermesInstructionType instructionType, string instructionFolderName)
		{
			var agentSpecPath = Path.Combine(_instructionsRootPath, "Resources", "Instructions", instructionFolderName, "agentspec.json");
			if (!File.Exists(agentSpecPath))
			{
				throw new PromptComposerException($"Agent spec not found for instruction type '{instructionType}'. Expected path: '{agentSpecPath}'.", PromptComposerErrorCode.AgentSpecNotFound);
			}

			try
			{
				var json = File.ReadAllText(agentSpecPath);
				return JsonSerializer.Deserialize<AgentSpec>(json);
			}
			catch (Exception ex)
			{
				// If the agentspec cannot be read or parsed, surface a specific error.
				throw new PromptComposerException($"Failed to parse agent spec for instruction type '{instructionType}'.", PromptComposerErrorCode.AgentSpecInvalid, ex);
			}
		}

		/// <summary>
		/// Appends capability-specific instructions using the capability id to build
		/// the expected instruction file path under the instruction folder.
		/// </summary>
		private void AppendCapabilities(StringBuilder sb, HermesInstructionType instructionType, string instructionFolderName, AgentSpec spec)
		{
			if (spec.Capabilities == null || spec.Capabilities.Count == 0)
			{
				throw new PromptComposerException($"No capabilities defined in agent spec for instruction type '{instructionType}'.", PromptComposerErrorCode.NoCapabilitiesDefined);
			}

			foreach (var capability in spec.Capabilities)
			{
				if (string.IsNullOrWhiteSpace(capability.Id))
				{
					continue;
				}

				// Build the capability instruction path using the capability id.
				var capabilityFileName = $"{capability.Id}.txt";
				var capabilityPath = Path.Combine(
					_instructionsRootPath,
					"Resources",
					"Instructions",
					instructionFolderName,
					"Capabilities",
					capabilityFileName);

				AppendInstruction(sb, capabilityPath, addSeparator: true);
			}
		}

		/// <summary>
		/// Appends the contents of an instruction or capability file to the prompt buffer if the file exists.
		/// Optionally inserts a visual separator before the content when appending capability sections.
		/// </summary>
		private static void AppendInstruction(StringBuilder sb, string path, bool addSeparator = false)
		{
			if (!File.Exists(path))
			{
				return;
			}

			if (addSeparator && sb.Length > 0)
			{
				sb.AppendLine();
				sb.AppendLine("-----");
			}

			var content = File.ReadAllText(path);
			if (sb.Length > 0)
			{
				sb.AppendLine();
			}
			sb.AppendLine(content.Trim());
		}
	}
}
