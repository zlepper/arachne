
namespace Arachne.Tests.Services;

[TestFixture]
[Category("Integration")]
public class SecureQueryExecutionServiceTests : TestBase
{
    private SecureQueryExecutionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new SecureQueryExecutionService();
    }

    [Test]
    public async Task StartSecureContextAsync_ShouldCreateReadOnlyContext()
    {
        // Arrange
        var connectionString = GetDatabaseConnectionString("TestDatabase2");

        // Act
        await using var context = await _service.StartSecureContextAsync(connectionString);

        // Assert
        Assert.That(context, Is.Not.Null);
        
        // Verify we can read data
        await using var command = new SqlCommand("SELECT COUNT(*) FROM Users", context.GetSecuredSqlConnection());
        var count = await command.ExecuteScalarAsync();
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task SecureContext_ShouldPreventWriteOperations()
    {
        // Arrange
        var connectionString = GetDatabaseConnectionString("TestDatabase2");

        // Act & Assert
        await using var context = await _service.StartSecureContextAsync(connectionString);
        
        // Attempt to insert data - should fail due to read-only permissions
        await using var insertCommand = new SqlCommand(
            "INSERT INTO Users (UserName, Email) VALUES ('test.user', 'test@example.com')", 
            context.GetSecuredSqlConnection());
        
        Assert.ThrowsAsync<SqlException>(async () => 
            await insertCommand.ExecuteNonQueryAsync());
    }

    [Test]
    public async Task SecureContext_ShouldPreventUpdateOperations()
    {
        // Arrange
        var connectionString = GetDatabaseConnectionString("TestDatabase2");

        // Act & Assert
        await using var context = await _service.StartSecureContextAsync(connectionString);
        
        // Attempt to update data - should fail due to read-only permissions
        await using var updateCommand = new SqlCommand(
            "UPDATE Users SET Email = 'updated@example.com' WHERE ID = 1", 
            context.GetSecuredSqlConnection());
        
        Assert.ThrowsAsync<SqlException>(async () => 
            await updateCommand.ExecuteNonQueryAsync());
    }

    [Test]
    public async Task SecureContext_ShouldPreventDeleteOperations()
    {
        // Arrange
        var connectionString = GetDatabaseConnectionString("TestDatabase2");

        // Act & Assert
        await using var context = await _service.StartSecureContextAsync(connectionString);
        
        // Attempt to delete data - should fail due to read-only permissions
        await using var deleteCommand = new SqlCommand(
            "DELETE FROM Users WHERE ID = 1", 
            context.GetSecuredSqlConnection());
        
        Assert.ThrowsAsync<SqlException>(async () => 
            await deleteCommand.ExecuteNonQueryAsync());
    }

    [Test]
    public async Task SecureContext_ShouldPreventSchemaChanges()
    {
        // Arrange
        var connectionString = GetDatabaseConnectionString("TestDatabase2");

        // Act & Assert
        await using var context = await _service.StartSecureContextAsync(connectionString);
        
        // Attempt to create table - should fail due to read-only permissions
        await using var createCommand = new SqlCommand(
            "CREATE TABLE TestTable (ID int PRIMARY KEY)", 
            context.GetSecuredSqlConnection());
        
        Assert.ThrowsAsync<SqlException>(async () => 
            await createCommand.ExecuteNonQueryAsync());
    }

    [Test]
    public async Task SecureContext_DisposalShouldCleanupRole()
    {
        // Arrange
        var connectionString = GetDatabaseConnectionString("TestDatabase2");
        var masterConnectionString = GetMasterConnectionString();

        // Act
        string? roleName = null;
        
        // Create and dispose context
        {
            await using var context = await _service.StartSecureContextAsync(connectionString);
            
            // Extract role name by querying system tables
            await using var roleQuery = new SqlCommand(
                "SELECT name FROM sys.database_principals WHERE type = 'A' AND name LIKE 'TempReadOnly%'", 
                context.GetSecuredSqlConnection());
            
            roleName = (string?)await roleQuery.ExecuteScalarAsync();
            Assert.That(roleName, Is.Not.Null);
        } // Context disposed here
        
        // Verify role is cleaned up
        await using var masterConnection = new SqlConnection(masterConnectionString);
        await masterConnection.OpenAsync();
        
        var dbConnectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = dbConnectionStringBuilder.InitialCatalog;
        
        await using var checkCommand = new SqlCommand($"""
            USE [{databaseName}];
            SELECT COUNT(*) FROM sys.database_principals 
            WHERE type = 'A' AND name = '{roleName}'
            """, masterConnection);
        
        var roleCount = (int)(await checkCommand.ExecuteScalarAsync() ?? 0);
        Assert.That(roleCount, Is.EqualTo(0), "Temporary role should be cleaned up after disposal");
    }

    [Test]
    public async Task SecureContext_ShouldAllowMultipleQueries()
    {
        // Arrange
        var connectionString = GetDatabaseConnectionString("TestDatabase2");

        // Act
        await using var context = await _service.StartSecureContextAsync(connectionString);
        
        // Execute multiple queries using the same secure context
        var queries = new[]
        {
            "SELECT COUNT(*) FROM Users",
            "SELECT COUNT(*) FROM FeatureUsage", 
            "SELECT TOP 1 UserName FROM Users ORDER BY ID"
        };
        
        var results = new List<object?>();
        
        foreach (var query in queries)
        {
            await using var command = new SqlCommand(query, context.GetSecuredSqlConnection());
            var result = await command.ExecuteScalarAsync();
            results.Add(result);
        }

        // Assert
        Assert.That(results[0], Is.EqualTo(2)); // User count
        Assert.That(results[1], Is.EqualTo(3)); // FeatureUsage count
        Assert.That(results[2], Is.EqualTo("john.doe")); // First username
    }

    [Test]
    public void SecureContext_AccessAfterDisposal_ShouldThrow()
    {
        // Arrange & Act
        var context = _service.StartSecureContextAsync(GetDatabaseConnectionString("TestDatabase2")).Result;
        context.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => context.GetSecuredSqlConnection());
    }
}