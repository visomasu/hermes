using Hermes.Orchestrator.PhraseGen;
using Xunit;

namespace Hermes.Tests.Orchestrator.PhraseGen
{
	public class WaitingPhraseGeneratorTests
	{
		[Fact]
		public void GeneratePhrase_ReturnsNonEmptyString()
		{
			// Arrange
			var generator = new WaitingPhraseGenerator();

			// Act
			var phrase = generator.GeneratePhrase();

			// Assert
			Assert.NotNull(phrase);
			Assert.NotEmpty(phrase);
		}

		[Fact]
		public void GeneratePhrase_ReturnsHyphenatedFormat()
		{
			// Arrange
			var generator = new WaitingPhraseGenerator();

			// Act
			var phrase = generator.GeneratePhrase();

			// Assert - should be "adjective-verb-noun"
			var parts = phrase.Split('-');
			Assert.Equal(3, parts.Length);
			Assert.All(parts, part => Assert.NotEmpty(part));
		}

		[Fact]
		public void GeneratePhrase_GeneratesVariedPhrases()
		{
			// Arrange
			var generator = new WaitingPhraseGenerator();
			var phrases = new HashSet<string>();

			// Act - generate 100 phrases
			for (int i = 0; i < 100; i++)
			{
				phrases.Add(generator.GeneratePhrase());
			}

			// Assert - should have at least 50 unique phrases (randomness check)
			Assert.True(phrases.Count >= 50, $"Expected at least 50 unique phrases, but got {phrases.Count}");
		}

		[Fact]
		public void GeneratePhrase_IsThreadSafe()
		{
			// Arrange
			var generator = new WaitingPhraseGenerator();
			var tasks = new List<Task<string>>();

			// Act - generate phrases from multiple threads
			for (int i = 0; i < 10; i++)
			{
				tasks.Add(Task.Run(() => generator.GeneratePhrase()));
			}

			// Assert - should complete without exceptions
			var results = Task.WhenAll(tasks).GetAwaiter().GetResult();
			Assert.Equal(10, results.Length);
			Assert.All(results, phrase => Assert.NotEmpty(phrase));
		}
	}
}
