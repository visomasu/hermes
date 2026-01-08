using Autofac;
using Hermes.Tools;
using Hermes.Tools.AzureDevOps;
using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;

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

			builder.RegisterType<AzureDevOpsTool>()
				.AsSelf()
				.SingleInstance();
		}
	}
}
