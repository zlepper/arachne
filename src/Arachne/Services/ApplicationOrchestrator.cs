using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using Arachne.Models;

namespace Arachne.Services;

public class ApplicationOrchestrator : IApplicationOrchestrator
{
    private readonly IConfigurationService _configService;
    private readonly IDatabaseDiscoveryService _discoveryService;
    private readonly IFallbackQueryExecutionService _executionService;
    private readonly ITableFormatter _formatter;
    private readonly IConsoleLogger _logger;

    public ApplicationOrchestrator(
        IConfigurationService configService,
        IDatabaseDiscoveryService discoveryService,
        IFallbackQueryExecutionService executionService,
        ITableFormatter formatter,
        IConsoleLogger logger)
    {
        _configService = configService;
        _discoveryService = discoveryService;
        _executionService = executionService;
        _formatter = formatter;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync()
    {
        try
        {
            // Load configuration
            var sqlConfig = _configService.GetSqlServerConfiguration();
            var outputConfig = _configService.GetOutputConfiguration();
            
            var allResults = new ConcurrentBag<QueryResult>();
            
            _logger.WriteLine($"Starting cross-database query execution...");
            _logger.WriteLine($"Configured servers: {sqlConfig.Servers.Count}");
            _logger.WriteLine($"Configured queries: {sqlConfig.Queries.Count}");
            _logger.WriteLine($"Max concurrent operations: {sqlConfig.MaxConcurrentOperations}");
            _logger.WriteLine();

            // First, discover all databases across all servers
            var allDatabaseTasks = new List<(string serverName, string serverDescription, string masterConnectionString, Task<List<DatabaseInfo>> databasesTask)>();
            
            foreach (var server in sqlConfig.Servers)
            {
                _logger.WriteLine($"Discovering databases on server: {server.Name}");
                var databasesTask = _discoveryService.DiscoverDatabasesAsync(server.MasterConnectionString, sqlConfig.ExcludeSystemDatabases);
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
                    _logger.WriteLine($"  Found {databases.Count} databases on {serverName}");
                    
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
                                _logger.WriteLine($"  Querying database: {capturedDatabaseName} on {capturedServerName}");
                                
                                // Build connection string for specific database
                                var connectionStringBuilder = new SqlConnectionStringBuilder(capturedMasterConnectionString);
                                connectionStringBuilder.InitialCatalog = capturedDatabaseName;
                                var databaseConnectionString = connectionStringBuilder.ConnectionString;

                                // Execute fallback queries
                                var result = await _executionService.ExecuteQueriesAsync(
                                    capturedServerName,
                                    capturedDatabaseName,
                                    databaseConnectionString,
                                    sqlConfig.Queries,
                                    sqlConfig.QueryTimeout,
                                    sqlConfig.StopOnFirstSuccessfulQuery);

                                allResults.Add(result);
                                
                                if (result.HasError)
                                {
                                    _logger.WriteLine($"    ❌ Error on {capturedServerName}.{capturedDatabaseName}: {result.ErrorMessage}");
                                }
                                else if (result.HasData)
                                {
                                    _logger.WriteLine($"    ✅ Success on {capturedServerName}.{capturedDatabaseName}: {result.RowCount} rows using {result.SuccessfulQuery?.Name}");
                                }
                                else
                                {
                                    _logger.WriteLine($"    ✅ Success on {capturedServerName}.{capturedDatabaseName}: No data returned using {result.SuccessfulQuery?.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.WriteLine($"    ❌ Database error on {capturedServerName}.{capturedDatabaseName}: {ex.Message}");
                                
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
                    _logger.WriteLine($"  ❌ Server error on {serverName}: {ex.Message}");
                    
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
            
            _logger.WriteLine($"Total database operations to perform: {allOperations.Count}");
            _logger.WriteLine();
            
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
            _logger.WriteLine();

            // Format and display results
            _logger.Clear();
            var resultList = allResults.ToList();
            var formattedResults = _formatter.FormatResults(resultList, outputConfig);
            _logger.WriteLine(formattedResults);
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"Application error: {ex.Message}");
            _logger.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
}