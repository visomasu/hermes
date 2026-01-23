using Autofac;
using Hermes.Configuration;
using Hermes.Scheduling;
using Hermes.Scheduling.Jobs;
using Hermes.Notifications.WorkItemSla;

namespace Hermes.DI
{
	/// <summary>
	/// Autofac module for scheduling-related dependency registrations.
	/// </summary>
	public class SchedulingModule : Module
	{
		private readonly IConfiguration _configuration;

		public SchedulingModule(IConfiguration configuration)
		{
			_configuration = configuration;
		}

		protected override void Load(ContainerBuilder builder)
		{
			// Register SchedulingConfiguration
			builder.Register(ctx =>
			{
				var config = new SchedulingConfiguration();
				_configuration.GetSection("Scheduling").Bind(config);
				return config;
			})
			.AsSelf()
			.SingleInstance();

			// Register JobTypeResolver
			builder.RegisterType<JobTypeResolver>()
				.AsSelf()
				.SingleInstance();

			// Register SchedulerSetup as IHostedService
			// Note: IHostedService is registered via Microsoft.Extensions.DependencyInjection in Program.cs
			// We register the concrete type here so it can be resolved by Autofac
			builder.RegisterType<SchedulerSetup>()
				.AsSelf()
				.SingleInstance();

			// Register jobs
			builder.RegisterType<SampleJob>()
				.AsSelf()
				.InstancePerDependency();

			builder.RegisterType<WorkItemUpdateSlaJob>()
				.AsSelf()
				.InstancePerDependency();

			// Register WorkItemUpdateSla configuration
			builder.Register(ctx =>
			{
				var config = new WorkItemUpdateSlaConfiguration();
				_configuration.GetSection("WorkItemUpdateSla").Bind(config);
				return config;
			})
			.AsSelf()
			.SingleInstance();

			// Register WorkItemUpdateSla services
			builder.RegisterType<WorkItemUpdateSlaMessageComposer>()
				.AsSelf()
				.SingleInstance();

			builder.RegisterType<WorkItemUpdateSlaEvaluator>()
				.As<IWorkItemUpdateSlaEvaluator>()
				.SingleInstance();
		}
	}
}
