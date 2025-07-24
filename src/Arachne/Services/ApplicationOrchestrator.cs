using System.Collections.Concurrent;
using Spectre.Console;

namespace Arachne.Services;

public class ApplicationOrchestrator(
    IConfigurationService configService,
    IDatabaseDiscoveryService discoveryService,
    IFallbackQueryExecutionService executionService,
    ITableFormatter formatter,
    IMarkdownFormatter markdownFormatter) : IApplicationOrchestrator
{
    public async Task<int> ExecuteAsync()
    {
        try
        {
            // Load configuration
            var sqlConfig = configService.GetSqlServerConfiguration();
            var outputConfig = configService.GetOutputConfiguration();
            
            var allResults = new ConcurrentBag<QueryResult>();

            return await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .StartAsync(async ctx =>
                {
                    // Discovery phase
                    var discoveryTask = ctx.AddTask("[green]Discovering databases...[/]", maxValue: sqlConfig.Servers.Count);
                    
                    List<(string serverName, string connectionString, Task<List<DatabaseInfo>> databasesTask)> allDatabaseTasks = [];
                    
                    foreach (var server in sqlConfig.Servers)
                    {
                        discoveryTask.Description = $"[green]Discovering databases on {server.Name}...[/]";
                        var databasesTask = discoveryService.DiscoverDatabasesAsync(server.ConnectionString, sqlConfig.ExcludeSystemDatabases);
                        allDatabaseTasks.Add((server.Name, server.ConnectionString, databasesTask));
                        discoveryTask.Increment(1);
                    }
                    
                    // Wait for all database discovery to complete
                    await Task.WhenAll(allDatabaseTasks.Select(t => t.databasesTask));
                    discoveryTask.StopTask();
                    
                    // Build list of all database operations to perform
                    List<(string serverName, string databaseName, Func<Task> operation)> allOperations = [];
                    
                    foreach (var (serverName, connectionString, databasesTask) in allDatabaseTasks)
                    {
                        try
                        {
                            var databases = await databasesTask;
                            
                            foreach (var database in databases)
                            {
                                // Capture variables for closure
                                var capturedServerName = serverName;
                                var capturedDatabaseName = database.Name;
                                var capturedConnectionString = connectionString;
                                
                                var operation = async () =>
                                {
                                    try
                                    {
                                        // Build connection string for specific database
                                        var connectionStringBuilder = new SqlConnectionStringBuilder(capturedConnectionString);
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
                    var formattedResults = formatter.FormatResults(resultList, outputConfig);
                    AnsiConsole.WriteLine(formattedResults);
                    
                    // Generate markdown report if enabled
                    if (outputConfig.GenerateMarkdownReport)
                    {
                        try
                        {
                            var reportTask = ctx.AddTask("[yellow]Generating markdown report...[/]", maxValue: 1);
                            var markdownReport = await markdownFormatter.GenerateMarkdownReportAsync(resultList, outputConfig);
                            await File.WriteAllTextAsync(outputConfig.MarkdownOutputPath, markdownReport);
                            reportTask.Increment(1);
                            reportTask.StopTask();
                            
                            AnsiConsole.MarkupLine($"[green]✓[/] Markdown report saved to: [link]{Path.GetFullPath(outputConfig.MarkdownOutputPath)}[/]");
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[red]✗[/] Failed to generate markdown report: {ex.Message}");
                        }
                    }
                    
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