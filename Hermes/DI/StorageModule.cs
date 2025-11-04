using Autofac;
using Hermes.Storage.Core;
using Hermes.Storage.Core.CosmosDB;
using Hermes.Storage.Core.InMemory;
using Hermes.Storage.Repositories;
using Hermes.Storage.Repositories.Sample;

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
        }
    }
}
