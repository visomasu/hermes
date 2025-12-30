using Autofac;
using Hermes.Channels.Teams;
using Hermes.DI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;

var builder = WebApplication.CreateBuilder(args);

// Use Kestrel as the web server
builder.WebHost.UseKestrel();

// Add HttpClient services BEFORE Autofac configuration
// This registers IHttpClientFactory in the built-in DI container
builder.Services.AddHttpClient();

// Use Autofac as the service provider
builder.Host.UseServiceProviderFactory(new Autofac.Extensions.DependencyInjection.AutofacServiceProviderFactory());

// Register HermesModule as Autofac module, passing configuration and environment directly
builder.Host.ConfigureContainer<Autofac.ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterModule(new HermesModule(builder.Configuration, builder.Environment));
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

// Configure the HTTP request pipeline.
// Removed Swagger/OpenAPI middleware

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// This receives incoming messages from Azure Bot Service or other SDK Agents
var incomingRoute = app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

app.Run();
