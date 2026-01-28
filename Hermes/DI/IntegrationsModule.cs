using Autofac;
using Hermes.Integrations.AzureOpenAI;
using Hermes.Integrations.MicrosoftGraph;
using Integrations.AzureDevOps;
using Microsoft.Extensions.Logging;

namespace Hermes.DI
{
	/// <summary>
	/// Autofac module for registering integration services.
	/// </summary>
	public class IntegrationsModule : Module
	{
		private readonly IConfiguration _configuration;
		private readonly IHostEnvironment _environment;

		/// <summary>
		/// Initializes a new instance of the <see cref="IntegrationsModule"/> class.
		/// </summary>
		/// <param name="configuration">Application configuration.</param>
		/// <param name="environment">Host environment.</param>
		public IntegrationsModule(IConfiguration configuration, IHostEnvironment environment)
		{
			_configuration = configuration;
			_environment = environment;
		}

		/// <inheritdoc/>
		protected override void Load(ContainerBuilder builder)
		{
            string organization = _configuration["AzureDevOps:Organization"] ?? string.Empty;
            string project = _configuration["AzureDevOps:Project"] ?? string.Empty;

			builder.Register(c =>
			{
				// Uses DefaultAzureCredential (az login for local dev, Managed Identity for production)
				return new AzureDevOpsWorkItemClient(organization, project);
			})
			.As<IAzureDevOpsWorkItemClient>()
			.SingleInstance();

			builder.Register(c =>
			{
				return new AzureDevOpsGitClient(organization, project, pat);
			})
			.As<IAzureDevOpsGitClient>()
			.SingleInstance();

			// Register Azure OpenAI Embedding Client
			builder.Register(c =>
			{
				var logger = c.Resolve<ILogger<AzureOpenAIEmbeddingClient>>();
				var endpoint = _configuration["OpenAI:Endpoint"] ?? throw new InvalidOperationException("OpenAI:Endpoint is not configured");
				var embeddingModel = _configuration["ConversationContext:EmbeddingModel"] ?? "text-embedding-3-small";

				return new AzureOpenAIEmbeddingClient(endpoint, embeddingModel, logger);
			})
			.As<IAzureOpenAIEmbeddingClient>()
			.SingleInstance();

			// Register Microsoft Graph Client
			// Uses DefaultAzureCredential (az login for local dev, Managed Identity for production)
			builder.Register(c =>
			{
				var logger = c.Resolve<ILogger<MicrosoftGraphClient>>();
				return new MicrosoftGraphClient(logger);
			})
			.As<IMicrosoftGraphClient>()
			.SingleInstance();
		}
	}
}
