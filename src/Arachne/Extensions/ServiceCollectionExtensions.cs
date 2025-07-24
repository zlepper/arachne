using Microsoft.Extensions.DependencyInjection;
using Arachne.Services;

namespace Arachne.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArachneServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IDatabaseDiscoveryService, DatabaseDiscoveryService>();
        services.AddSingleton<ISecureQueryExecutionService, SecureQueryExecutionService>();
        services.AddSingleton<IFallbackQueryExecutionService, FallbackQueryExecutionService>();
        services.AddSingleton<ITableFormatter, TableFormatter>();
        services.AddSingleton<IMarkdownFormatter, MarkdownFormatter>();
        services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();
        
        return services;
    }
}