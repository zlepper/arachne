using Testcontainers.MsSql;

namespace Arachne.Tests;

public abstract class TestBase
{
    protected static MsSqlContainer? SqlContainer;
    protected static string? ConnectionString;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        SqlContainer = new MsSqlBuilder()
            .WithPassword("TestPassword123!")
            .WithPortBinding(0, true)
            .Build();

        await SqlContainer.StartAsync();
        ConnectionString = SqlContainer.GetConnectionString();
        
        await SetupTestData();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (SqlContainer != null)
        {
            await SqlContainer.DisposeAsync();
        }
    }

    private async Task SetupTestData()
    {
        if (ConnectionString == null) return;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Create test databases
        var testDatabases = new[] { "TestDatabase1", "TestDatabase2", "LegacyDatabase" };
        
        foreach (var dbName in testDatabases)
        {
            await using var command = new SqlCommand($"CREATE DATABASE [{dbName}]", connection);
            await command.ExecuteNonQueryAsync();
        }

        // Create test tables in TestDatabase1 (modern schema)
        await SetupModernSchema("TestDatabase1");
        
        // Create test tables in TestDatabase2 (modern schema with data)  
        await SetupModernSchemaWithData("TestDatabase2");
        
        // Create test tables in LegacyDatabase (old schema)
        await SetupLegacySchema("LegacyDatabase");
    }

    private async Task SetupModernSchema(string databaseName)
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder(ConnectionString!);
        connectionStringBuilder.InitialCatalog = databaseName;
        
        await using var connection = new SqlConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync();

        var createTables = """
            CREATE TABLE Users (
                ID int PRIMARY KEY IDENTITY(1,1),
                UserName nvarchar(100) NOT NULL,
                Email nvarchar(255),
                CreatedDate datetime2 DEFAULT GETDATE()
            );

            CREATE TABLE FeatureUsage (
                ID int PRIMARY KEY IDENTITY(1,1),
                UserID int FOREIGN KEY REFERENCES Users(ID),
                FeatureName nvarchar(100) NOT NULL,
                LastUsed datetime2 DEFAULT GETDATE(),
                UsageCount int DEFAULT 1
            );
            """;

        await using var command = new SqlCommand(createTables, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SetupModernSchemaWithData(string databaseName)
    {
        await SetupModernSchema(databaseName);
        
        var connectionStringBuilder = new SqlConnectionStringBuilder(ConnectionString!);
        connectionStringBuilder.InitialCatalog = databaseName;
        
        await using var connection = new SqlConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync();

        var insertData = """
            INSERT INTO Users (UserName, Email) VALUES 
                ('john.doe', 'john@example.com'),
                ('jane.smith', 'jane@example.com');

            INSERT INTO FeatureUsage (UserID, FeatureName, LastUsed, UsageCount) VALUES 
                (1, 'ReportBuilder', DATEADD(day, -5, GETDATE()), 47),
                (2, 'DataExport', DATEADD(day, -2, GETDATE()), 12),
                (1, 'Dashboard', DATEADD(day, -1, GETDATE()), 23);
            """;

        await using var command = new SqlCommand(insertData, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SetupLegacySchema(string databaseName)
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder(ConnectionString!);
        connectionStringBuilder.InitialCatalog = databaseName;
        
        await using var connection = new SqlConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync();

        var createTables = """
            CREATE TABLE FeatureLog (
                ID int PRIMARY KEY IDENTITY(1,1),
                FeatureName nvarchar(100) NOT NULL,
                LogDate datetime2 DEFAULT GETDATE(),
                UserInfo nvarchar(255)
            );

            INSERT INTO FeatureLog (FeatureName, LogDate, UserInfo) VALUES 
                ('LegacyReports', DATEADD(day, -3, GETDATE()), 'legacy_user_1'),
                ('LegacyReports', DATEADD(day, -2, GETDATE()), 'legacy_user_2'),
                ('DataImport', DATEADD(day, -1, GETDATE()), 'legacy_user_1');
            """;

        await using var command = new SqlCommand(createTables, connection);
        await command.ExecuteNonQueryAsync();
    }

    protected static string GetMasterConnectionString()
    {
        return ConnectionString ?? throw new InvalidOperationException("Test container not initialized");
    }

    protected static string GetDatabaseConnectionString(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString!);
        builder.InitialCatalog = databaseName;
        return builder.ConnectionString;
    }
}