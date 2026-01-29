namespace Hermes.Tools
{
	/// <summary>
	/// Represents the strategy used to match a capability operation name.
	/// </summary>
	public enum MatchStrategy
	{
		/// <summary>
		/// No match was found.
		/// </summary>
		NoMatch = 0,

		/// <summary>
		/// Exact case-insensitive match with the canonical name.
		/// </summary>
		ExactMatch = 1,

		/// <summary>
		/// Case-insensitive match with a registered alias.
		/// </summary>
		AliasMatch = 2,

		/// <summary>
		/// Match after removing common prefixes/suffixes (Get, Capability).
		/// </summary>
		PatternMatch = 3,

		/// <summary>
		/// Partial substring match (only when unambiguous).
		/// </summary>
		PartialMatch = 4
	}
}
