using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;

namespace Hermes.Orchestrator
{
    /// <summary>
    /// Hermes Orchestrator.
    /// Responsible for orchestrating the execution of tasks in response to the user input over chat modality.
    /// </summary>
    public class Hermes
    {
        private readonly AIAgent _hermes;

        /// <summary>
        /// Initializes a new instance of the Hermes class using the specified endpoint and API key.
        /// </summary>
        /// <param name="endpoint">The URI endpoint of the Azure OpenAI resource to connect to. Must be a valid, accessible endpoint.</param>
        /// <param name="apiKey">The API key used to authenticate requests to the Azure OpenAI service. Cannot be null or empty.</param>
        public Hermes(string endpoint, string apiKey)
        {
             this._hermes = new AzureOpenAIClient(
                new Uri("https://<myresource>.openai.azure.com"),
                new AzureCliCredential())
                    .GetChatClient("gpt-5o-mini")
                    .CreateAIAgent(instructions:"You are a agent that helps the user understand the state of a project utilizing data from Azure Devops.");
        }
    }
}
