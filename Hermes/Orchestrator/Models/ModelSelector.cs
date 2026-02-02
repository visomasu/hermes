using System;
using System.Collections.Generic;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

namespace Hermes.Orchestrator.Models
{
    /// <summary>
    /// Service for selecting and caching Azure OpenAI models based on operation type.
    /// </summary>
    public class ModelSelector : IModelSelector
    {
        private readonly string _endpoint;
        private readonly ModelConfiguration _config;
        private readonly Dictionary<string, ChatClient> _clientCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelSelector"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration containing OpenAI settings</param>
        public ModelSelector(IConfiguration configuration)
        {
            _endpoint = configuration["OpenAI:Endpoint"]
                ?? throw new InvalidOperationException("OpenAI:Endpoint is not configured");

            // Load model configuration from appsettings.json
            _config = configuration.GetSection("OpenAI").Get<ModelConfiguration>()
                ?? throw new InvalidOperationException("OpenAI configuration section is missing or invalid");

            _clientCache = new Dictionary<string, ChatClient>();
        }

        /// <summary>
        /// Gets the Azure OpenAI deployment name for the specified operation.
        /// </summary>
        public string GetModelForOperation(string operation)
        {
            return _config.GetModelForOperation(operation);
        }

        /// <summary>
        /// Gets a ChatClient configured for the specified operation's model.
        /// Clients are cached to avoid recreating Azure OpenAI connections.
        /// </summary>
        public ChatClient GetChatClientForOperation(string operation)
        {
            var modelName = GetModelForOperation(operation);

            // Return cached client if available
            if (_clientCache.TryGetValue(modelName, out var cachedClient))
            {
                return cachedClient;
            }

            // Create new client and cache it
            var azureClient = new AzureOpenAIClient(
                new Uri(_endpoint),
                new AzureCliCredential());

            var chatClient = azureClient.GetChatClient(modelName);
            _clientCache[modelName] = chatClient;

            return chatClient;
        }
    }
}
