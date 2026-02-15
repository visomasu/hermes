using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermes.Integration.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Configures the test application host and allows service override.
/// </summary>
public class HermesWebApplicationFactory : WebApplicationFactory<Program>
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureServices(services =>
		{
			// Configure test-specific services here if needed
			// For example, you could replace CosmosDB with in-memory storage
		});

		builder.UseEnvironment("Development");
	}

	protected override IHost CreateHost(IHostBuilder builder)
	{
		// Allow tests to configure the host
		return base.CreateHost(builder);
	}
}
