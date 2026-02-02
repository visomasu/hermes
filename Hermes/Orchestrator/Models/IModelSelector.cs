using OpenAI.Chat;

namespace Hermes.Orchestrator.Models
{
    /// <summary>
    /// Service for selecting Azure OpenAI models based on operation type.
    /// Enables per-operation model routing to optimize performance and cost.
    /// </summary>
    public interface IModelSelector
    {
        /// <summary>
        /// Gets the Azure OpenAI deployment name for the specified operation.
        /// </summary>
        /// <param name="operation">The operation name (e.g., "NewsletterGeneration", "ToolRouting", "Default")</param>
        /// <returns>The model deployment name (e.g., "gpt-4o", "gpt-4o-mini", "gpt-5-mini")</returns>
        string GetModelForOperation(string operation);

        /// <summary>
        /// Gets a ChatClient configured for the specified operation's model.
        /// Clients are cached to avoid recreating Azure OpenAI connections.
        /// </summary>
        /// <param name="operation">The operation name (e.g., "NewsletterGeneration", "ToolRouting", "Default")</param>
        /// <returns>A ChatClient instance configured for the appropriate model</returns>
        ChatClient GetChatClientForOperation(string operation);
    }
}
