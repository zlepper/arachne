
namespace Arachne.Tests.Services;

[TestFixture]
public class MarkdownFormatterTests
{
    private IMarkdownFormatter _formatter = null!;
    private OutputConfiguration _defaultConfig = null!;

    [SetUp]
    public void SetUp()
    {
        _formatter = new MarkdownFormatter();
        _defaultConfig = new OutputConfiguration();
    }

    [Test]
    public void FormatDataTableAsMarkdown_SimpleData_ReturnsMarkdownTable()
    {
        // Arrange
        var dataTable = new DataTable();
        dataTable.Columns.Add("ID", typeof(int));
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("Active", typeof(bool));

        dataTable.Rows.Add(1, "John Doe", true);
        dataTable.Rows.Add(2, "Jane Smith", false);

        // Act
        var result = _formatter.FormatDataTableAsMarkdown(dataTable, _defaultConfig);

        // Assert
        var expectedOutput = @"| ID | Name | Active |
|---|---|---|
| 1 | John Doe | True |
| 2 | Jane Smith | False |

";
        Assert.That(result, Is.EqualTo(expectedOutput));
    }

    [Test]
    public void FormatDataTableAsMarkdown_EmptyTable_ReturnsNoDataMessage()
    {
        // Arrange
        var dataTable = new DataTable();
        dataTable.Columns.Add("ID", typeof(int));

        // Act
        var result = _formatter.FormatDataTableAsMarkdown(dataTable, _defaultConfig);

        // Assert
        Assert.That(result, Is.EqualTo("*No data returned.*\n"));
    }

    [Test]
    public void FormatDataTableAsMarkdown_WithNullValues_DisplaysNullPlaceholder()
    {
        // Arrange
        var dataTable = new DataTable();
        dataTable.Columns.Add("ID", typeof(int));
        dataTable.Columns.Add("Name", typeof(string));

        dataTable.Rows.Add(1, "John");
        dataTable.Rows.Add(2, DBNull.Value);

        // Act
        var result = _formatter.FormatDataTableAsMarkdown(dataTable, _defaultConfig);

        // Assert
        Assert.That(result, Does.Contain("John"));
        Assert.That(result, Does.Contain("<NULL>"));
    }

    [Test]
    public void FormatDataTableAsMarkdown_WithCustomNullDisplay_UsesCustomValue()
    {
        // Arrange
        var config = new OutputConfiguration { NullDisplayValue = "N/A" };
        var dataTable = new DataTable();
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Rows.Add(DBNull.Value);

        // Act
        var result = _formatter.FormatDataTableAsMarkdown(dataTable, config);

        // Assert
        Assert.That(result, Does.Contain("N/A"));
        Assert.That(result, Does.Not.Contain("<NULL>"));
    }

    [Test]
    public void FormatDataTableAsMarkdown_ExceedsMaxRows_ShowsTruncationMessage()
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
        var result = _formatter.FormatDataTableAsMarkdown(dataTable, config);

