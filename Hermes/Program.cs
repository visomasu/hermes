using Autofac;
using Hermes.DI;

var builder = WebApplication.CreateBuilder(args);

// Use Kestrel as the web server
builder.WebHost.UseKestrel();

// Use Autofac as the service provider
builder.Host.UseServiceProviderFactory(new Autofac.Extensions.DependencyInjection.AutofacServiceProviderFactory());

// Register HermesModule as Autofac module, passing configuration and environment directly
builder.Host.ConfigureContainer<Autofac.ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterModule(new HermesModule(builder.Configuration, builder.Environment));
});

// Add services to the container.
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Removed Swagger/OpenAPI middleware

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
