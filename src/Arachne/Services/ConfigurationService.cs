using Microsoft.Extensions.Configuration;

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

        // Resolve QueryFile references into Query text
        foreach (var query in config.Queries)
        {
            if (query.QueryFile is not null && query.Query is not null)
                throw new InvalidOperationException(
                    $"Query '{query.Name}' has both 'Query' and 'QueryFile' set. Use only one.");

            if (query.QueryFile is not null)
            {
                var path = Path.IsPathRooted(query.QueryFile)
                    ? query.QueryFile
                    : Path.Combine(Directory.GetCurrentDirectory(), query.QueryFile);

                if (!File.Exists(path))
                    throw new InvalidOperationException(
                        $"Query '{query.Name}': sql file not found at '{path}'.");

                query.Query = File.ReadAllText(path);
                query.QueryFile = null;
            }

            if (string.IsNullOrWhiteSpace(query.Query))
                throw new InvalidOperationException(
                    $"Query '{query.Name}' has no 'Query' or 'QueryFile' configured.");
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