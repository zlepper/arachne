using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using Arachne.Models;
using Spectre.Console;

namespace Arachne.Services;

public class ApplicationOrchestrator : IApplicationOrchestrator
{
    private readonly IConfigurationService _configService;
    private readonly IDatabaseDiscoveryService _discoveryService;
    private readonly IFallbackQueryExecutionService _executionService;
    private readonly ITableFormatter _formatter;

    public ApplicationOrchestrator(
        IConfigurationService configService,
        IDatabaseDiscoveryService discoveryService,
        IFallbackQueryExecutionService executionService,
        ITableFormatter formatter)
    {
        _configService = configService;
        _discoveryService = discoveryService;
        _executionService = executionService;
        _formatter = formatter;
    }

    public async Task<int> ExecuteAsync()
    {
        try
        {
            // Load configuration
            var sqlConfig = _configService.GetSqlServerConfiguration();
            var outputConfig = _configService.GetOutputConfiguration();
            
            var allResults = new ConcurrentBag<QueryResult>();

            return await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .StartAsync(async ctx =>
                {
                    // Discovery phase
                    var discoveryTask = ctx.AddTask("[green]Discovering databases...[/]", maxValue: sqlConfig.Servers.Count);
                    
                    var allDatabaseTasks = new List<(string serverName, string serverDescription, string masterConnectionString, Task<List<DatabaseInfo>> databasesTask)>();
                    
                    foreach (var server in sqlConfig.Servers)
                    {
                        discoveryTask.Description = $"[green]Discovering databases on {server.Name}...[/]";
                        var databasesTask = _discoveryService.DiscoverDatabasesAsync(server.MasterConnectionString, sqlConfig.ExcludeSystemDatabases);
                        allDatabaseTasks.Add((server.Name, server.Description, server.MasterConnectionString, databasesTask));
                        discoveryTask.Increment(1);
                    }
                    
                    // Wait for all database discovery to complete
                    await Task.WhenAll(allDatabaseTasks.Select(t => t.databasesTask));
                    discoveryTask.StopTask();
                    
                    // Build list of all database operations to perform
                    var allOperations = new List<(string serverName, string databaseName, Func<Task> operation)>();
                    
                    foreach (var (serverName, serverDescription, masterConnectionString, databasesTask) in allDatabaseTasks)
                    {
                        try
                        {
                            var databases = await databasesTask;
                            
                            foreach (var database in databases)
                            {
                                // Capture variables for closure
                                var capturedServerName = serverName;
                                var capturedDatabaseName = database.Name;
                                var capturedMasterConnectionString = masterConnectionString;
                                
                                var operation = async () =>
                                {
                                    try
                                    {
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
                                    }
                                    catch (Exception ex)
                                    {
                                        allResults.Add(new QueryResult
                                        {
                                            ServerName = capturedServerName,
                                            DatabaseName = capturedDatabaseName,
                                            HasError = true,
                                            ErrorMessage = $"Database query failed: {ex.Message}"
                                        });
                                    }
                                };
                                
                                allOperations.Add((capturedServerName, capturedDatabaseName, operation));
                            }
                        }
                        catch (Exception ex)
                        {
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
                    
                    // Query execution phase
                    var queryTask = ctx.AddTask("[blue]Executing queries...[/]", maxValue: allOperations.Count);
                    
                    // Execute all database operations with global concurrency control
                    using var globalSemaphore = new SemaphoreSlim(sqlConfig.MaxConcurrentOperations, sqlConfig.MaxConcurrentOperations);
                    
                    var operationTasks = allOperations.Select(async (operationInfo) =>
                    {
                        await globalSemaphore.WaitAsync();
                        try
                        {
                            queryTask.Description = $"[blue]Querying {operationInfo.serverName}.{operationInfo.databaseName}...[/]";
                            await operationInfo.operation();
                            queryTask.Increment(1);
                        }
                        finally
                        {
                            globalSemaphore.Release();
                        }
                    });
                    
                    await Task.WhenAll(operationTasks);
                    queryTask.StopTask();

                    // Format and display results
                    var resultList = allResults.ToList();
                    var formattedResults = _formatter.FormatResults(resultList, outputConfig);
                    AnsiConsole.WriteLine(formattedResults);
                    
                    return 0;
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}