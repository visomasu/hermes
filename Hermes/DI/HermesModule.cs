using Autofac;
using Hermes.Orchestrator;
using Hermes.Tools;
using Hermes.Tools.AzureDevOps;

namespace Hermes.DI
{
    /// <summary>
    /// Parent Autofac module for Hermes application-wide dependency registrations.
    /// </summary>
    public class HermesModule : Module
    {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public HermesModule(IConfiguration configuration, IHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // Register configuration and environment
            builder.RegisterInstance(_configuration).As<IConfiguration>().SingleInstance();
            builder.RegisterInstance(_environment).As<IHostEnvironment>().SingleInstance();

            // Register StorageModule for storage-related dependencies, injecting config and env
            builder.RegisterModule(new StorageModule(_configuration, _environment));
            // Register IntegrationsModule for integration-related dependencies
            builder.RegisterModule(new IntegrationsModule(_configuration, _environment));
            // Register AgentToolsModule for tools-related dependencies
            builder.RegisterModule(new AgentToolsModule());

            // Register HermesOrchestrator and pass only AzureDevOpsTool
            builder.Register(ctx =>
            {
                var config = ctx.Resolve<IConfiguration>();
                var env = ctx.Resolve<IHostEnvironment>();
                string endpoint;
                string apiKey;

                if (env.IsDevelopment())
                {
                    endpoint = "https://visomasu-project-hermes.openai.azure.com/";
                    apiKey = "dev-api-key";
                }
                else
                {
                    endpoint = config["OpenAI:Endpoint"] ?? string.Empty;
                    apiKey = config["OpenAI:ApiKey"] ?? string.Empty;
                }

                var azureDevOpsTool = ctx.Resolve<AzureDevOpsTool>();

                return new HermesOrchestrator(endpoint, apiKey, new[] { azureDevOpsTool });
            }).As<IAgentOrchestrator>().SingleInstance();
        }
    }
}
