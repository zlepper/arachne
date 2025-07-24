# Arachne

> Named after the legendary weaver from Greek mythology, Arachne was a master craftsperson who could weave intricate patterns across multiple threads. Like her mythical namesake, this application weaves intelligent queries across multiple database schemas with graceful fallback support.

A C# console application that executes SQL queries across multiple databases on different SQL Server instances, with intelligent fallback query support for different database schema versions.

## Features

- **Multi-Server Support**: Connect to multiple SQL Server instances
- **Database Discovery**: Automatically discover user databases on each server
- **Fallback Query Support**: Try multiple query versions to handle different database schemas
- **Smart Error Handling**: Distinguish between schema-related errors and other failures
- **Rich Output Formatting**: Display results in formatted tables with query version tracking
- **Markdown Reports**: Generate comprehensive markdown reports with detailed analytics
- **Comprehensive Testing**: Full test coverage using NUnit and Testcontainers

## Project Structure

```
├── src/
│   └── Arachne/
│       ├── Models/              # Data models
│       ├── Services/            # Business logic services
│       ├── Extensions/          # Service registration extensions
│       ├── Program.cs           # Main application entry point
│       └── AppSettings.json     # Configuration file
├── tests/
│   └── Arachne.Tests/
│       ├── Services/            # Unit tests for services
│       ├── Integration/         # Integration tests with real SQL Server
│       └── TestBase.cs          # Shared test infrastructure
├── Arachne.sln                  # Solution file
└── task.md                      # Original project plan
```

## Configuration

### Base Configuration

The `AppSettings.json` file contains safe, example configuration that can be committed to git:

### Development Configuration (Gitignored)

For production credentials, create `AppSettings.Development.json` with your actual connection strings. This file is gitignored to prevent accidental commits of sensitive data:

```json
{
  "SqlServerConfiguration": {
    "Servers": [
      {
        "Name": "Production-Server-1",
        "MasterConnectionString": "Server=prod-sql-01.company.com;Database=master;User Id=query_user;Password=YourActualPassword;TrustServerCertificate=true;",
        "Description": "Production SQL Server - Customer Environment 1"
      }
    ]
  }
}
```

The application will load `AppSettings.json` first, then override with values from `AppSettings.Development.json` if it exists.

### Example Configuration

Edit your development configuration file to match your environment:

```json
{
  "SqlServerConfiguration": {
    "Servers": [
      {
        "Name": "Production-Server",
        "MasterConnectionString": "Server=prod-server;Database=master;Trusted_Connection=true;",
        "Description": "Production SQL Server"
      }
    ],
    "Queries": [
      {
        "Name": "FeatureUsage_v3",
        "Description": "Latest schema with user details",
        "Query": "SELECT u.UserName, f.FeatureName, f.LastUsed, f.UsageCount FROM FeatureUsage f JOIN Users u ON f.UserID = u.ID WHERE f.LastUsed > DATEADD(day, -30, GETDATE())",
        "SchemaVersion": "3.0+"
      },
      {
        "Name": "FeatureUsage_v2", 
        "Description": "Schema without user details join",
        "Query": "SELECT UserID, FeatureName, LastUsed, UsageCount FROM FeatureUsage WHERE LastUsed > DATEADD(day, -30, GETDATE())",
        "SchemaVersion": "2.0-2.9"
      }
    ],
    "QueryTimeout": 30,
    "ConnectionTimeout": 15,
    "ExcludeSystemDatabases": true,
    "StopOnFirstSuccessfulQuery": true
  },
  "OutputConfiguration": {
    "ShowEmptyResults": false,
    "IncludeTimestamp": true,
    "ShowQueryVersion": true,
    "MaxRowsPerDatabase": 100,
    "GenerateMarkdownReport": false,
    "MarkdownOutputPath": "query-results.md",
    "MarkdownIncludeFailedQueries": true
  }
}
```

### Output Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `ShowEmptyResults` | `false` | Show databases that return no data |
| `IncludeTimestamp` | `true` | Include execution timestamp in output |
| `ShowQueryVersion` | `true` | Show which query version was used |
| `MaxRowsPerDatabase` | `100` | Maximum rows to display per database |
| `NullDisplayValue` | `"<NULL>"` | How to display null values |
| `DateTimeFormat` | `"yyyy-MM-dd HH:mm:ss"` | DateTime format string |
| `GenerateMarkdownReport` | `false` | **Enable markdown report generation** |
| `MarkdownOutputPath` | `"query-results.md"` | **Path for markdown report file** |
| `MarkdownIncludeFailedQueries` | `true` | **Include failed query information in report** |

## Markdown Reports

Arachne can generate comprehensive markdown reports that include all query results plus detailed analytics. These reports are perfect for documentation, sharing with teams, or including in pull requests.

### Enabling Markdown Reports

Set `GenerateMarkdownReport` to `true` in your configuration:

```json
{
  "OutputConfiguration": {
    "GenerateMarkdownReport": true,
    "MarkdownOutputPath": "database-analysis-report.md"
  }
}
```

