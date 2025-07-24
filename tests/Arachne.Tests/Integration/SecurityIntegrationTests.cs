using Microsoft.Data.SqlClient;
using Arachne.Services;
using Arachne.Models;

namespace Arachne.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class SecurityIntegrationTests : TestBase
{
    private FallbackQueryExecutionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var secureExecutionService = new SecureQueryExecutionService();
        _service = new FallbackQueryExecutionService(secureExecutionService);
    }

    [Test]
    public async Task FallbackQueryExecution_WithDestructiveQuery_ShouldFail()
    {
        // Arrange - Try to execute a destructive query through the fallback system
        var destructiveQueries = new List<QueryDefinition>
        {
            new() { Name = "DestructiveInsert", Query = "INSERT INTO Users (UserName, Email) VALUES ('hacker', 'hacker@evil.com')" },
            new() { Name = "DestructiveUpdate", Query = "UPDATE Users SET Email = 'hacked@evil.com'" },
            new() { Name = "DestructiveDelete", Query = "DELETE FROM Users" }
        };
        var connectionString = GetDatabaseConnectionString("TestDatabase2");

        // Act
        var result = await _service.ExecuteQueriesAsync("TestServer", "TestDatabase2", connectionString, destructiveQueries, 30);

        // Assert - All destructive queries should fail due to read-only permissions
        Assert.That(result.HasError, Is.True, "Destructive queries should be blocked");
        Assert.That(result.HasData, Is.False, "No data should be returned from failed destructive queries");
        Assert.That(result.SuccessfulQuery, Is.Null, "No destructive query should succeed");
        Assert.That(result.FailedQueryNames.Count, Is.GreaterThan(0), "All destructive queries should fail");
    }

    [Test]
    public async Task FallbackQueryExecution_WithReadOnlyQuery_ShouldSucceed()
    {
        // Arrange - Safe read-only queries
        var readOnlyQueries = new List<QueryDefinition>
        {
            new() { Name = "SafeSelect", Query = "SELECT COUNT(*) as UserCount FROM Users" },
            new() { Name = "FallbackSelect", Query = "SELECT 'No Users' as Message" }
        };
        var connectionString = GetDatabaseConnectionString("TestDatabase2");

        // Act
        var result = await _service.ExecuteQueriesAsync("TestServer", "TestDatabase2", connectionString, readOnlyQueries, 30);

        // Assert - Read-only queries should work fine
        Assert.That(result.HasError, Is.False, "Read-only queries should succeed");
        Assert.That(result.HasData, Is.True, "Data should be returned from read-only queries");
        Assert.That(result.SuccessfulQuery?.Name, Is.EqualTo("SafeSelect"), "First query should succeed");
        Assert.That(result.Data!.Rows.Count, Is.EqualTo(1), "Should return one row with count");
    }

    [Test]
    public async Task SecurityIntegration_DataIntegrityPreserved_AfterSecureExecution()
    {
        // Arrange
        var connectionString = GetDatabaseConnectionString("TestDatabase2");
        
        // Get initial user count
        int initialUserCount;
        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT COUNT(*) FROM Users", connection);
            initialUserCount = (int)await command.ExecuteScalarAsync();
        }

        // Try to execute destructive queries through secure execution
        var destructiveQueries = new List<QueryDefinition>
        {
            new() { Name = "TryToDelete", Query = "DELETE FROM Users WHERE ID = 1" },
            new() { Name = "TryToInsert", Query = "INSERT INTO Users (UserName, Email) VALUES ('malicious', 'bad@evil.com')" }
        };

        // Act - Execute destructive queries (should fail)
        var result = await _service.ExecuteQueriesAsync("TestServer", "TestDatabase2", connectionString, destructiveQueries, 30);

        // Verify data integrity is preserved
        int finalUserCount;
        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT COUNT(*) FROM Users", connection);
            finalUserCount = (int)await command.ExecuteScalarAsync();
        }

        // Assert
        Assert.That(result.HasError, Is.True, "Destructive operations should be blocked");
        Assert.That(finalUserCount, Is.EqualTo(initialUserCount), "User count should remain unchanged after blocked destructive operations");
    }
}