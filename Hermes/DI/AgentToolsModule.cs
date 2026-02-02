using Autofac;
using Hermes.Tools;
using Hermes.Tools.AzureDevOps;
using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Hermes.Tools.UserManagement;
using Hermes.Tools.UserManagement.Capabilities;
using Hermes.Tools.UserManagement.Capabilities.Inputs;
using Hermes.Tools.WorkItemSla;
using Hermes.Tools.WorkItemSla.Capabilities;
using Hermes.Tools.WorkItemSla.Capabilities.Inputs;
using Integrations.AzureDevOps;

namespace Hermes.DI
{
	/// <summary>
	/// Autofac module for registering agent tools.
	/// </summary>
	public class AgentToolsModule : Module
	{
		private readonly IConfiguration _configuration;

		public AgentToolsModule(IConfiguration configuration)
		{
			_configuration = configuration;
		}

		protected override void Load(ContainerBuilder builder)
		{
            // Azure DevOps
            builder.Register(c =>
			{
				var client = c.Resolve<IAzureDevOpsWorkItemClient>();
				var maxConcurrentFetches = _configuration.GetValue<int>("AzureDevOps:MaxConcurrentTreeFetches", 5);
				return new GetWorkItemTreeCapability(client, maxConcurrentFetches);
			})
				.As<IAgentToolCapability<GetWorkItemTreeCapabilityInput>>()
				.AsSelf()
				.InstancePerDependency();

			builder.RegisterType<GetWorkItemsByAreaPathCapability>()
				.As<IAgentToolCapability<GetWorkItemsByAreaPathCapabilityInput>>()
				.AsSelf()
				.InstancePerDependency();

			builder.RegisterType<GetParentHierarchyCapability>()
				.As<IAgentToolCapability<GetParentHierarchyCapabilityInput>>()
				.AsSelf()
				.InstancePerDependency();

			builder.RegisterType<GetFullHierarchyCapability>()
				.As<IAgentToolCapability<GetFullHierarchyCapabilityInput>>()
				.AsSelf()
				.InstancePerDependency();

			builder.RegisterType<DiscoverUserActivityCapability>()
				.As<IAgentToolCapability<DiscoverUserActivityCapabilityInput>>()
				.AsSelf()
				.InstancePerDependency();

			builder.RegisterType<GenerateNewsletterCapability>()
				.As<IAgentToolCapability<GenerateNewsletterCapabilityInput>>()
				.AsSelf()
				.InstancePerDependency();

			builder.RegisterType<AzureDevOpsTool>()
				.AsSelf()
				.SingleInstance();

			// User Management
			builder.RegisterType<RegisterSlaNotificationsCapability>()
				.As<IAgentToolCapability<RegisterSlaNotificationsCapabilityInput>>()
				.AsSelf()
				.InstancePerDependency();

			builder.RegisterType<UnregisterSlaNotificationsCapability>()
				.As<IAgentToolCapability<UnregisterSlaNotificationsCapabilityInput>>()
				.AsSelf()
				.InstancePerDependency();

			builder.RegisterType<UserManagementTool>()
				.AsSelf()
				.SingleInstance();

			// Work Item SLA
			builder.RegisterType<CheckSlaViolationsCapability>()
				.As<IAgentToolCapability<CheckSlaViolationsCapabilityInput>>()
				.AsSelf()
				.InstancePerDependency();

			builder.RegisterType<WorkItemSlaTool>()
				.AsSelf()
				.SingleInstance();
		}
	}
}
