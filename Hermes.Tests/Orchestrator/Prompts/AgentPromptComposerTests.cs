using Hermes.Orchestrator.Prompts;
using Hermes.Orchestrator.Prompts.Exceptions;
using Hermes.Orchestrator.Prompts.Models;
using Hermes.Storage.Repositories.HermesInstructions;
using System.Text.Json;
using Xunit;

namespace Hermes.Tests.Orchestrator.Prompts
{
	public class AgentPromptComposerTests
	{
		[Fact]
		public void ComposePrompt_Throws_WhenAgentSpecMissing()
		{
			var tempRoot = CreateTempDirectory();
			try
			{
				var composer = new AgentPromptComposer(tempRoot);

				var ex = Assert.Throws<PromptComposerException>(() => composer.ComposePrompt(HermesInstructionType.ProjectAssistant));
				Assert.Equal(PromptComposerErrorCode.AgentSpecNotFound, ex.ErrorCode);
			}
			finally
			{
				TryDeleteDirectory(tempRoot);
			}
		}

		[Fact]
		public void ComposePrompt_Throws_WhenAgentSpecInvalidJson()
		{
			var tempRoot = CreateTempDirectory();
			try
			{
				var instructionFolder = Path.Combine(tempRoot, "Resources", "Instructions", "ProjectAssistant");
				Directory.CreateDirectory(instructionFolder);
				File.WriteAllText(Path.Combine(instructionFolder, "agentspec.json"), "{ invalid json }");

				var composer = new AgentPromptComposer(tempRoot);

				var ex = Assert.Throws<PromptComposerException>(() => composer.ComposePrompt(HermesInstructionType.ProjectAssistant));
				Assert.Equal(PromptComposerErrorCode.AgentSpecInvalid, ex.ErrorCode);
			}
			finally
			{
				TryDeleteDirectory(tempRoot);
			}
		}

		[Fact]
		public void ComposePrompt_Throws_WhenNoCapabilitiesDefined()
		{
			var tempRoot = CreateTempDirectory();
			try
			{
				var instructionFolder = Path.Combine(tempRoot, "Resources", "Instructions", "ProjectAssistant");
				Directory.CreateDirectory(instructionFolder);

				var spec = new AgentSpec { Capabilities = new List<AgentCapability>() };
				var json = JsonSerializer.Serialize(spec);
				File.WriteAllText(Path.Combine(instructionFolder, "agentspec.json"), json);

				var composer = new AgentPromptComposer(tempRoot);

				var ex = Assert.Throws<PromptComposerException>(() => composer.ComposePrompt(HermesInstructionType.ProjectAssistant));
				Assert.Equal(PromptComposerErrorCode.NoCapabilitiesDefined, ex.ErrorCode);
			}
			finally
			{
				TryDeleteDirectory(tempRoot);
			}
		}

		[Fact]
		public void ComposePrompt_BuildsPrompt_FromCommonAndCapabilities()
		{
			var tempRoot = CreateTempDirectory();
			try
			{
				var instructionFolder = Path.Combine(tempRoot, "Resources", "Instructions", "ProjectAssistant");
				Directory.CreateDirectory(instructionFolder);

				var commonPath = Path.Combine(instructionFolder, "ProjectAssistant_Common");
				File.WriteAllText(commonPath, "COMMON-INSTRUCTIONS");

				var capabilitiesFolder = Path.Combine(tempRoot, "Resources", "Instructions", "ProjectAssistant", "Capabilities");
				Directory.CreateDirectory(capabilitiesFolder);

				var cap1Path = Path.Combine(capabilitiesFolder, "newsletter-generation.txt");
				var cap2Path = Path.Combine(capabilitiesFolder, "parent-hierarchy-validator.txt");

				File.WriteAllText(cap1Path, "CAPABILITY-ONE");
				File.WriteAllText(cap2Path, "CAPABILITY-TWO");

				var spec = new AgentSpec
				{
					Capabilities = new List<AgentCapability>
					{
						new AgentCapability { Id = "newsletter-generation" },
						new AgentCapability { Id = "parent-hierarchy-validator" }
					}
				};
				var json = JsonSerializer.Serialize(spec);
				File.WriteAllText(Path.Combine(instructionFolder, "agentspec.json"), json);

				var composer = new AgentPromptComposer(tempRoot);

				var prompt = composer.ComposePrompt(HermesInstructionType.ProjectAssistant);

				Assert.Contains("COMMON-INSTRUCTIONS", prompt);
				Assert.Contains("CAPABILITY-ONE", prompt);
				Assert.Contains("CAPABILITY-TWO", prompt);
			}
			finally
			{
				TryDeleteDirectory(tempRoot);
			}
		}

		private static string CreateTempDirectory()
		{
			var path = Path.Combine(Path.GetTempPath(), "AgentPromptComposerTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return path;
		}

		private static void TryDeleteDirectory(string path)
		{
			try
			{
				if (Directory.Exists(path))
				{
					Directory.Delete(path, recursive: true);
				}
			}
			catch
			{
				// Best-effort cleanup only.
			}
		}
	}
}
