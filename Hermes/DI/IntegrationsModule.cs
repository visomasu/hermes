using Autofac;
using Integrations.AzureDevOps;

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
			string organization;
			string project;
			string pat;

			if (_environment.IsDevelopment())
			{
				organization = "dynamicscrm";
				project = "OneCRM";
				pat = "";
			}
			else
			{
                organization = _configuration["AzureDevOps:Organization"] ?? string.Empty;
                project = _configuration["AzureDevOps:Project"] ?? string.Empty;
				pat = _configuration["AzureDevOps:PersonalAccessToken"] ?? string.Empty;
			}

			builder.Register(c =>
			{
				return new AzureDevOpsWorkItemClient(organization, project, pat);
			})
			.As<IAzureDevOpsWorkItemClient>()
			.SingleInstance();
		}
	}
}
