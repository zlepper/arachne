using Microsoft.Extensions.DependencyInjection;
using CrossDatabaseQuery.Services;

namespace CrossDatabaseQuery.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCrossDatabaseQueryServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IDatabaseDiscoveryService, DatabaseDiscoveryService>();
        services.AddSingleton<IFallbackQueryExecutionService, FallbackQueryExecutionService>();
        services.AddSingleton<ITableFormatter, TableFormatter>();
        
        return services;
    }
}