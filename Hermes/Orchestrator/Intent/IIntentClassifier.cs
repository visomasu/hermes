namespace Hermes.Orchestrator.Intent
{
    /// <summary>
    /// Service for classifying user intent from natural language queries.
    /// Used to optimize model selection and prompt templates.
    /// </summary>
    public interface IIntentClassifier
    {
        /// <summary>
        /// Classifies user intent from a natural language query.
        /// </summary>
        /// <param name="userQuery">The user's natural language query</param>
        /// <returns>Intent name (e.g., "NewsletterGeneration", "SlaCheck") or "Default"</returns>
        string ClassifyIntent(string userQuery);

        /// <summary>
        /// Gets confidence score for a classified intent (0.0 to 1.0).
        /// </summary>
        /// <param name="userQuery">The user's natural language query</param>
        /// <param name="intent">The intent to score</param>
        /// <returns>Confidence score between 0.0 and 1.0</returns>
        double GetConfidence(string userQuery, string intent);
    }
}
