using Autofac;
using Hermes.Tools;
using Hermes.Tools.AzureDevOps;
using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Hermes.Tools.UserManagement;
using Hermes.Tools.UserManagement.Capabilities;
using Hermes.Tools.UserManagement.Capabilities.Inputs;

namespace Hermes.DI
{
	/// <summary>
	/// Autofac module for registering agent tools.
	/// </summary>
	public class AgentToolsModule : Module
	{
		protected override void Load(ContainerBuilder builder)
		{
            // Azure DevOps
            builder.RegisterType<GetWorkItemTreeCapability>()
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
		}
	}
}
