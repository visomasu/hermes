using Hermes.Tools;
using Xunit;

namespace Hermes.Tests.Tools
{
	public class CapabilityMatcherTests
	{
		private static readonly IReadOnlyDictionary<string, string[]> TestCapabilityAliases = new Dictionary<string, string[]>
		{
			{ "GetWorkItemTree", new[] { "GetTree", "WorkItemTree", "FetchTree" } },
			{ "RegisterSlaNotifications", new[] { "RegisterSLA", "RegisterForSLA", "Register" } },
			{ "CheckSlaViolations", new[] { "CheckViolations", "CheckSLA", "SLACheck" } }
		};

		#region ExactMatch Tests

		[Fact]
		public void TryResolve_ExactMatchCanonicalName_ReturnsTrue()
		{
			// Arrange
			var operation = "GetWorkItemTree";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.True(result);
			Assert.Equal("GetWorkItemTree", canonicalName);
			Assert.Equal(MatchStrategy.ExactMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_ExactMatchCaseInsensitive_ReturnsTrue()
		{
			// Arrange
			var operation = "getworkitemtree";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.True(result);
			Assert.Equal("GetWorkItemTree", canonicalName);
			Assert.Equal(MatchStrategy.ExactMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_ExactMatchMixedCase_ReturnsTrue()
		{
			// Arrange
			var operation = "GetWorkItemTree".ToUpper();

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.True(result);
			Assert.Equal("GetWorkItemTree", canonicalName);
			Assert.Equal(MatchStrategy.ExactMatch, matchStrategy);
		}

		#endregion

		#region AliasMatch Tests

		[Fact]
		public void TryResolve_AliasMatch_ReturnsTrue()
		{
			// Arrange
			var operation = "GetTree";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.True(result);
			Assert.Equal("GetWorkItemTree", canonicalName);
			Assert.Equal(MatchStrategy.AliasMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_AliasMatchCaseInsensitive_ReturnsTrue()
		{
			// Arrange
			var operation = "registersla";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.True(result);
			Assert.Equal("RegisterSlaNotifications", canonicalName);
			Assert.Equal(MatchStrategy.AliasMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_MultipleAliases_ReturnsCorrectCanonical()
		{
			// Arrange - test different aliases for the same capability
			var aliases = new[] { "RegisterSLA", "RegisterForSLA", "Register" };

			foreach (var alias in aliases)
			{
				// Act
				var result = CapabilityMatcher.TryResolve(alias, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

				// Assert
				Assert.True(result);
				Assert.Equal("RegisterSlaNotifications", canonicalName);
				Assert.Equal(MatchStrategy.AliasMatch, matchStrategy);
			}
		}

		#endregion

		#region PatternMatch Tests

		[Fact]
		public void TryResolve_PatternMatchRemoveGetPrefix_ReturnsTrue()
		{
			// Arrange - Test with a capability that has "Get" prefix in an alias dict without that alias
			var aliases = new Dictionary<string, string[]>
			{
				{ "GetUserProfile", new[] { "Profile", "User" } }
			};
			var operation = "UserProfile"; // Should match after removing "Get" prefix

			// Act
			var result = CapabilityMatcher.TryResolve(operation, aliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.True(result);
			Assert.Equal("GetUserProfile", canonicalName);
			Assert.Equal(MatchStrategy.PatternMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_PatternMatchWithCapabilitySuffix_ReturnsTrue()
		{
			// Arrange
			var aliases = new Dictionary<string, string[]>
			{
				{ "RegisterSlaNotificationsCapability", new[] { "Register" } }
			};
			var operation = "RegisterSlaNotifications";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, aliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.True(result);
			Assert.Equal("RegisterSlaNotificationsCapability", canonicalName);
			Assert.Equal(MatchStrategy.PatternMatch, matchStrategy);
		}

		#endregion

		#region PartialMatch Tests

		[Fact]
		public void TryResolve_PartialMatchUnambiguous_ReturnsTrue()
		{
			// Arrange
			var operation = "Violations";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.True(result);
			Assert.Equal("CheckSlaViolations", canonicalName);
			Assert.Equal(MatchStrategy.PartialMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_PartialMatchAmbiguous_ReturnsFalse()
		{
			// Arrange - "SLA" matches both "RegisterSlaNotifications" and "CheckSlaViolations"
			var operation = "SLA";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.False(result);
			Assert.Equal(string.Empty, canonicalName);
			Assert.Equal(MatchStrategy.NoMatch, matchStrategy);
		}

		#endregion

		#region NoMatch Tests

		[Fact]
		public void TryResolve_NoMatch_ReturnsFalse()
		{
			// Arrange
			var operation = "NonExistentOperation";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.False(result);
			Assert.Equal(string.Empty, canonicalName);
			Assert.Equal(MatchStrategy.NoMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_EmptyOperation_ReturnsFalse()
		{
			// Arrange
			var operation = "";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.False(result);
			Assert.Equal(string.Empty, canonicalName);
			Assert.Equal(MatchStrategy.NoMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_WhitespaceOperation_ReturnsFalse()
		{
			// Arrange
			var operation = "   ";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.False(result);
			Assert.Equal(string.Empty, canonicalName);
			Assert.Equal(MatchStrategy.NoMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_NullOperation_ReturnsFalse()
		{
			// Arrange
			string? operation = null;

			// Act
			var result = CapabilityMatcher.TryResolve(operation!, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.False(result);
			Assert.Equal(string.Empty, canonicalName);
			Assert.Equal(MatchStrategy.NoMatch, matchStrategy);
		}

		#endregion

		#region Overload Tests

		[Fact]
		public void TryResolve_OverloadWithoutMatchStrategy_ReturnsTrue()
		{
			// Arrange
			var operation = "GetTree";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName);

			// Assert
			Assert.True(result);
			Assert.Equal("GetWorkItemTree", canonicalName);
		}

		#endregion

		#region FormatNotSupportedError Tests

		[Fact]
		public void FormatNotSupportedError_ReturnsFormattedMessage()
		{
			// Arrange
			var operation = "InvalidOperation";
			var toolName = "TestTool";
			var availableCapabilities = new[] { "GetWorkItemTree", "RegisterSlaNotifications" };

			// Act
			var result = CapabilityMatcher.FormatNotSupportedError(operation, toolName, availableCapabilities);

			// Assert
			Assert.Contains("InvalidOperation", result);
			Assert.Contains("TestTool", result);
			Assert.Contains("GetWorkItemTree", result);
			Assert.Contains("RegisterSlaNotifications", result);
		}

		#endregion

		#region Edge Cases

		[Fact]
		public void TryResolve_OperationWithLeadingTrailingSpaces_ReturnsTrue()
		{
			// Arrange
			var operation = "  GetWorkItemTree  ";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, TestCapabilityAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.True(result);
			Assert.Equal("GetWorkItemTree", canonicalName);
			Assert.Equal(MatchStrategy.ExactMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_EmptyAliasesDictionary_ReturnsFalse()
		{
			// Arrange
			var operation = "GetWorkItemTree";
			var emptyAliases = new Dictionary<string, string[]>();

			// Act
			var result = CapabilityMatcher.TryResolve(operation, emptyAliases, out var canonicalName, out var matchStrategy);

			// Assert
			Assert.False(result);
			Assert.Equal(string.Empty, canonicalName);
			Assert.Equal(MatchStrategy.NoMatch, matchStrategy);
		}

		[Fact]
		public void TryResolve_PrioritizesExactOverAlias_ReturnsExactMatch()
		{
			// Arrange - "GetTree" is both a canonical name AND an alias for "GetWorkItemTree"
			var aliases = new Dictionary<string, string[]>
			{
				{ "GetTree", new[] { "Tree" } },
				{ "GetWorkItemTree", new[] { "GetTree", "WorkItemTree" } }
			};
			var operation = "GetTree";

			// Act
			var result = CapabilityMatcher.TryResolve(operation, aliases, out var canonicalName, out var matchStrategy);

			// Assert - should match "GetTree" as canonical (ExactMatch) not as alias
			Assert.True(result);
			Assert.Equal("GetTree", canonicalName);
			Assert.Equal(MatchStrategy.ExactMatch, matchStrategy);
		}

		#endregion
	}
}
