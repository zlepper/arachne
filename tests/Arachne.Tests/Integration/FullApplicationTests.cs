using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Arachne.Extensions;

namespace Arachne.Tests.Integration;

[TestFixture]
public class FullApplicationTests : TestBase
{
    private IServiceProvider _serviceProvider = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        
        // Create configuration with test connection string
        var configData = new Dictionary<string, string?>
        {
            ["SqlServerConfiguration:Servers:0:Name"] = "TestServer",
            ["SqlServerConfiguration:Servers:0:ConnectionString"] = GetMasterConnectionString(),
            
            // Modern schema query (should work on TestDatabase2)
            ["SqlServerConfiguration:Queries:0:Name"] = "FeatureUsage_v3",
            ["SqlServerConfiguration:Queries:0:Query"] = "SELECT u.UserName, f.FeatureName, f.LastUsed, f.UsageCount FROM FeatureUsage f JOIN Users u ON f.UserID = u.ID WHERE f.LastUsed > DATEADD(day, -30, GETDATE())",
            
            // Intermediate schema query (should work on databases with FeatureUsage table but no Users join)
            ["SqlServerConfiguration:Queries:1:Name"] = "FeatureUsage_v2",
            ["SqlServerConfiguration:Queries:1:Query"] = "SELECT UserID, FeatureName, LastUsed, UsageCount FROM FeatureUsage WHERE LastUsed > DATEADD(day, -30, GETDATE())",
            
            // Legacy schema query (should work on LegacyDatabase)
            ["SqlServerConfiguration:Queries:2:Name"] = "FeatureUsage_v1",
            ["SqlServerConfiguration:Queries:2:Query"] = "SELECT FeatureName, COUNT(*) as UsageCount FROM FeatureLog WHERE LogDate > DATEADD(day, -30, GETDATE()) GROUP BY FeatureName",
            
            ["SqlServerConfiguration:QueryTimeout"] = "30",
            ["SqlServerConfiguration:ConnectionTimeout"] = "15",
            ["SqlServerConfiguration:ExcludeSystemDatabases"] = "true",
            ["SqlServerConfiguration:StopOnFirstSuccessfulQuery"] = "true",
            ["SqlServerConfiguration:MaxConcurrentOperations"] = "5",
            
            ["OutputConfiguration:ShowEmptyResults"] = "true",
            ["OutputConfiguration:IncludeTimestamp"] = "true",
            ["OutputConfiguration:ShowQueryVersion"] = "true",
            ["OutputConfiguration:MaxRowsPerDatabase"] = "100",
            ["OutputConfiguration:GenerateMarkdownReport"] = "false",
            ["OutputConfiguration:MarkdownOutputPath"] = "test-results.md",
            ["OutputConfiguration:MarkdownIncludeFailedQueries"] = "true"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
            
        services.AddSingleton<IConfiguration>(configuration);
        services.AddArachneServices();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Test]
    public async Task FullWorkflow_WithMockData_ExecutesSuccessfully()
    {
        // Arrange
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var discoveryService = _serviceProvider.GetRequiredService<IDatabaseDiscoveryService>();
        var executionService = _serviceProvider.GetRequiredService<IFallbackQueryExecutionService>();
        var formatter = _serviceProvider.GetRequiredService<ITableFormatter>();

        var sqlConfig = configService.GetSqlServerConfiguration();
        var outputConfig = configService.GetOutputConfiguration();
        
        // Act - Simulate the main application workflow
        var allResults = new List<QueryResult>();
        
        foreach (var server in sqlConfig.Servers)
        {
            var databases = await discoveryService.DiscoverDatabasesAsync(
                server.ConnectionString, 
                sqlConfig.ExcludeSystemDatabases);

            foreach (var database in databases)
            {
                var connectionStringBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(server.ConnectionString);
                connectionStringBuilder.InitialCatalog = database.Name;
                var databaseConnectionString = connectionStringBuilder.ConnectionString;

                var result = await executionService.ExecuteQueriesAsync(
                    server.Name,
                    database.Name,
                    databaseConnectionString,
                    sqlConfig.Queries,
                    sqlConfig.QueryTimeout,
                    sqlConfig.StopOnFirstSuccessfulQuery);

                allResults.Add(result);
            }
        }

        var formattedResults = formatter.FormatResults(allResults, outputConfig);

        // Assert
        Assert.That(allResults.Count, Is.EqualTo(3)); // Three test databases
        
        // Check TestDatabase1 (empty modern schema) - should use modern query but return no data
        var testDb1Result = allResults.First(r => r.DatabaseName == "TestDatabase1");
        Assert.That(testDb1Result.HasError, Is.False);
        Assert.That(testDb1Result.SuccessfulQuery?.Name, Is.EqualTo("FeatureUsage_v3"));
        Assert.That(testDb1Result.HasData, Is.False);
        
        // Check TestDatabase2 (modern schema with data) - should use modern query and return data
        var testDb2Result = allResults.First(r => r.DatabaseName == "TestDatabase2");
        Assert.That(testDb2Result.HasError, Is.False);
        Assert.That(testDb2Result.SuccessfulQuery?.Name, Is.EqualTo("FeatureUsage_v3"));
        Assert.That(testDb2Result.HasData, Is.True);
        Assert.That(testDb2Result.RowCount, Is.EqualTo(3));
        
        // Check LegacyDatabase - should fall back to legacy query
        var legacyDbResult = allResults.First(r => r.DatabaseName == "LegacyDatabase");
        Assert.That(legacyDbResult.HasError, Is.False);
        Assert.That(legacyDbResult.SuccessfulQuery?.Name, Is.EqualTo("FeatureUsage_v1"));
        Assert.That(legacyDbResult.FailedQueryNames.Count, Is.EqualTo(2)); // Two modern queries should fail
        Assert.That(legacyDbResult.HasData, Is.True);
        Assert.That(legacyDbResult.RowCount, Is.EqualTo(2)); // Two different feature names in legacy data
        
        // Check formatted output contains expected elements
        Assert.That(formattedResults, Does.Contain("Cross-Database Query Results"));
        Assert.That(formattedResults, Does.Contain("TestServer"));
        Assert.That(formattedResults, Does.Contain("TestDatabase1"));
        Assert.That(formattedResults, Does.Contain("TestDatabase2"));
        Assert.That(formattedResults, Does.Contain("LegacyDatabase"));
        Assert.That(formattedResults, Does.Contain("[Query: FeatureUsage_v3]"));
        Assert.That(formattedResults, Does.Contain("[Query: FeatureUsage_v1]"));
        Assert.That(formattedResults, Does.Contain("⚠️  Queries failed"));
        Assert.That(formattedResults, Does.Contain("Query version usage:"));
        Assert.That(formattedResults, Does.Contain("FeatureUsage_v3: 2 database(s)"));
        Assert.That(formattedResults, Does.Contain("FeatureUsage_v1: 1 database(s)"));
    }