        // Assert
        Assert.That(result, Does.Contain("*... and 3 more rows*"));
    }

    [Test]
    public void FormatDataTableAsMarkdown_WithSpecialCharacters_EscapesMarkdown()
    {
        // Arrange
        var dataTable = new DataTable();
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("Description", typeof(string));

        dataTable.Rows.Add("Test*Bold*", "Contains | pipe");
        dataTable.Rows.Add("[Link](url)", "Has # hash");

        // Act
        var result = _formatter.FormatDataTableAsMarkdown(dataTable, _defaultConfig);

        // Assert
        Assert.That(result, Does.Contain("Test\\*Bold\\*"));
        Assert.That(result, Does.Contain("Contains \\| pipe"));
        Assert.That(result, Does.Contain("\\[Link\\]\\(url\\)"));
        Assert.That(result, Does.Contain("Has \\# hash"));
    }

    [Test]
    public async Task GenerateMarkdownReportAsync_MultipleServersAndDatabases_GeneratesCompleteReport()
    {
        // Arrange
        var results = new List<QueryResult>
        {
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB1",
                Data = CreateSampleDataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "Query1", Query = "SELECT 1" },
                HasError = false,
                ExecutionTime = TimeSpan.FromSeconds(1.5)
            },
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB2",
                Data = new DataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "Query1", Query = "SELECT 1" },
                HasError = false,
                ExecutionTime = TimeSpan.FromSeconds(0.8)
            },
            new()
            {
                ServerName = "Server2",
                DatabaseName = "DB1",
                HasError = true,
                ErrorMessage = "Connection failed",
                ExecutionTime = TimeSpan.FromSeconds(0.1)
            }
        };

        // Act
        var configWithEmptyResults = new OutputConfiguration { ShowEmptyResults = true };
        var result = await _formatter.GenerateMarkdownReportAsync(results, configWithEmptyResults);

        // Assert
        Assert.That(result, Does.Contain("# Cross-Database Query Results"));
        Assert.That(result, Does.Contain("## Table of Contents"));
        Assert.That(result, Does.Contain("## Summary"));
        Assert.That(result, Does.Contain("## Results by Server"));
        Assert.That(result, Does.Contain("## Detailed Statistics"));
        Assert.That(result, Does.Contain("### Server1"));
        Assert.That(result, Does.Contain("### Server2"));
        Assert.That(result, Does.Contain("#### DB1"));
        Assert.That(result, Does.Contain("#### DB2"));
        Assert.That(result, Does.Contain("❌ Error"));
        Assert.That(result, Does.Contain("✅ Success"));
        Assert.That(result, Does.Contain("Connection failed"));
    }

    [Test]
    public async Task GenerateMarkdownReportAsync_IncludesTimestamp_WhenConfigured()
    {
        // Arrange
        var results = new List<QueryResult>
        {
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB1",
                Data = CreateSampleDataTable(),
                HasError = false,
                ExecutionTime = TimeSpan.FromSeconds(1.0)
            }
        };

        var config = new OutputConfiguration 
        { 
            IncludeTimestamp = true,
            DateTimeFormat = "yyyy-MM-dd"
        };

        // Act
        var result = await _formatter.GenerateMarkdownReportAsync(results, config);

        // Assert
        Assert.That(result, Does.Contain("**Executed:**"));
        Assert.That(result, Does.Contain(DateTime.Now.ToString("yyyy-MM-dd")));
    }

    [Test]
    public async Task GenerateMarkdownReportAsync_WithFailedQueries_ShowsFailures()
    {
        // Arrange
        var results = new List<QueryResult>
        {
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB1",
                Data = CreateSampleDataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "QueryV2", Query = "SELECT 2" },
                FailedQueryNames = new List<string> { "QueryV3 (Invalid object 'NewTable')" },
                HasError = false,
                ExecutionTime = TimeSpan.FromSeconds(2.1)
            }
        };

        // Act
        var result = await _formatter.GenerateMarkdownReportAsync(results, _defaultConfig);

        // Assert
        Assert.That(result, Does.Contain("**Failed Queries:** QueryV3 \\(Invalid object 'NewTable'\\)"));
    }

    [Test]
    public async Task GenerateMarkdownReportAsync_QueryVersionUsage_ShowsStatistics()
    {
        // Arrange
        var results = new List<QueryResult>
        {
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB1",
                Data = CreateSampleDataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "QueryV1", Query = "SELECT 1" },
                HasError = false,
                ExecutionTime = TimeSpan.FromSeconds(1.0)
            },
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB2",
                Data = CreateSampleDataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "QueryV2", Query = "SELECT 2" },
                HasError = false,
                ExecutionTime = TimeSpan.FromSeconds(1.5)
            },
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB3",
                Data = CreateSampleDataTable(),
                SuccessfulQuery = new QueryDefinition { Name = "QueryV1", Query = "SELECT 1" },
                HasError = false,
                ExecutionTime = TimeSpan.FromSeconds(0.8)
            }
        };

        // Act
        var result = await _formatter.GenerateMarkdownReportAsync(results, _defaultConfig);

        // Assert
        Assert.That(result, Does.Contain("### Query Version Usage"));
        Assert.That(result, Does.Contain("| QueryV1 | 2 |"));
        Assert.That(result, Does.Contain("| QueryV2 | 1 |"));
    }

    [Test]
    public async Task GenerateMarkdownReportAsync_PerformanceAnalysis_IncludesTimingStatistics()
    {
        // Arrange
        var results = new List<QueryResult>
        {
            new()
            {
                ServerName = "Server1",
                DatabaseName = "FastDB",
                Data = CreateSampleDataTable(),
                HasError = false,
                ExecutionTime = TimeSpan.FromSeconds(0.5)
            },
            new()
            {
                ServerName = "Server1",
                DatabaseName = "SlowDB",
                Data = CreateSampleDataTable(),
                HasError = false,
                ExecutionTime = TimeSpan.FromSeconds(5.0)
            },
            new()
            {
                ServerName = "Server1",
                DatabaseName = "MediumDB",
                Data = CreateSampleDataTable(),
                HasError = false,
                ExecutionTime = TimeSpan.FromSeconds(2.0)
            }
        };

        // Act
        var result = await _formatter.GenerateMarkdownReportAsync(results, _defaultConfig);

        // Assert
        Assert.That(result, Does.Contain("### Performance Analysis"));
        Assert.That(result, Does.Contain("**Fastest query**"));
        Assert.That(result, Does.Contain("**Slowest query**"));
        Assert.That(result, Does.Contain("**Median time**"));
        Assert.That(result, Does.Contain("0,50s").Or.Contain("0.50s")); // Fastest (handle locale)
        Assert.That(result, Does.Contain("5,00s").Or.Contain("5.00s")); // Slowest (handle locale)
    }

    [Test]
    public async Task GenerateMarkdownReportAsync_ErrorAnalysis_GroupsErrorsByType()
    {
        // Arrange
        var results = new List<QueryResult>
        {
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB1",
                HasError = true,
                ErrorMessage = "Connection timeout",
                ExecutionTime = TimeSpan.FromSeconds(0.1)
            },
            new()
            {
                ServerName = "Server1",
                DatabaseName = "DB2",
                HasError = true,
                ErrorMessage = "Connection timeout",
                ExecutionTime = TimeSpan.FromSeconds(0.1)
            },
            new()
            {
                ServerName = "Server2",
                DatabaseName = "DB1",
                HasError = true,
                ErrorMessage = "Permission denied",
                ExecutionTime = TimeSpan.FromSeconds(0.05)
            }
        };

        // Act
        var result = await _formatter.GenerateMarkdownReportAsync(results, _defaultConfig);

        // Assert
        Assert.That(result, Does.Contain("### Error Analysis"));
        Assert.That(result, Does.Contain("Connection timeout"));
        Assert.That(result, Does.Contain("Permission denied"));
        Assert.That(result, Does.Contain("| 2 |")); // Connection timeout count
        Assert.That(result, Does.Contain("| 1 |")); // Permission denied count
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