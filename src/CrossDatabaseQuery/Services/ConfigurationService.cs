using Microsoft.Extensions.Configuration;
using CrossDatabaseQuery.Models;

namespace CrossDatabaseQuery.Services;

public interface IConfigurationService
{
    SqlServerConfiguration GetSqlServerConfiguration();
    OutputConfiguration GetOutputConfiguration();
}

public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;

    public ConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SqlServerConfiguration GetSqlServerConfiguration()
    {
        var config = new SqlServerConfiguration();
        _configuration.GetSection("SqlServerConfiguration").Bind(config);
        
        if (config.Servers.Count == 0)
        {
            throw new InvalidOperationException("No SQL Server configurations found in AppSettings.json");
        }
        
        if (config.Queries.Count == 0)
        {
            throw new InvalidOperationException("No query configurations found in AppSettings.json");
        }
        
        return config;
    }

    public OutputConfiguration GetOutputConfiguration()
    {
        var config = new OutputConfiguration();
        _configuration.GetSection("OutputConfiguration").Bind(config);
        return config;
    }
}