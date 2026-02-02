using System.Text.RegularExpressions;

namespace Hermes.Orchestrator.Intent
{
    /// <summary>
    /// Classifies user intent from natural language queries using keyword matching.
    /// Used to optimize model selection and prompt templates for different operation types.
    /// </summary>
    public class IntentClassifier : IIntentClassifier
    {
        // Intent patterns with keywords and phrases
        private static readonly Dictionary<string, IntentPattern> IntentPatterns = new()
        {
            {
                "NewsletterGeneration",
                new IntentPattern
                {
                    Keywords = new[] { "newsletter", "status update", "summary", "report", "executive update", "progress report" },
                    Phrases = new[] { "generate.*newsletter", "create.*summary", "status.*update", "executive.*report" }
                }
            },
            {
                "HierarchyValidation",
                new IntentPattern
                {
                    Keywords = new[] { "validate", "hierarchy", "parent", "broken links", "tree structure" },
                    Phrases = new[] { "validate.*hierarchy", "check.*parent", "broken.*links" }
                }
            },
            {
                "SlaRegister",
                new IntentPattern
                {
                    Keywords = new[] { "register for sla", "sign up for sla", "enable sla", "sla registration" },
                    Phrases = new[] { "register.*sla", "sign.*up.*sla", "enable.*sla.*notif", "start.*sla", "unregister.*sla", "remove.*sla.*notif" }
                }
            },
            {
                "SlaCheck",
                new IntentPattern
                {
                    Keywords = new[] { "violations", "overdue", "stale", "needs update" },
                    Phrases = new[] { "sla.*violation", "check.*sla", "my.*sla", "overdue.*items", "stale.*work" }
                }
            },
            {
                "UserActivity",
                new IntentPattern
                {
                    Keywords = new[] { "activity", "pull requests", "prs", "worked on", "user activity" },
                    Phrases = new[] { "what.*worked.*on", "pull.*requests.*by", "user.*activity" }
                }
            },
            {
                "AreaPathQuery",
                new IntentPattern
                {
                    Keywords = new[] { "area path", "area paths", "in area", "from area" },
                    Phrases = new[] { "show.*area.*path", "list.*area", "query.*area", "get.*items.*from.*area", "features.*in.*area", "work.*items.*in.*area" }
                }
            },
            {
                "Help",
                new IntentPattern
                {
                    Keywords = new[] { "help", "hello", "hi", "capabilities", "what can you do", "commands", "how do i" },
                    Phrases = new[] { "what.*can.*do", "list.*capabilities", "show.*commands", "how.*use" }
                }
            }
        };

        /// <summary>
        /// Classifies user intent from a natural language query.
        /// Uses keyword matching and regex patterns for fast, deterministic classification.
        /// </summary>
        /// <param name="userQuery">The user's natural language query</param>
        /// <returns>Intent name (e.g., "NewsletterGeneration") or "Default" if no match</returns>
        public string ClassifyIntent(string userQuery)
        {
            if (string.IsNullOrWhiteSpace(userQuery))
                return "Default";

            var query = userQuery.ToLowerInvariant();

            foreach (var (intent, pattern) in IntentPatterns)
            {
                // Check keyword matches
                if (pattern.Keywords.Any(kw => query.Contains(kw)))
                    return intent;

                // Check phrase patterns (regex)
                if (pattern.Phrases.Any(phrase => Regex.IsMatch(query, phrase, RegexOptions.IgnoreCase)))
                    return intent;
            }

            return "Default";
        }

        /// <summary>
        /// Gets confidence score for a classified intent (0.0 to 1.0).
        /// For keyword matching, returns 1.0 if matched, 0.0 otherwise.
        /// Future: Can be upgraded to return similarity scores for embedding-based classification.
        /// </summary>
        public double GetConfidence(string userQuery, string intent)
        {
            return ClassifyIntent(userQuery) == intent ? 1.0 : 0.0;
        }
    }
}
