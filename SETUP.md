# Arachne - Development Setup Guide

> Named after the legendary weaver from Greek mythology, Arachne weaves intelligent queries across multiple database schemas.

## Quick Start

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd cross-database-query
   ```

2. **Build the solution**
   ```bash
   dotnet build
   ```

3. **Create your development configuration**
   ```bash
   cp src/Arachne/AppSettings.json src/Arachne/AppSettings.Development.json
   ```

4. **Edit AppSettings.Development.json with your actual credentials**
   - Replace example server names with your actual SQL Server instances
   - Update connection strings with real credentials
   - Modify queries to match your database schema

5. **Run the application**
   ```bash
   dotnet run --project src/Arachne/Arachne.csproj
   ```

## Configuration Security

### ✅ Safe Files (Committed to Git)
- `AppSettings.json` - Contains only example/template configuration
- All source code files

### ❌ Sensitive Files (Gitignored)
- `AppSettings.Development.json` - Contains real production credentials
- `appsettings.Development.json` - Alternative naming convention
- Any file matching `**/AppSettings.Development.json` pattern

## Configuration Hierarchy

The application loads configuration in this order:
1. `AppSettings.json` (base configuration)
2. `AppSettings.Development.json` (overrides base, if exists)

This means you only need to specify the differences in your development file.

### Important Note About Configuration Properties

The current `AppSettings.Development.json` file in the repository contains properties that are not implemented in the actual code models:
- `MasterConnectionString` (should be `ConnectionString`)
- `Description` (not supported in ServerInfo model)
- `ShowServerDescription` (not supported in OutputConfiguration model)
- `ColumnPadding` (not supported in OutputConfiguration model)
- Query properties like `Description` and `SchemaVersion` (not supported in QueryDefinition model)

The examples below show the correct property names that actually work with the current codebase.

## Example Development Configuration

```json
{
  "SqlServerConfiguration": {
    "Servers": [
      {
        "Name": "Production-Customer-A",
        "ConnectionString": "Server=prod-sql-01.customer-a.com;Database=master;User Id=readonly_user;Password=SecurePassword123!;TrustServerCertificate=true;"
      },
      {
        "Name": "Production-Customer-B", 
        "ConnectionString": "Server=prod-sql-02.customer-b.com;Database=master;User Id=query_user;Password=AnotherPassword456!;TrustServerCertificate=true;"
      }
    ],
    "QueryTimeout": 60,
    "ConnectionTimeout": 30,
    "ExcludeSystemDatabases": true,
    "StopOnFirstSuccessfulQuery": true,
    "MaxConcurrentOperations": 10
  },
  "OutputConfiguration": {
    "ShowEmptyResults": true,
    "IncludeTimestamp": true,
    "ShowQueryVersion": true,
    "MaxRowsPerDatabase": 1000,
    "NullDisplayValue": "<NULL>",
    "DateTimeFormat": "yyyy-MM-dd HH:mm:ss",
    "GenerateMarkdownReport": true,
    "MarkdownOutputPath": "production-database-analysis.md",
    "MarkdownIncludeFailedQueries": true
  }
}
```

## Markdown Reports

Arachne can generate comprehensive markdown reports that include all query results plus detailed analytics. This is particularly useful for:

- **Documentation**: Create reports for compliance or audit purposes
- **Team Sharing**: Share results with stakeholders who don't have direct database access
- **Historical Analysis**: Keep records of database states over time
- **Pull Request Documentation**: Include database impact analysis in code reviews

### Enabling Markdown Reports

Add these settings to your `OutputConfiguration` in `AppSettings.Development.json`:

```json
{
  "OutputConfiguration": {
    "GenerateMarkdownReport": true,
    "MarkdownOutputPath": "database-analysis-{date}.md",
    "MarkdownIncludeFailedQueries": true
  }
}
```

### Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `GenerateMarkdownReport` | `false` | Enable markdown report generation |
| `MarkdownOutputPath` | `"query-results.md"` | Output file path (supports date placeholders) |
| `MarkdownIncludeFailedQueries` | `true` | Include information about failed query attempts |

### Sample Report Content

When enabled, Arachne will generate a comprehensive report including:

- **Executive Summary**: Key metrics like success rates, execution times, total databases processed
- **Query Version Analysis**: Which fallback queries were used across different databases
- **Server Breakdown**: Results organized by server with individual database details
- **Performance Analytics**: Fastest/slowest queries, timing distributions, performance outliers
- **Error Analysis**: Common error patterns grouped by type and frequency

The report is saved alongside your console output, providing a permanent record of your database analysis.

### Quick Test

To quickly test markdown report generation with the existing sample configuration:

1. **Enable markdown reports temporarily:**
   ```bash
   # Edit AppSettings.json and set GenerateMarkdownReport to true
   sed -i 's/"GenerateMarkdownReport": false/"GenerateMarkdownReport": true/' src/Arachne/AppSettings.json
   ```

2. **Run with sample data:**
   ```bash
   dotnet run --project src/Arachne/Arachne.csproj
   ```

3. **Check the generated report:**
   ```bash
   cat query-results.md
   ```

4. **Reset configuration:**
   ```bash
   git checkout src/Arachne/AppSettings.json
   ```

## Testing

Run the test suite to ensure everything works:

```bash
dotnet test
```

The tests use Testcontainers to spin up real SQL Server instances, so Docker must be available on your system.

## Troubleshooting

### "Configuration file not found" error
- Ensure you're running from the project root directory
- Check that `AppSettings.json` exists in the current directory or `src/Arachne/`

### "No SQL Server configurations found" error
- Verify your `AppSettings.Development.json` has valid server configurations
- Check the JSON syntax is valid

### Connection errors
- Verify SQL Server instances are accessible
- Check firewall rules and network connectivity
- Ensure SQL Server is configured to allow remote connections
- Verify credentials are correct

### Permission errors
- Ensure the database user has permission to:
  - Connect to the master database
  - Query sys.databases view
  - Connect to individual databases
  - Execute your custom queries

## Security Best Practices

1. **Never commit credentials to git**
   - Always use `AppSettings.Development.json` for real credentials
   - Double-check gitignore is working: `git status` should not show development config

2. **Use minimal permissions**
   - Create dedicated database users with read-only access
   - Grant only necessary permissions for database discovery and querying

3. **Secure connection strings**
   - Use `TrustServerCertificate=true` only for development/testing
   - Consider using Windows Authentication when possible
   - Use encrypted connections in production

4. **Regular rotation**
   - Rotate database passwords regularly
   - Update development configurations accordingly