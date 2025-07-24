using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;
using CrossDatabaseQuery.Extensions;
using CrossDatabaseQuery.Services;
using CrossDatabaseQuery.Models;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("AppSettings.Development.json", optional: true, reloadOnChange: true);

// Services
builder.Services.AddCrossDatabaseQueryServices();

var host = builder.Build();

// Get services
var configService = host.Services.GetRequiredService<IConfigurationService>();
var discoveryService = host.Services.GetRequiredService<IDatabaseDiscoveryService>();
var executionService = host.Services.GetRequiredService<IFallbackQueryExecutionService>();
var formatter = host.Services.GetRequiredService<ITableFormatter>();

try
{
    // Load configuration
    var sqlConfig = configService.GetSqlServerConfiguration();
    var outputConfig = configService.GetOutputConfiguration();
    
    var allResults = new List<QueryResult>();
    
    Console.WriteLine($"Starting cross-database query execution...");
    Console.WriteLine($"Configured servers: {sqlConfig.Servers.Count}");
    Console.WriteLine($"Configured queries: {sqlConfig.Queries.Count}");
    Console.WriteLine();

    // Process each server
    foreach (var server in sqlConfig.Servers)
    {
        Console.WriteLine($"Processing server: {server.Name}");
        
        try
        {
            // Discover databases
            var databases = await discoveryService.DiscoverDatabasesAsync(
                server.MasterConnectionString, 
                sqlConfig.ExcludeSystemDatabases);
                
            Console.WriteLine($"  Found {databases.Count} databases");

            // Execute queries on each database
            foreach (var database in databases)
            {
                Console.WriteLine($"  Querying database: {database.Name}");
                
                // Build connection string for specific database
                var connectionStringBuilder = new SqlConnectionStringBuilder(server.MasterConnectionString);
                connectionStringBuilder.InitialCatalog = database.Name;
                var databaseConnectionString = connectionStringBuilder.ConnectionString;

                // Execute fallback queries
                var result = await executionService.ExecuteQueriesAsync(
                    server.Name,
                    database.Name,
                    databaseConnectionString,
                    sqlConfig.Queries,
                    sqlConfig.QueryTimeout,
                    sqlConfig.StopOnFirstSuccessfulQuery);

                allResults.Add(result);
                
                if (result.HasError)
                {
                    Console.WriteLine($"    ❌ Error: {result.ErrorMessage}");
                }
                else if (result.HasData)
                {
                    Console.WriteLine($"    ✅ Success: {result.RowCount} rows using {result.SuccessfulQuery?.Name}");
                }
                else
                {
                    Console.WriteLine($"    ✅ Success: No data returned using {result.SuccessfulQuery?.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Server error: {ex.Message}");
            
            // Add error result for this server
            allResults.Add(new QueryResult
            {
                ServerName = server.Name,
                DatabaseName = "N/A",
                HasError = true,
                ErrorMessage = $"Server connection failed: {ex.Message}"
            });
        }
        
        Console.WriteLine();
    }

    // Format and display results
    Console.Clear();
    var formattedResults = formatter.FormatResults(allResults, outputConfig);
    Console.WriteLine(formattedResults);
}
catch (Exception ex)
{
    Console.WriteLine($"Application error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}

return 0;