    [Test]
    public async Task FullWorkflow_DatabaseDiscovery_FindsExpectedDatabases()
    {
        // Arrange
        var discoveryService = _serviceProvider.GetRequiredService<IDatabaseDiscoveryService>();
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var sqlConfig = configService.GetSqlServerConfiguration();

        // Act
        var databases = await discoveryService.DiscoverDatabasesAsync(
            sqlConfig.Servers[0].ConnectionString, 
            sqlConfig.ExcludeSystemDatabases);

        // Assert
        Assert.That(databases.Count, Is.EqualTo(3));
        Assert.That(databases.Select(d => d.Name), Does.Contain("TestDatabase1"));
        Assert.That(databases.Select(d => d.Name), Does.Contain("TestDatabase2"));
        Assert.That(databases.Select(d => d.Name), Does.Contain("LegacyDatabase"));
        
        // Ensure all databases are online and not read-only
        Assert.That(databases.All(d => d.Status == "ONLINE"), Is.True);
        Assert.That(databases.All(d => !d.IsReadOnly), Is.True);
    }

    [Test]
    public void FullWorkflow_ConfigurationValidation_LoadsCorrectly()
    {
        // Arrange
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        // Act
        var sqlConfig = configService.GetSqlServerConfiguration();
        var outputConfig = configService.GetOutputConfiguration();

        // Assert
        Assert.That(sqlConfig.Servers.Count, Is.EqualTo(1));
        Assert.That(sqlConfig.Servers[0].Name, Is.EqualTo("TestServer"));
        Assert.That(sqlConfig.Queries.Count, Is.EqualTo(3));
        Assert.That(sqlConfig.Queries[0].Name, Is.EqualTo("FeatureUsage_v3"));
        Assert.That(sqlConfig.Queries[1].Name, Is.EqualTo("FeatureUsage_v2"));
        Assert.That(sqlConfig.Queries[2].Name, Is.EqualTo("FeatureUsage_v1"));
        Assert.That(sqlConfig.MaxConcurrentOperations, Is.EqualTo(5));
        
        Assert.That(outputConfig.ShowEmptyResults, Is.True);
        Assert.That(outputConfig.IncludeTimestamp, Is.True);
        Assert.That(outputConfig.ShowQueryVersion, Is.True);
        Assert.That(outputConfig.GenerateMarkdownReport, Is.False);
        Assert.That(outputConfig.MarkdownOutputPath, Is.EqualTo("test-results.md"));
        Assert.That(outputConfig.MarkdownIncludeFailedQueries, Is.True);
    }

