using System.Collections.Generic;

namespace Hermes.Orchestrator.Models
{
    /// <summary>
    /// Configuration for Azure OpenAI model selection and routing.
    /// Maps operation types to specific models for optimal performance and cost balance.
    /// </summary>
    public class ModelConfiguration
    {
        /// <summary>
        /// Gets or sets the dictionary mapping model type names (Fast, Standard, Advanced)
        /// to actual Azure OpenAI deployment names (e.g., gpt-4o, gpt-4o-mini).
        /// </summary>
        public Dictionary<string, string> Models { get; set; } = new();

        /// <summary>
        /// Gets or sets the dictionary mapping operation names to model type names.
        /// Example: "NewsletterGeneration" -> "Advanced" -> "gpt-4o"
        /// </summary>
        public Dictionary<string, string> OperationModelMap { get; set; } = new();

        /// <summary>
        /// Gets the model deployment name for a specific operation.
        /// Falls back to the Default model if the operation is not mapped.
        /// </summary>
        /// <param name="operation">The operation name (e.g., "NewsletterGeneration", "ToolRouting")</param>
        /// <returns>The Azure OpenAI deployment name (e.g., "gpt-4o", "gpt-4o-mini")</returns>
        public string GetModelForOperation(string operation)
        {
            // First, try to get the model type for this operation (e.g., "Advanced")
            if (OperationModelMap.TryGetValue(operation, out var modelType))
            {
                // Then, resolve the model type to an actual model name (e.g., "gpt-4o")
                if (Models.TryGetValue(modelType, out var modelName))
                {
                    return modelName;
                }
            }

            // Fallback to default model if operation or model type not found
            return Models.TryGetValue("Default", out var defaultModel)
                ? defaultModel
                : "gpt-5-mini"; // Hardcoded ultimate fallback
        }
    }
}
