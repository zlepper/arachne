using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using Arachne.Extensions;
using Arachne.Services;
using Arachne.Models;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("AppSettings.Development.json", optional: true, reloadOnChange: true);

// Services
builder.Services.AddArachneServices();

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
    
    var allResults = new ConcurrentBag<QueryResult>();
    
    Console.WriteLine($"Starting cross-database query execution...");
    Console.WriteLine($"Configured servers: {sqlConfig.Servers.Count}");
    Console.WriteLine($"Configured queries: {sqlConfig.Queries.Count}");
    Console.WriteLine($"Max concurrent operations: {sqlConfig.MaxConcurrentOperations}");
    Console.WriteLine();

    // First, discover all databases across all servers
    var allDatabaseTasks = new List<(string serverName, string serverDescription, string masterConnectionString, Task<List<DatabaseInfo>> databasesTask)>();
    
    foreach (var server in sqlConfig.Servers)
    {
        Console.WriteLine($"Discovering databases on server: {server.Name}");
        var databasesTask = discoveryService.DiscoverDatabasesAsync(server.MasterConnectionString, sqlConfig.ExcludeSystemDatabases);
        allDatabaseTasks.Add((server.Name, server.Description, server.MasterConnectionString, databasesTask));
    }
    
    // Wait for all database discovery to complete
    await Task.WhenAll(allDatabaseTasks.Select(t => t.databasesTask));
    
    // Build list of all database operations to perform
    var allOperations = new List<Func<Task>>();
    
    foreach (var (serverName, serverDescription, masterConnectionString, databasesTask) in allDatabaseTasks)
    {
        try
        {
            var databases = await databasesTask;
            Console.WriteLine($"  Found {databases.Count} databases on {serverName}");
            
            foreach (var database in databases)
            {
                // Capture variables for closure
                var capturedServerName = serverName;
                var capturedDatabaseName = database.Name;
                var capturedMasterConnectionString = masterConnectionString;
                
                allOperations.Add(async () =>
                {
                    try
                    {
                        Console.WriteLine($"  Querying database: {capturedDatabaseName} on {capturedServerName}");
                        
                        // Build connection string for specific database
                        var connectionStringBuilder = new SqlConnectionStringBuilder(capturedMasterConnectionString);
                        connectionStringBuilder.InitialCatalog = capturedDatabaseName;
                        var databaseConnectionString = connectionStringBuilder.ConnectionString;

                        // Execute fallback queries
                        var result = await executionService.ExecuteQueriesAsync(
                            capturedServerName,
                            capturedDatabaseName,
                            databaseConnectionString,
                            sqlConfig.Queries,
                            sqlConfig.QueryTimeout,
                            sqlConfig.StopOnFirstSuccessfulQuery);

                        allResults.Add(result);
                        
                        if (result.HasError)
                        {
                            Console.WriteLine($"    ❌ Error on {capturedServerName}.{capturedDatabaseName}: {result.ErrorMessage}");
                        }
                        else if (result.HasData)
                        {
                            Console.WriteLine($"    ✅ Success on {capturedServerName}.{capturedDatabaseName}: {result.RowCount} rows using {result.SuccessfulQuery?.Name}");
                        }
                        else
                        {
                            Console.WriteLine($"    ✅ Success on {capturedServerName}.{capturedDatabaseName}: No data returned using {result.SuccessfulQuery?.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    ❌ Database error on {capturedServerName}.{capturedDatabaseName}: {ex.Message}");
                        
                        allResults.Add(new QueryResult
                        {
                            ServerName = capturedServerName,
                            DatabaseName = capturedDatabaseName,
                            HasError = true,
                            ErrorMessage = $"Database query failed: {ex.Message}"
                        });
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Server error on {serverName}: {ex.Message}");
            
            // Add error result for this server
            allResults.Add(new QueryResult
            {
                ServerName = serverName,
                DatabaseName = "N/A",
                HasError = true,
                ErrorMessage = $"Server connection failed: {ex.Message}"
            });
        }
    }
    
    Console.WriteLine($"Total database operations to perform: {allOperations.Count}");
    Console.WriteLine();
    
    // Execute all database operations with global concurrency control
    using var globalSemaphore = new SemaphoreSlim(sqlConfig.MaxConcurrentOperations, sqlConfig.MaxConcurrentOperations);
    
    var operationTasks = allOperations.Select(async operation =>
    {
        await globalSemaphore.WaitAsync();
        try
        {
            await operation();
        }
        finally
        {
            globalSemaphore.Release();
        }
    });
    
    await Task.WhenAll(operationTasks);
    Console.WriteLine();

    // Format and display results
    Console.Clear();
    var resultList = allResults.ToList();
    var formattedResults = formatter.FormatResults(resultList, outputConfig);
    Console.WriteLine(formattedResults);
}
catch (Exception ex)
{
    Console.WriteLine($"Application error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}

return 0;
