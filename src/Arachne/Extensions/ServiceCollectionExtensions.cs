using Microsoft.Extensions.DependencyInjection;
using Arachne.Services;

namespace Arachne.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArachneServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IDatabaseDiscoveryService, DatabaseDiscoveryService>();
        services.AddSingleton<IFallbackQueryExecutionService, FallbackQueryExecutionService>();
        services.AddSingleton<ITableFormatter, TableFormatter>();
        
        return services;
    }
}