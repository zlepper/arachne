using Microsoft.Extensions.Configuration;
using Arachne.Models;

namespace Arachne.Services;

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
        
        // Auto-generate names for queries that don't have them
        for (int i = 0; i < config.Queries.Count; i++)
        {
            if (string.IsNullOrEmpty(config.Queries[i].Name))
            {
                config.Queries[i].Name = $"Query{i + 1}";
            }
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