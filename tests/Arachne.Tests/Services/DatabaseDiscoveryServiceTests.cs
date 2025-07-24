using Arachne.Services;

namespace Arachne.Tests.Services;

[TestFixture]
public class DatabaseDiscoveryServiceTests : TestBase
{
    private IDatabaseDiscoveryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new DatabaseDiscoveryService();
    }

    [Test]
    public async Task DiscoverDatabasesAsync_WithExcludeSystemDatabases_ReturnsUserDatabasesOnly()
    {
        // Arrange
        var masterConnectionString = GetMasterConnectionString();

        // Act
        var databases = await _service.DiscoverDatabasesAsync(masterConnectionString, excludeSystemDatabases: true);

        // Assert
        Assert.That(databases.Count, Is.EqualTo(3));
        Assert.That(databases.Any(d => d.Name == "TestDatabase1"), Is.True);
        Assert.That(databases.Any(d => d.Name == "TestDatabase2"), Is.True);
        Assert.That(databases.Any(d => d.Name == "LegacyDatabase"), Is.True);
        
        // Ensure no system databases are included
        Assert.That(databases.Any(d => d.Name == "master"), Is.False);
        Assert.That(databases.Any(d => d.Name == "tempdb"), Is.False);
        Assert.That(databases.Any(d => d.Name == "msdb"), Is.False);
        Assert.That(databases.Any(d => d.Name == "model"), Is.False);
    }

    [Test]
    public async Task DiscoverDatabasesAsync_WithIncludeSystemDatabases_ReturnsAllDatabases()
    {
        // Arrange
        var masterConnectionString = GetMasterConnectionString();

        // Act
        var databases = await _service.DiscoverDatabasesAsync(masterConnectionString, excludeSystemDatabases: false);

        // Assert
        Assert.That(databases.Count, Is.GreaterThanOrEqualTo(7)); // At least 3 test + 4 system databases
        
        // Ensure system databases are included
        Assert.That(databases.Any(d => d.Name == "master"), Is.True);
        Assert.That(databases.Any(d => d.Name == "tempdb"), Is.True);
        Assert.That(databases.Any(d => d.Name == "msdb"), Is.True);
        Assert.That(databases.Any(d => d.Name == "model"), Is.True);
    }

    [Test]
    public async Task DiscoverDatabasesAsync_ReturnsCorrectDatabaseProperties()
    {
        // Arrange
        var masterConnectionString = GetMasterConnectionString();

        // Act
        var databases = await _service.DiscoverDatabasesAsync(masterConnectionString, excludeSystemDatabases: true);

        // Assert
        var testDb = databases.First(d => d.Name == "TestDatabase1");
        Assert.That(testDb.DatabaseId, Is.GreaterThan(4));
        Assert.That(testDb.Status, Is.EqualTo("ONLINE"));
        Assert.That(testDb.IsReadOnly, Is.False);
    }

    [Test]
    public void DiscoverDatabasesAsync_WithInvalidConnectionString_ThrowsException()
    {
        // Arrange
        var invalidConnectionString = "Server=nonexistent;Database=master;";

        // Act & Assert  
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.DiscoverDatabasesAsync(invalidConnectionString));
    }
}