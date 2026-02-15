using Autofac;
using Hermes.Storage.Core;
using Hermes.Storage.Core.CosmosDB;
using Hermes.Storage.Core.InMemory;
using Hermes.Storage.Core.File;
using Hermes.Storage.Repositories;
using Hermes.Storage.Repositories.Sample;
using Hermes.Storage.Repositories.HermesInstructions;
using Hermes.Storage.Repositories.ConversationHistory;
using Hermes.Storage.Repositories.ConversationReference;
using Hermes.Storage.Repositories.UserConfiguration;
using Hermes.Storage.Repositories.UserNotificationState;
using Hermes.Storage.Repositories.TeamConfiguration;
using Hermes.Storage.Core.Models;

namespace Hermes.DI
{
    /// <summary>
    /// Autofac module for storage-related dependency registrations.
    /// </summary>
    public class StorageModule : Module
    {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public StorageModule(IConfiguration configuration, IHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        protected override void Load(ContainerBuilder builder)
        {
            string connectionString;
            string databaseId;
            string containerId;

            if (_environment.IsDevelopment())
            {
                // Local CosmosDB emulator connection string
                connectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=\"C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==\";";
                databaseId = "HermesDb";
                containerId = "Hermes";
            }
            else
            {
                // Use values from configuration for production
                connectionString = _configuration["CosmosDb:ConnectionString"] ?? "";
                databaseId = _configuration["CosmosDb:DatabaseId"] ?? "";
                containerId = _configuration["CosmosDb:ContainerId"] ?? "";
            }

            // Register BitFasterStorageClient as L1 cache for all Document types
            builder.RegisterGeneric(typeof(BitFasterStorageClient<>))
                .Named("l1", typeof(IStorageClient<,>))
                .WithParameter("capacity",1000)
                .SingleInstance();

            // Register CosmosDbStorageClient as L2 for all Document types
            builder.RegisterGeneric(typeof(CosmosDbStorageClient<>))
                .Named("l2", typeof(IStorageClient<,>))
                .WithParameter("connectionString", connectionString)
                .WithParameter("databaseId", databaseId)
                .WithParameter("containerId", containerId)
                .SingleInstance();

            // Register FileStorageClient as a named file-backed storage client for FileDocument
            builder.RegisterType<FileStorageClient>()
                .Named<IStorageClient<FileDocument, string>>("file")
                .WithParameter(
                    "rootPath",
                    Path.Combine(AppContext.BaseDirectory, "Resources"))
                .SingleInstance();

            // Register HierarchicalStorageClient as the default IStorageClient<T, string>
            builder.RegisterGeneric(typeof(HierarchicalStorageClient<>))
                .As(typeof(IStorageClient<,>))
                .WithParameter(
                    (pi, ctx) => pi.Name == "l1",
                    (pi, ctx) => ctx.ResolveNamed("l1", typeof(IStorageClient<,>).MakeGenericType(pi.ParameterType.GenericTypeArguments[0], typeof(string)))
                )
                .WithParameter(
                    (pi, ctx) => pi.Name == "l2",
                    (pi, ctx) => ctx.ResolveNamed("l2", typeof(IStorageClient<,>).MakeGenericType(pi.ParameterType.GenericTypeArguments[0], typeof(string)))
                )
                .SingleInstance();

            // Register SampleRepository as IRepository<SampleRepositoryModel>
            builder.RegisterType<SampleRepository>()
                .As<IRepository<SampleRepositoryModel>>()
                .SingleInstance();

            // Explicitly wire HermesInstructionsRepository so its second constructor parameter
            // (fileStorageClient) is backed by the FileStorageClient<HermesInstructions, string>.
            builder.RegisterType<HermesInstructionsRepository>()
                .As<IHermesInstructionsRepository>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(IStorageClient<FileDocument, string>)
                                  && pi.Name == "fileStorageClient",
                    (pi, ctx) => ctx.ResolveNamed(
                        "file",
                        typeof(IStorageClient<FileDocument, string>)))
                .SingleInstance();

            // Register ConversationHistoryRepository as IConversationHistoryRepository
            builder.RegisterType<ConversationHistoryRepository>()
                .As<IConversationHistoryRepository>()
                .SingleInstance();

            // Register ConversationReferenceRepository as IConversationReferenceRepository
            builder.RegisterType<ConversationReferenceRepository>()
                .As<IConversationReferenceRepository>()
                .SingleInstance();

            // Register UserConfigurationRepository as IUserConfigurationRepository
            builder.RegisterType<UserConfigurationRepository>()
                .As<IUserConfigurationRepository>()
                .SingleInstance();

            // Register UserNotificationStateRepository
            builder.RegisterType<UserNotificationStateRepository>()
                .As<IUserNotificationStateRepository>()
                .SingleInstance();

            // Register TeamConfigurationRepository
            builder.RegisterType<TeamConfigurationRepository>()
                .As<ITeamConfigurationRepository>()
                .SingleInstance();
        }
    }
}
