using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
        }
    }
}
