# Cross-Database Query Console Application

A C# console application that executes SQL queries across multiple databases on different SQL Server instances, with intelligent fallback query support for different database schema versions.

## Features

- **Multi-Server Support**: Connect to multiple SQL Server instances
- **Database Discovery**: Automatically discover user databases on each server
- **Fallback Query Support**: Try multiple query versions to handle different database schemas
- **Smart Error Handling**: Distinguish between schema-related errors and other failures
- **Rich Output Formatting**: Display results in formatted tables with query version tracking
- **Comprehensive Testing**: Full test coverage using NUnit and Testcontainers

## Project Structure

```
├── src/
│   └── CrossDatabaseQuery/
│       ├── Models/              # Data models
│       ├── Services/            # Business logic services
│       ├── Extensions/          # Service registration extensions
│       ├── Program.cs           # Main application entry point
│       └── AppSettings.json     # Configuration file
├── tests/
│   └── CrossDatabaseQuery.Tests/
│       ├── Services/            # Unit tests for services
│       ├── Integration/         # Integration tests with real SQL Server
│       └── TestBase.cs          # Shared test infrastructure
├── CrossDatabaseQuery.sln       # Solution file
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
    "MaxRowsPerDatabase": 100
  }
}
```

## Usage

### Running the Application

```bash
dotnet run --project src/CrossDatabaseQuery/CrossDatabaseQuery.csproj
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
- **TableFormatter**: Formats query results into readable tables

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
- **ConsoleTableExt**: Professional table formatting for console output

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