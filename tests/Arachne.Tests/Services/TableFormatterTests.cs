using System.Data;
using Arachne.Services;
using Arachne.Models;

namespace Arachne.Tests.Services;

[TestFixture]
public class TableFormatterTests
{
    private ITableFormatter _formatter = null!;
    private OutputConfiguration _defaultConfig = null!;

    [SetUp]
    public void SetUp()
    {
        _formatter = new TableFormatter();
        _defaultConfig = new OutputConfiguration();
    }

    [Test]
    public void FormatTable_SimpleData_ReturnsFormattedTable()
    {
        // Arrange
        var dataTable = new DataTable();
        dataTable.Columns.Add("ID", typeof(int));
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("Active", typeof(bool));

        dataTable.Rows.Add(1, "John Doe", true);
        dataTable.Rows.Add(2, "Jane Smith", false);

        // Act
        var result = _formatter.FormatTable(dataTable, _defaultConfig);

        // Assert - based on the ConsoleTableExt format from the error message
        var expectedOutput = @"+----+------------+--------+
| ID | Name       | Active |
+----+------------+--------+
| 1  | John Doe   | True   |
+----+------------+--------+
| 2  | Jane Smith | False  |
+----+------------+--------+
";
        Assert.That(result, Is.EqualTo(expectedOutput));
    }

    [Test]
    public void FormatTable_EmptyTable_ReturnsNoDataMessage()
    {
        // Arrange
        var dataTable = new DataTable();
        dataTable.Columns.Add("ID", typeof(int));

        // Act
        var result = _formatter.FormatTable(dataTable, _defaultConfig);

        // Assert
        Assert.That(result, Is.EqualTo("No data returned."));
    }

    [Test]
    public void FormatTable_WithNullValues_DisplaysNullPlaceholder()
    {
        // Arrange
        var dataTable = new DataTable();
        dataTable.Columns.Add("ID", typeof(int));
        dataTable.Columns.Add("Name", typeof(string));

        dataTable.Rows.Add(1, "John");
        dataTable.Rows.Add(2, DBNull.Value);

        // Act
        var result = _formatter.FormatTable(dataTable, _defaultConfig);

        // Assert
        Assert.That(result, Does.Contain("John"));
        Assert.That(result, Does.Contain("<NULL>"));
    }

    [Test]
    public void FormatTable_WithCustomNullDisplay_UsesCustomValue()
    {
        // Arrange
        var config = new OutputConfiguration { NullDisplayValue = "N/A" };
        var dataTable = new DataTable();
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Rows.Add(DBNull.Value);

        // Act
        var result = _formatter.FormatTable(dataTable, config);

        // Assert
        Assert.That(result, Does.Contain("N/A"));
        Assert.That(result, Does.Not.Contain("<NULL>"));
    }

    [Test]
    public void FormatTable_ExceedsMaxRows_ShowsTruncationMessage()
    {
        // Arrange
        var config = new OutputConfiguration { MaxRowsPerDatabase = 2 };
        var dataTable = new DataTable();
        dataTable.Columns.Add("ID", typeof(int));

        for (int i = 1; i <= 5; i++)
        {
            dataTable.Rows.Add(i);
        }

        // Act
        var result = _formatter.FormatTable(dataTable, config);

        // Assert
        Assert.That(result, Does.Contain("... and 3 more rows"));
    }

    [Test]
    public void FormatResults_MultipleServersAndDatabases_FormatsCorrectly()
    {
        // Arrange
        var results = new List<QueryResult>
        {
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB1",
                Data = CreateSampleDataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "Query1" },
                HasError = false
            },
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB2",
                Data = new DataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "Query1" },
                HasError = false
            },
            new()
            {
                ServerName = "Server2",
                DatabaseName = "DB1",
                HasError = true,
                ErrorMessage = "Connection failed"
            }
        };

        // Act
        var configWithEmptyResults = new OutputConfiguration { ShowEmptyResults = true };
        var result = _formatter.FormatResults(results, configWithEmptyResults);

        // Assert
        Assert.That(result, Does.Contain("Cross-Database Query Results"));
        Assert.That(result, Does.Contain("Server1"));
        Assert.That(result, Does.Contain("Server2"));
        Assert.That(result, Does.Contain("DB1"));
        Assert.That(result, Does.Contain("DB2"));
        Assert.That(result, Does.Contain("❌ Connection failed"));
        Assert.That(result, Does.Contain("Summary:"));
        Assert.That(result, Does.Contain("Servers processed:"));
        Assert.That(result, Does.Contain("Databases discovered: 3"));
    }

    [Test]
    public void FormatResults_WithFailedQueries_ShowsWarnings()
    {
        // Arrange
        var results = new List<QueryResult>
        {
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB1",
                Data = CreateSampleDataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "QueryV2" },
                FailedQueryNames = new List<string> { "QueryV3 (Invalid object 'NewTable')" },
                HasError = false
            }
        };

        // Act
        var result = _formatter.FormatResults(results, _defaultConfig);

        // Assert
        Assert.That(result, Does.Contain("⚠️  Queries failed: QueryV3"));
        Assert.That(result, Does.Contain("Invalid object 'NewTable'"));
    }

    [Test]
    public void FormatResults_QueryVersionUsage_ShowsStatistics()
    {
        // Arrange
        var results = new List<QueryResult>
        {
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB1",
                Data = CreateSampleDataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "QueryV1" },
                HasError = false
            },
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB2",
                Data = CreateSampleDataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "QueryV2" },
                HasError = false
            },
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB3",
                Data = CreateSampleDataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "QueryV1" },
                HasError = false
            }
        };

        // Act
        var result = _formatter.FormatResults(results, _defaultConfig);

        // Assert
        Assert.That(result, Does.Contain("Query version usage:"));
        Assert.That(result, Does.Contain("QueryV1: 2 database(s)"));
        Assert.That(result, Does.Contain("QueryV2: 1 database(s)"));
    }

    private static DataTable CreateSampleDataTable()
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add("ID", typeof(int));
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Rows.Add(1, "Test");
        return dataTable;
    }
}