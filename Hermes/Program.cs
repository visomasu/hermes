using Autofac;
using Hermes.Channels.Teams;
using Hermes.DI;
using Hermes.Scheduling;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Http;
using Quartz;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for file logging (required for Hermes.Evals)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hermes",
            "logs",
            "hermes-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// DEVELOPMENT ONLY: Disable SSL certificate validation for local testing
if (builder.Environment.IsDevelopment())
{
    // Global bypass for all ServicePoint connections
    System.Net.ServicePointManager.ServerCertificateValidationCallback =
        (sender, certificate, chain, sslPolicyErrors) => true;

    // Also set security protocol
    System.Net.ServicePointManager.SecurityProtocol =
        System.Net.SecurityProtocolType.Tls12 |
        System.Net.SecurityProtocolType.Tls13;
}

// Use Kestrel as the web server
builder.WebHost.UseKestrel();

// Add HttpClient services BEFORE Autofac configuration
// This registers IHttpClientFactory in the built-in DI container
builder.Services.AddHttpClient();

// DEVELOPMENT ONLY: Configure HttpClient to bypass certificate validation
if (builder.Environment.IsDevelopment())
{
    builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
    {
        options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
        {
            handlerBuilder.PrimaryHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
        });
    });
}

// Add Quartz.NET scheduler
builder.Services.AddQuartz(q =>
{
    q.UseSimpleTypeLoader();
    q.UseInMemoryStore();
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Use Autofac as the service provider
builder.Host.UseServiceProviderFactory(new Autofac.Extensions.DependencyInjection.AutofacServiceProviderFactory());

// Register HermesModule as Autofac module, passing configuration and environment directly
builder.Host.ConfigureContainer<Autofac.ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterModule(new HermesModule(builder.Configuration, builder.Environment));
});

// Register SchedulerSetup as hosted service (resolved from Autofac container)
builder.Services.AddHostedService(sp =>
{
    var lifetime = sp.GetRequiredService<IHostApplicationLifetime>();
    var autofacContainer = sp.GetRequiredService<ILifetimeScope>();
    return autofacContainer.Resolve<SchedulerSetup>();
});

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<HermesTeamsAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

// Add services to the container.
builder.Services.AddControllers();

var app = builder.Build();

// Seed team configurations from appsettings.json on startup (optional for development)
// Production environments should use REST API at /api/teamconfiguration
using (var scope = app.Services.CreateScope())
{
    var container = scope.ServiceProvider.GetRequiredService<ILifetimeScope>();
    var seeder = container.Resolve<Hermes.Infrastructure.TeamConfigurationSeeder>();
    await seeder.SeedTeamsAsync();
}

// Configure the HTTP request pipeline.
// Removed Swagger/OpenAPI middleware

// DEVELOPMENT ONLY: Don't redirect to HTTPS for local testing with Agents Playground
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Enable WebSocket support
app.UseWebSockets();

app.UseAuthorization();

app.MapControllers();

// This receives incoming messages from Azure Bot Service or other SDK Agents
var incomingRoute = app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

try
{
    Log.Information("Starting Hermes application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Hermes application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for integration tests (WebApplicationFactory)
public partial class Program { }
