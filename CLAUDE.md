# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Cross-Database Query is a C# console application that executes SQL queries across multiple SQL Server databases with intelligent fallback query support for different database schema versions. The application discovers databases, tries multiple query versions in order until one succeeds, and formats results in readable tables.

## Common Development Commands

### Build and Run
```bash
# Build the solution
dotnet build

# Run the main application
dotnet run --project src/CrossDatabaseQuery/CrossDatabaseQuery.csproj

# Run tests (uses Testcontainers for SQL Server integration testing)
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~DatabaseDiscoveryServiceTests"

# Run with verbose output
dotnet test -v normal
```

### Development Workflow
- Tests use Testcontainers.MsSql to spin up real SQL Server instances
- No mocking approach - all database interactions tested against real SQL Server
- AppSettings.json is copied to output directory and used for configuration

## Architecture Overview

### Core Services (Dependency Injection Pattern)
- **IConfigurationService**: Loads and validates AppSettings.json configuration
- **IDatabaseDiscoveryService**: Discovers user databases on SQL Server instances  
- **IFallbackQueryExecutionService**: Executes queries with intelligent fallback handling
- **ITableFormatter**: Formats query results into readable console tables

### Key Models
- **SqlServerConfiguration**: Server connection and query configuration from AppSettings.json
- **QueryDefinition**: Individual fallback query with schema version metadata
- **QueryResult**: Query execution results with timing, error info, and successful query tracking
- **DatabaseInfo**: Database metadata from discovery process

### Fallback Query Logic
The application tries queries in order until one succeeds:
1. Execute queries sequentially (modern schema first, fallback to older)
2. Classify errors: schema-related errors → try next query; other errors → skip database
3. Track which query succeeded and which failed for reporting
4. Support configurable "StopOnFirstSuccessfulQuery" behavior

### Error Classification Strategy
- **Schema-related errors** (invalid object, invalid column): Try next fallback query
- **Permission/Timeout/Connection errors**: Skip entire database
- Intelligent error detection prevents unnecessary fallback attempts

### Configuration Structure (AppSettings.json)
```json
{
  "SqlServerConfiguration": {
    "Servers": [/* SQL Server connection configs */],
    "Queries": [/* Ordered fallback queries with schema version info */],
    "QueryTimeout": 30,
    "StopOnFirstSuccessfulQuery": true
  },
  "OutputConfiguration": {
    "ShowQueryVersion": true,
    "MaxRowsPerDatabase": 100
    /* Other formatting options */
  }
}
```

## Testing Architecture

### TestBase Class Pattern
- Uses Testcontainers.MsSql for SQL Server containers
- Creates multiple test databases with different schemas (modern vs legacy)
- Provides helper methods for connection string building
- OneTimeSetUp/OneTimeTearDown lifecycle for container management

### Test Database Schemas
- **TestDatabase1**: Modern schema (Users + FeatureUsage tables)
- **TestDatabase2**: Modern schema with test data
- **LegacyDatabase**: Legacy schema (FeatureLog table) for fallback testing

### Integration Testing Strategy
- Full application workflow testing with real SQL Server
- Schema version compatibility testing
- Error handling and fallback behavior validation
- No database mocking - tests run against actual SQL Server instances

## Service Registration

All services registered as singletons in `ServiceCollectionExtensions.AddCrossDatabaseQueryServices()`:
- Dependency injection configured in Program.cs
- Uses Microsoft.Extensions.Hosting for application lifecycle
- Configuration loaded from AppSettings.json with binding

## Key Implementation Patterns

### Clean Architecture
- Interface segregation: Each service has focused interface
- Single responsibility: Each service handles one concern  
- Dependency injection throughout
- Testable design with real database integration

### Error Handling
- Comprehensive SQL error classification
- Server/database/query context in error messages
- Graceful degradation (skip problematic databases/servers)
- Detailed logging of fallback attempts

### Output Formatting
- Hierarchical display: Server → Database → Query Results
- Shows successful query version used per database
- Warns about failed query attempts with reasons
- Summary statistics including query version distribution