### Report Structure

The markdown report includes:

- **Table of Contents** with navigation links
- **Executive Summary** with key metrics:
  - Servers processed (successful/failed)
  - Total databases discovered
  - Databases with results vs errors
  - Total rows returned
  - Execution time statistics
- **Query Version Usage** showing which fallback queries were used
- **Results by Server** with detailed database breakdowns
- **Detailed Statistics** including:
  - Server-level performance metrics
  - Error analysis grouped by type
  - Performance analysis (fastest/slowest queries)
  - Query execution timing distribution

### Sample Report Content

```markdown
# Cross-Database Query Results

**Executed:** 2024-07-24 10:30:15

## Summary

| Metric | Value |
|--------|-------|
| **Servers processed** | 2/2 successful |
| **Total databases** | 15 |
| **Databases with results** | 12 |
| **Databases with errors** | 3 |
| **Total execution time** | 45.2s |
| **Average execution time** | 3.01s |

### Query Version Usage

| Query Version | Databases |
|---------------|-----------|
| FeatureUsage_v3 | 8 |
| FeatureUsage_v2 | 4 |

## Results by Server

### Production-Server-1

#### CustomerDB_2024 ✅ Success
- **Rows:** 156
- **Query:** FeatureUsage_v3
- **Execution Time:** 2.4s

[Data tables and detailed results...]
```

## Usage

### Running the Application

```bash
dotnet run --project src/Arachne/Arachne.csproj
```

### Building the Solution

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

The tests use Testcontainers to spin up real SQL Server instances for integration testing.

## Key Components

### Services

- **ConfigurationService**: Loads and validates configuration from AppSettings.json
- **DatabaseDiscoveryService**: Discovers user databases on SQL Server instances
- **FallbackQueryExecutionService**: Executes queries with intelligent fallback handling
- **TableFormatter**: Formats query results into readable console tables
- **MarkdownFormatter**: Generates comprehensive markdown reports with analytics

### Models

- **SqlServerConfiguration**: Server and query configuration
- **QueryDefinition**: Individual query definitions with schema version info
- **QueryResult**: Query execution results with timing and error information
- **DatabaseInfo**: Database metadata from discovery process

### Error Handling

The application intelligently classifies SQL errors:

- **Schema-related errors**: Try next fallback query
- **Permission errors**: Skip database
- **Timeout errors**: Skip database
- **Connection errors**: Skip database

## Sample Output

```
========================================
Cross-Database Query Results
========================================
Executed: 2024-07-24 10:30:15

┌─────────────────────────────────────────┐
│ Production-Server (prod-server)         │
└─────────────────────────────────────────┘

Database: CustomerDB_2024 [Query: FeatureUsage_v3] (2 rows)
┌──────────┬─────────────────┬─────────────────────┬────────────┐
│ UserName │ FeatureName     │ LastUsed            │ UsageCount │
├──────────┼─────────────────┼─────────────────────┼────────────┤
│ john.doe │ ReportBuilder   │ 2024-07-23 14:22:10 │ 47         │
│ jane.smith│ DataExport      │ 2024-07-24 09:15:33 │ 12         │
└──────────┴─────────────────┴─────────────────────┴────────────┘

Database: LegacyDB [Query: FeatureUsage_v2] (1 row)
⚠️  Query failed: FeatureUsage_v3 (Invalid object 'Users')
┌────────┬─────────────────┬─────────────────────┬────────────┐
│ UserID │ FeatureName     │ LastUsed            │ UsageCount │
├────────┼─────────────────┼─────────────────────┼────────────┤
│ 2001   │ LegacyReports   │ 2024-07-20 11:30:15 │ 5          │
└────────┴─────────────────┴─────────────────────┴────────────┘

========================================
Summary:
- Servers processed: 1/1 successful
- Databases discovered: 2
- Databases with results: 2
- Query version usage:
  • FeatureUsage_v3: 1 database(s)
  • FeatureUsage_v2: 1 database(s)
- Total execution time: 12.4s
========================================
```

## Dependencies

- **.NET 9.0**: Modern .NET framework
- **Microsoft.Data.SqlClient**: SQL Server connectivity
- **Microsoft.Extensions.*** packages: Dependency injection and configuration
- **NUnit**: Testing framework
- **Testcontainers**: Integration testing with real SQL Server containers
- **Spectre.Console**: Professional console output with rich formatting and progress tracking

## Architecture

The application follows clean architecture principles:

- **Dependency Injection**: All services are registered in DI container
- **Interface Segregation**: Each service implements a focused interface  
- **Single Responsibility**: Each service has a single, well-defined purpose
- **Testability**: All components are easily testable with real database scenarios

## Testing Strategy

- **Unit Tests**: Test individual service components
- **Integration Tests**: Test the complete workflow with real SQL Server containers
- **No Mocking**: All tests run against actual database instances for maximum confidence
- **Schema Testing**: Tests validate fallback query behavior with different database schemas