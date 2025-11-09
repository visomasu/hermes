using Autofac;
using Hermes.Tools;
using Hermes.Tools.AzureDevOps;

namespace Hermes.DI
{
	/// <summary>
	/// Autofac module for registering agent tools.
	/// </summary>
	public class AgentToolsModule : Module
	{
		protected override void Load(ContainerBuilder builder)
		{
			builder.RegisterType<AzureDevOpsTool>()
				.AsSelf()
				.SingleInstance();
		}
	}
}
