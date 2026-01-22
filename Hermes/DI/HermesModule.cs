using Autofac;
using Hermes.Orchestrator;
using Hermes.Orchestrator.Context;
using Hermes.Orchestrator.Models;
using Hermes.Orchestrator.PhraseGen;
using Hermes.Orchestrator.Prompts;
using Hermes.Storage.Repositories.ConversationHistory;
using Hermes.Storage.Repositories.HermesInstructions;
using Hermes.Services.Notifications;
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

            // Register TeamsModule for Teams channel-related dependencies
            builder.RegisterModule(new TeamsModule(_configuration, _environment));

            // Register SchedulingModule for scheduling infrastructure
            builder.RegisterModule(new SchedulingModule(_configuration));

            builder.RegisterType<AgentPromptComposer>()
                .As<IAgentPromptComposer>()
                .SingleInstance();

            builder.RegisterType<WaitingPhraseGenerator>()
                .As<IWaitingPhraseGenerator>()
                .SingleInstance();

            // Register ProactiveMessenger for sending proactive notifications
            builder.RegisterType<ProactiveMessenger>()
                .As<IProactiveMessenger>()
                .SingleInstance();

            // Register NotificationGate for notification throttling and deduplication
            builder.RegisterType<NotificationGate>()
                .As<INotificationGate>()
                .SingleInstance();

            // Register conversation context configuration
            builder.Register(ctx =>
            {
                var config = ctx.Resolve<IConfiguration>();
                var contextConfig = new ConversationContextConfig();
                config.GetSection("ConversationContext").Bind(contextConfig);
                return contextConfig;
            })
            .AsSelf()
            .SingleInstance();

            // Register semantic conversation context selector
            builder.RegisterType<SemanticConversationContextSelector>()
                .As<IConversationContextSelector>()
                .SingleInstance();

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
                var instructionsRepository = ctx.Resolve<IHermesInstructionsRepository>();
                var conversationHistoryRepository = ctx.Resolve<IConversationHistoryRepository>();
                var phraseGenerator = ctx.Resolve<IWaitingPhraseGenerator>();
                var contextSelector = ctx.Resolve<IConversationContextSelector>();

                return new HermesOrchestrator(
                    endpoint,
                    apiKey,
                    new[] { azureDevOpsTool },
                    instructionsRepository,
                    conversationHistoryRepository,
                    ctx.Resolve<IAgentPromptComposer>(),
                    phraseGenerator,
                    contextSelector);
            }).As<IAgentOrchestrator>().SingleInstance();
        }
    }
}
