using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Arachne.Extensions;
using Arachne.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("AppSettings.Development.json", optional: true, reloadOnChange: true);

// Services
builder.Services.AddArachneServices();

var host = builder.Build();

// Execute application
var orchestrator = host.Services.GetRequiredService<IApplicationOrchestrator>();
return await orchestrator.ExecuteAsync();
