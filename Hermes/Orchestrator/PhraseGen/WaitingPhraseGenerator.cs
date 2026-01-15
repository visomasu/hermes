namespace Hermes.Orchestrator.PhraseGen
{
	/// <summary>
	/// Generates creative, Claude-style waiting phrases by randomly combining
	/// adjectives, verbs, and nouns.
	/// </summary>
	public class WaitingPhraseGenerator : IWaitingPhraseGenerator
	{
		private readonly Random _random = new Random();

		private static readonly string[] Adjectives = new[]
		{
			"splendid", "brilliant", "curious", "magnificent", "delightful",
			"radiant", "vibrant", "luminous", "graceful", "elegant",
			"sparkling", "dazzling", "enchanting", "marvelous", "wonderful",
			"glorious", "stellar", "supreme", "exquisite", "pristine",
			"serene", "tranquil", "harmonious", "melodic", "rhythmic"
		};

		private static readonly string[] Verbs = new[]
		{
			"soaring", "dancing", "wandering", "flowing", "gliding",
			"spinning", "twirling", "weaving", "drifting", "floating",
			"cascading", "spiraling", "blooming", "shimmering", "gleaming",
			"racing", "leaping", "bounding", "sailing", "diving",
			"exploring", "discovering", "pondering", "contemplating", "analyzing"
		};

		private static readonly string[] Nouns = new[]
		{
			"sketch", "thought", "mind", "idea", "concept",
			"vision", "dream", "notion", "spark", "flash",
			"insight", "revelation", "discovery", "pattern", "design",
			"blueprint", "schema", "framework", "structure", "model",
			"narrative", "story", "tale", "journey", "quest"
		};

		/// <inheritdoc />
		public string GeneratePhrase()
		{
			var adjective = Adjectives[_random.Next(Adjectives.Length)];
			var verb = Verbs[_random.Next(Verbs.Length)];
			var noun = Nouns[_random.Next(Nouns.Length)];

			return $"{adjective}-{verb}-{noun}";
		}
	}
}
