using Autofac;

namespace Hermes.DI
{
    /// <summary>
    /// Autofac module for registering Microsoft Teams channel dependencies.
    /// </summary>
    public class TeamsModule : Module
    {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the TeamsModule class.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="environment">Host environment.</param>
        public TeamsModule(IConfiguration configuration, IHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        /// <summary>
        /// Loads and registers Teams channel dependencies into the container.
        /// </summary>
        /// <param name="builder">The container builder.</param>
        protected override void Load(ContainerBuilder builder)
        {
            // Register Teams channel dependencies here
        }
    }
}
