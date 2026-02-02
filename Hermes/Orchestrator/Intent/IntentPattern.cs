namespace Hermes.Orchestrator.Intent
{
    /// <summary>
    /// Pattern definition for intent classification.
    /// </summary>
    internal class IntentPattern
    {
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string[] Phrases { get; set; } = Array.Empty<string>();
    }
}
