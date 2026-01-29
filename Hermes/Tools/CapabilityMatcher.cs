namespace Hermes.Tools
{
	/// <summary>
	/// Utility for flexible capability name matching with multiple strategies.
	/// Supports exact matches, aliases, pattern matching, and partial matches.
	/// </summary>
	public static class CapabilityMatcher
	{
		/// <summary>
		/// Attempts to resolve an operation name to a canonical capability name using multiple matching strategies.
		/// </summary>
		/// <param name="operation">The operation name to resolve (case-insensitive).</param>
		/// <param name="capabilityAliases">Dictionary mapping canonical capability names to their aliases.</param>
		/// <param name="canonicalName">The resolved canonical name if found.</param>
		/// <param name="matchStrategy">The strategy that successfully matched the operation.</param>
		/// <returns>True if the operation was successfully resolved to a canonical name.</returns>
		/// <remarks>
		/// Matching strategies are applied in order of specificity:
		/// 1. ExactMatch: Case-insensitive exact match with canonical name
		/// 2. AliasMatch: Case-insensitive match with any registered alias
		/// 3. PatternMatch: Match after removing common prefixes (Get) and suffixes (Capability)
		/// 4. PartialMatch: Substring match (only if unambiguous - exactly one match)
		/// </remarks>
		public static bool TryResolve(
			string operation,
			IReadOnlyDictionary<string, string[]> capabilityAliases,
			out string canonicalName,
			out MatchStrategy matchStrategy)
		{
			if (string.IsNullOrWhiteSpace(operation))
			{
				canonicalName = string.Empty;
				matchStrategy = MatchStrategy.NoMatch;
				return false;
			}

			var normalized = operation.Trim();

			// Strategy 1: Exact match (case-insensitive) with canonical name
			foreach (var (canonical, aliases) in capabilityAliases)
			{
				if (canonical.Equals(normalized, StringComparison.OrdinalIgnoreCase))
				{
					canonicalName = canonical;
					matchStrategy = MatchStrategy.ExactMatch;
					return true;
				}
			}

			// Strategy 2: Alias match (case-insensitive)
			foreach (var (canonical, aliases) in capabilityAliases)
			{
				if (aliases.Any(alias => alias.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
				{
					canonicalName = canonical;
					matchStrategy = MatchStrategy.AliasMatch;
					return true;
				}
			}

			// Strategy 3: Pattern match - remove common prefixes/suffixes
			var stripped = _StripCommonAffixes(normalized);
			foreach (var (canonical, _) in capabilityAliases)
			{
				var canonicalStripped = _StripCommonAffixes(canonical);
				if (canonicalStripped.Equals(stripped, StringComparison.OrdinalIgnoreCase))
				{
					canonicalName = canonical;
					matchStrategy = MatchStrategy.PatternMatch;
					return true;
				}
			}

			// Strategy 4: Partial match - only if unambiguous (exactly one match)
			var partialMatches = capabilityAliases.Keys
				.Where(k => k.Contains(normalized, StringComparison.OrdinalIgnoreCase))
				.ToList();

			if (partialMatches.Count == 1)
			{
				canonicalName = partialMatches[0];
				matchStrategy = MatchStrategy.PartialMatch;
				return true;
			}

			// No match found
			canonicalName = string.Empty;
			matchStrategy = MatchStrategy.NoMatch;
			return false;
		}

		/// <summary>
		/// Attempts to resolve an operation name to a canonical capability name.
		/// Overload that doesn't return the match strategy.
		/// </summary>
		/// <param name="operation">The operation name to resolve.</param>
		/// <param name="capabilityAliases">Dictionary mapping canonical capability names to their aliases.</param>
		/// <param name="canonicalName">The resolved canonical name if found.</param>
		/// <returns>True if the operation was successfully resolved.</returns>
		public static bool TryResolve(
			string operation,
			IReadOnlyDictionary<string, string[]> capabilityAliases,
			out string canonicalName)
		{
			return TryResolve(operation, capabilityAliases, out canonicalName, out _);
		}

		/// <summary>
		/// Strips common prefixes and suffixes from capability names for pattern matching.
		/// Removes: "Get", "Capability" (case-insensitive).
		/// </summary>
		/// <param name="name">The name to strip.</param>
		/// <returns>The name with common affixes removed.</returns>
		private static string _StripCommonAffixes(string name)
		{
			var result = name;

			// Remove "Get" prefix
			if (result.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
			{
				result = result.Substring(3);
			}

			// Remove "Capability" suffix
			if (result.EndsWith("Capability", StringComparison.OrdinalIgnoreCase))
			{
				result = result.Substring(0, result.Length - 10);
			}

			return result.Trim();
		}

		/// <summary>
		/// Formats a helpful error message when an operation cannot be resolved.
		/// </summary>
		/// <param name="operation">The operation that failed to resolve.</param>
		/// <param name="toolName">The name of the tool.</param>
		/// <param name="availableCapabilities">List of available canonical capability names.</param>
		/// <returns>A formatted error message with suggestions.</returns>
		public static string FormatNotSupportedError(
			string operation,
			string toolName,
			IEnumerable<string> availableCapabilities)
		{
			var capabilitiesList = string.Join(", ", availableCapabilities);
			return $"Operation '{operation}' is not supported by {toolName}. " +
			       $"Available capabilities: {capabilitiesList}";
		}
	}
}
