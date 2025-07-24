# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Arachne is a C# console application that executes SQL queries across multiple databases on different SQL Server instances, with intelligent fallback query support for different database schema versions.

## Build and Test Commands

### Building
```bash
dotnet build                                    # Build entire solution
dotnet build src/Arachne/Arachne.csproj       # Build main application only
dotnet build tests/Arachne.Tests/Arachne.Tests.csproj  # Build tests only
```

### Running
```bash
dotnet run --project src/Arachne/Arachne.csproj  # Run the application
```

### Testing
```bash
dotnet test                                     # Run all tests
dotnet test --logger "console;verbosity=detailed"  # Run tests with detailed output
dotnet test tests/Arachne.Tests/Arachne.Tests.csproj  # Run specific test project
dotnet test --filter "TestCategory=Integration"    # Run integration tests only
dotnet test --filter "TestCategory=Unit"          # Run unit tests only
```

### Single Test Execution
```bash
dotnet test --filter "MethodName~YourTestMethodName"
dotnet test --filter "ClassName~YourTestClassName"
```

## Architecture Overview

### Service-Oriented Architecture
The application follows clean architecture principles with dependency injection:

- **ApplicationOrchestrator**: Main orchestration service that coordinates the entire workflow
- **ConfigurationService**: Loads and validates configuration from AppSettings.json/AppSettings.Development.json
- **DatabaseDiscoveryService**: Discovers user databases on SQL Server instances
- **FallbackQueryExecutionService**: Executes queries with intelligent fallback handling for different schema versions
- **TableFormatter**: Formats query results using Spectre.Console for rich terminal output
- **MarkdownFormatter**: Generates comprehensive markdown reports with detailed analytics and statistics

### Key Architectural Patterns
- **Fallback Query System**: Queries are tried in sequence until one succeeds, enabling support for different database schema versions
- **Parallel Execution**: Database operations run in parallel with configurable concurrency limits via `MaxConcurrentOperations`
- **Progress Tracking**: Real-time progress display using Spectre.Console live progress bars
- **Schema Error Detection**: Intelligent error classification to distinguish schema issues from other failures
- **Dual Output System**: Console output for immediate feedback, optional markdown reports for documentation and sharing

### Configuration System
- Base configuration in `AppSettings.json` (safe for git commits)
- Production credentials in `AppSettings.Development.json` (gitignored)
- Hierarchical configuration loading with Development overriding base settings

### Service Registration
All services are registered in `Extensions/ServiceCollectionExtensions.cs` using the `AddArachneServices()` extension method.

## Testing Strategy

### Test Infrastructure
- **TestBase Class**: Shared test infrastructure using Testcontainers
- **Real SQL Server**: Tests run against actual SQL Server containers (no mocking)
- **Multi-Schema Testing**: Test databases with different schemas (modern vs legacy) to validate fallback behavior
- **Integration Tests**: Full end-to-end testing in `Integration/FullApplicationTests.cs`

### Test Database Setup
TestBase automatically creates test databases with different schemas:
- `TestDatabase1`: Modern schema (Users + FeatureUsage tables)
- `TestDatabase2`: Modern schema with sample data
- `LegacyDatabase`: Legacy schema (FeatureLog table only)

### Test Categories
Use `[Category("Integration")]` and `[Category("Unit")]` attributes for test organization.

## Configuration Details

### Connection String Format
SQL Server connection strings should target the `master` database for discovery, then individual databases are accessed by modifying the `InitialCatalog` property.

### Query Definition Structure
Queries support fallback mechanisms where multiple query versions can be defined, tried in order until one succeeds:

```json
{
  "Name": "FeatureUsage_v3",
  "Query": "SELECT u.UserName, f.FeatureName FROM FeatureUsage f JOIN Users u ON f.UserID = u.ID",
  "SchemaVersion": "3.0+"
}
```

### Performance Configuration
- `MaxConcurrentOperations`: Controls global parallelism across all database operations
- `QueryTimeout`: SQL command timeout in seconds
- `ConnectionTimeout`: SQL connection timeout in seconds
- `StopOnFirstSuccessfulQuery`: Whether to stop trying additional queries after first success

## Development Notes

### Package Dependencies
- **Spectre.Console**: Used for rich console output and progress tracking (replaced ConsoleTableExt)
- **Testcontainers.MsSql**: Integration testing with real SQL Server containers
- **Microsoft.Data.SqlClient**: SQL Server connectivity
- **NUnit**: Testing framework

### Error Handling Strategy
The application classifies SQL errors to determine appropriate responses:
- Schema-related errors → Try next fallback query
- Permission/timeout/connection errors → Skip database and continue

### Output and Reporting
The application provides two output formats:

#### Console Output (Default)
Results are displayed using Spectre.Console with:
- Live progress tracking during execution
- Formatted tables showing query results
- Query version tracking to show which fallback was used
- Summary statistics including execution times and success rates

#### Markdown Reports (Optional)
When enabled via `GenerateMarkdownReport: true`, the application generates comprehensive markdown reports with:
- Table of contents with navigation links
- Executive summary with key metrics (servers, databases, execution times)
- Query version usage analysis
- Detailed results organized by server and database
- Performance analytics (fastest/slowest queries, timing distribution)
- Error analysis grouped by type
- Server-level statistics and breakdowns

Reports are saved to the path specified in `MarkdownOutputPath` (default: "query-results.md")
