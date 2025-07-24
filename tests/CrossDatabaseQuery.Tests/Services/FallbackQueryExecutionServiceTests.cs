using CrossDatabaseQuery.Services;
using CrossDatabaseQuery.Models;

namespace CrossDatabaseQuery.Tests.Services;

[TestFixture]
public class FallbackQueryExecutionServiceTests : TestBase
{
    private IFallbackQueryExecutionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new FallbackQueryExecutionService();
    }

    [Test]
    public async Task ExecuteQueriesAsync_ModernSchema_UsesFirstQuery()
    {
        // Arrange
        var queries = new List<QueryDefinition>
        {
            new() { Name = "Modern_v3", Query = "SELECT u.UserName, f.FeatureName, f.UsageCount FROM FeatureUsage f JOIN Users u ON f.UserID = u.ID" },
            new() { Name = "Legacy_v1", Query = "SELECT FeatureName, COUNT(*) as UsageCount FROM FeatureLog GROUP BY FeatureName" }
        };
        var connectionString = GetDatabaseConnectionString("TestDatabase2");

        // Act
        var result = await _service.ExecuteQueriesAsync("TestServer", "TestDatabase2", connectionString, queries, 30);

        // Assert
        Assert.That(result.HasError, Is.False);
        Assert.That(result.HasData, Is.True);
        Assert.That(result.SuccessfulQuery?.Name, Is.EqualTo("Modern_v3"));
        Assert.That(result.FailedQueryNames.Count, Is.EqualTo(0));
        Assert.That(result.Data!.Rows.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task ExecuteQueriesAsync_LegacySchema_FallsBackToLegacyQuery()
    {
        // Arrange
        var queries = new List<QueryDefinition>
        {
            new() { Name = "Modern_v3", Query = "SELECT u.UserName, f.FeatureName, f.UsageCount FROM FeatureUsage f JOIN Users u ON f.UserID = u.ID" },
            new() { Name = "Modern_v2", Query = "SELECT UserID, FeatureName, UsageCount FROM FeatureUsage" },
            new() { Name = "Legacy_v1", Query = "SELECT FeatureName, COUNT(*) as UsageCount FROM FeatureLog GROUP BY FeatureName" }
        };
        var connectionString = GetDatabaseConnectionString("LegacyDatabase");

        // Act
        var result = await _service.ExecuteQueriesAsync("TestServer", "LegacyDatabase", connectionString, queries, 30);

        // Assert
        Assert.That(result.HasError, Is.False);
        Assert.That(result.HasData, Is.True);
        Assert.That(result.SuccessfulQuery?.Name, Is.EqualTo("Legacy_v1"));
        Assert.That(result.FailedQueryNames.Count, Is.EqualTo(2));
        Assert.That(result.FailedQueryNames[0], Does.Contain("Modern_v3"));
        Assert.That(result.FailedQueryNames[1], Does.Contain("Modern_v2"));
        Assert.That(result.Data!.Rows.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task ExecuteQueriesAsync_NoDataReturned_ReturnsEmptyResult()
    {
        // Arrange
        var queries = new List<QueryDefinition>
        {
            new() { Name = "EmptyQuery", Query = "SELECT * FROM Users WHERE 1=0" }
        };
        var connectionString = GetDatabaseConnectionString("TestDatabase1");

        // Act
        var result = await _service.ExecuteQueriesAsync("TestServer", "TestDatabase1", connectionString, queries, 30);

        // Assert
        Assert.That(result.HasError, Is.False);
        Assert.That(result.HasData, Is.False);
        Assert.That(result.SuccessfulQuery?.Name, Is.EqualTo("EmptyQuery"));
        Assert.That(result.Data!.Rows.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task ExecuteQueriesAsync_AllQueriesFail_ReturnsError()
    {
        // Arrange
        var queries = new List<QueryDefinition>
        {
            new() { Name = "BadQuery1", Query = "SELECT * FROM NonExistentTable1" },
            new() { Name = "BadQuery2", Query = "SELECT * FROM NonExistentTable2" }
        };
        var connectionString = GetDatabaseConnectionString("TestDatabase1");

        // Act
        var result = await _service.ExecuteQueriesAsync("TestServer", "TestDatabase1", connectionString, queries, 30);

        // Assert
        Assert.That(result.HasError, Is.True);
        Assert.That(result.HasData, Is.False);
        Assert.That(result.SuccessfulQuery, Is.Null);
        Assert.That(result.FailedQueryNames.Count, Is.EqualTo(2));
        Assert.That(result.ErrorMessage, Does.Contain("All queries failed"));
    }

    [Test]
    public async Task ExecuteQueriesAsync_NonSchemaError_StopsExecution()
    {
        // Arrange
        var queries = new List<QueryDefinition>
        {
            new() { Name = "TimeoutQuery", Query = "WAITFOR DELAY '00:01:00'" }, // Will timeout
            new() { Name = "GoodQuery", Query = "SELECT COUNT(*) FROM Users" }
        };
        var connectionString = GetDatabaseConnectionString("TestDatabase1");

        // Act
        var result = await _service.ExecuteQueriesAsync("TestServer", "TestDatabase1", connectionString, queries, queryTimeout: 1);

        // Assert
        Assert.That(result.HasError, Is.True);
        Assert.That(result.HasData, Is.False);
        Assert.That(result.SuccessfulQuery, Is.Null);
        Assert.That(result.FailedQueryNames.Count, Is.EqualTo(1)); // Should stop after first timeout
        Assert.That(result.ErrorMessage, Does.Contain("Non-schema error"));
    }

    [Test]
    public async Task ExecuteQueriesAsync_StopOnFirstSuccess_False_ExecutesAllQueries()
    {
        // Arrange
        var queries = new List<QueryDefinition>
        {
            new() { Name = "Query1", Query = "SELECT 'First' as Result" },
            new() { Name = "Query2", Query = "SELECT 'Second' as Result" }
        };
        var connectionString = GetDatabaseConnectionString("TestDatabase1");

        // Act
        var result = await _service.ExecuteQueriesAsync("TestServer", "TestDatabase1", connectionString, queries, 30, stopOnFirstSuccess: false);

        // Assert
        Assert.That(result.HasError, Is.False);
        Assert.That(result.HasData, Is.True);
        Assert.That(result.SuccessfulQuery?.Name, Is.EqualTo("Query2")); // Last successful query
        Assert.That(result.FailedQueryNames.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task ExecuteQueriesAsync_MeasuresExecutionTime()
    {
        // Arrange
        var queries = new List<QueryDefinition>
        {
            new() { Name = "DelayQuery", Query = "WAITFOR DELAY '00:00:01'; SELECT 1 as Result" }
        };
        var connectionString = GetDatabaseConnectionString("TestDatabase1");

        // Act
        var result = await _service.ExecuteQueriesAsync("TestServer", "TestDatabase1", connectionString, queries, 30);

        // Assert
        Assert.That(result.HasError, Is.False);
        Assert.That(result.ExecutionTime.TotalMilliseconds, Is.GreaterThan(900)); // At least 0.9 seconds
        Assert.That(result.ExecutionTime.TotalSeconds, Is.LessThan(5)); // But not too long
    }
}