    [Test]
    public async Task MarkdownFormatter_Integration_GeneratesCompleteReport()
    {
        // Arrange
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var discoveryService = _serviceProvider.GetRequiredService<IDatabaseDiscoveryService>();
        var executionService = _serviceProvider.GetRequiredService<IFallbackQueryExecutionService>();
        var markdownFormatter = _serviceProvider.GetRequiredService<IMarkdownFormatter>();

        var sqlConfig = configService.GetSqlServerConfiguration();
        var outputConfig = configService.GetOutputConfiguration();
        outputConfig.GenerateMarkdownReport = true; // Enable for this test
        
        // Act - Execute the full workflow to get real results
        var allResults = new List<QueryResult>();
        
        foreach (var server in sqlConfig.Servers)
        {
            var databases = await discoveryService.DiscoverDatabasesAsync(
                server.ConnectionString, 
                sqlConfig.ExcludeSystemDatabases);

            foreach (var database in databases)
            {
                var connectionStringBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(server.ConnectionString);
                connectionStringBuilder.InitialCatalog = database.Name;
                var databaseConnectionString = connectionStringBuilder.ConnectionString;

                var result = await executionService.ExecuteQueriesAsync(
                    server.Name,
                    database.Name,
                    databaseConnectionString,
                    sqlConfig.Queries,
                    sqlConfig.QueryTimeout,
                    sqlConfig.StopOnFirstSuccessfulQuery);

                allResults.Add(result);
            }
        }

        // Generate markdown report
        var markdownReport = await markdownFormatter.GenerateMarkdownReportAsync(allResults, outputConfig);

        // Assert - Check markdown structure and content
        Assert.That(markdownReport, Does.Contain("# Cross-Database Query Results"));
        Assert.That(markdownReport, Does.Contain("## Table of Contents"));
        Assert.That(markdownReport, Does.Contain("## Summary"));
        Assert.That(markdownReport, Does.Contain("## Results by Server"));
        Assert.That(markdownReport, Does.Contain("## Detailed Statistics"));
        
        // Check server sections
        Assert.That(markdownReport, Does.Contain("### TestServer"));
        
        // Check database sections
        Assert.That(markdownReport, Does.Contain("#### TestDatabase1"));
        Assert.That(markdownReport, Does.Contain("#### TestDatabase2"));
        Assert.That(markdownReport, Does.Contain("#### LegacyDatabase"));
        
        // Check status indicators
        Assert.That(markdownReport, Does.Contain("✅ Success"));
        
        // Check query version information
        Assert.That(markdownReport, Does.Contain("Query: FeatureUsage_v3"));
        Assert.That(markdownReport, Does.Contain("Query: FeatureUsage_v1"));
        
        // Check failed queries are shown
        Assert.That(markdownReport, Does.Contain("**Failed Queries:**"));
        
        // Check summary statistics
        Assert.That(markdownReport, Does.Contain("| **Total databases** | 3 |"));
        Assert.That(markdownReport, Does.Contain("| **Databases with results** | 2 |"));
        
        // Check query version usage statistics
        Assert.That(markdownReport, Does.Contain("### Query Version Usage"));
        Assert.That(markdownReport, Does.Contain("| FeatureUsage\\_v3 | 2 |"));
        Assert.That(markdownReport, Does.Contain("| FeatureUsage\\_v1 | 1 |"));
        
        // Check performance analysis
        Assert.That(markdownReport, Does.Contain("### Performance Analysis"));
        Assert.That(markdownReport, Does.Contain("**Fastest query**"));
        Assert.That(markdownReport, Does.Contain("**Slowest query**"));
        
        // Check data tables are properly formatted
        Assert.That(markdownReport, Does.Contain("| UserName | FeatureName |")); // TestDatabase2 data
        Assert.That(markdownReport, Does.Contain("| FeatureName | UsageCount |")); // LegacyDatabase data
        
        // Verify markdown is properly escaped
        Assert.That(markdownReport, Does.Not.Contain("*TestServer*")); // Should be escaped
        Assert.That(markdownReport, Does.Contain("\\*TestServer\\*").Or.Not.Contain("*TestServer*")); // Either escaped or no asterisks
    }
}