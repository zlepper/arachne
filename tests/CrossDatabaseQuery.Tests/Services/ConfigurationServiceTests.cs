using Microsoft.Extensions.Configuration;
using CrossDatabaseQuery.Services;
using CrossDatabaseQuery.Models;

namespace CrossDatabaseQuery.Tests.Services;

[TestFixture]
public class ConfigurationServiceTests
{
    private IConfigurationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var configData = new Dictionary<string, string?>
        {
            ["SqlServerConfiguration:Servers:0:Name"] = "TestServer",
            ["SqlServerConfiguration:Servers:0:MasterConnectionString"] = "Server=test;Database=master;",
            ["SqlServerConfiguration:Servers:0:Description"] = "Test Server",
            ["SqlServerConfiguration:Queries:0:Name"] = "TestQuery",
            ["SqlServerConfiguration:Queries:0:Description"] = "Test Query Description",
            ["SqlServerConfiguration:Queries:0:Query"] = "SELECT 1",
            ["SqlServerConfiguration:Queries:0:SchemaVersion"] = "1.0",
            ["SqlServerConfiguration:QueryTimeout"] = "45",
            ["SqlServerConfiguration:ConnectionTimeout"] = "20",
            ["SqlServerConfiguration:ExcludeSystemDatabases"] = "false",
            ["SqlServerConfiguration:StopOnFirstSuccessfulQuery"] = "false",
            ["OutputConfiguration:ShowEmptyResults"] = "true",
            ["OutputConfiguration:IncludeTimestamp"] = "false",
            ["OutputConfiguration:MaxRowsPerDatabase"] = "50"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _service = new ConfigurationService(configuration);
    }

    [Test]
    public void GetSqlServerConfiguration_ValidConfiguration_ReturnsCorrectValues()
    {
        // Act
        var config = _service.GetSqlServerConfiguration();

        // Assert
        Assert.That(config.Servers.Count, Is.EqualTo(1));
        Assert.That(config.Servers[0].Name, Is.EqualTo("TestServer"));
        Assert.That(config.Servers[0].MasterConnectionString, Is.EqualTo("Server=test;Database=master;"));
        Assert.That(config.Servers[0].Description, Is.EqualTo("Test Server"));

        Assert.That(config.Queries.Count, Is.EqualTo(1));
        Assert.That(config.Queries[0].Name, Is.EqualTo("TestQuery"));
        Assert.That(config.Queries[0].Description, Is.EqualTo("Test Query Description"));
        Assert.That(config.Queries[0].Query, Is.EqualTo("SELECT 1"));
        Assert.That(config.Queries[0].SchemaVersion, Is.EqualTo("1.0"));

        Assert.That(config.QueryTimeout, Is.EqualTo(45));
        Assert.That(config.ConnectionTimeout, Is.EqualTo(20));
        Assert.That(config.ExcludeSystemDatabases, Is.False);
        Assert.That(config.StopOnFirstSuccessfulQuery, Is.False);
    }

    [Test]
    public void GetOutputConfiguration_ValidConfiguration_ReturnsCorrectValues()
    {
        // Act
        var config = _service.GetOutputConfiguration();

        // Assert
        Assert.That(config.ShowEmptyResults, Is.True);
        Assert.That(config.IncludeTimestamp, Is.False);
        Assert.That(config.MaxRowsPerDatabase, Is.EqualTo(50));
    }

    [Test]
    public void GetSqlServerConfiguration_NoServers_ThrowsException()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SqlServerConfiguration:Servers"] = "",
                ["SqlServerConfiguration:Queries:0:Name"] = "TestQuery"
            })
            .Build();
        
        var service = new ConfigurationService(emptyConfig);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => service.GetSqlServerConfiguration());
        Assert.That(ex.Message, Does.Contain("No SQL Server configurations found"));
    }

    [Test]
    public void GetSqlServerConfiguration_NoQueries_ThrowsException()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SqlServerConfiguration:Servers:0:Name"] = "TestServer",
                ["SqlServerConfiguration:Queries"] = ""
            })
            .Build();
        
        var service = new ConfigurationService(emptyConfig);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => service.GetSqlServerConfiguration());
        Assert.That(ex.Message, Does.Contain("No query configurations found"));
    }

    [Test]
    public void GetOutputConfiguration_DefaultValues_ReturnsDefaults()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        
        var service = new ConfigurationService(emptyConfig);

        // Act
        var config = service.GetOutputConfiguration();

        // Assert - Test a few default values
        Assert.That(config.ShowEmptyResults, Is.False);
        Assert.That(config.IncludeTimestamp, Is.True);
        Assert.That(config.MaxRowsPerDatabase, Is.EqualTo(100));
        Assert.That(config.ColumnPadding, Is.EqualTo(2));
        Assert.That(config.NullDisplayValue, Is.EqualTo("<NULL>"));
        Assert.That(config.DateTimeFormat, Is.EqualTo("yyyy-MM-dd HH:mm:ss"));
    }
}