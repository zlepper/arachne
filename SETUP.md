# Development Setup Guide

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
   cp src/CrossDatabaseQuery/AppSettings.json src/CrossDatabaseQuery/AppSettings.Development.json
   ```

4. **Edit AppSettings.Development.json with your actual credentials**
   - Replace example server names with your actual SQL Server instances
   - Update connection strings with real credentials
   - Modify queries to match your database schema

5. **Run the application**
   ```bash
   dotnet run --project src/CrossDatabaseQuery/CrossDatabaseQuery.csproj
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

## Example Development Configuration

```json
{
  "SqlServerConfiguration": {
    "Servers": [
      {
        "Name": "Production-Customer-A",
        "MasterConnectionString": "Server=prod-sql-01.customer-a.com;Database=master;User Id=readonly_user;Password=SecurePassword123!;TrustServerCertificate=true;",
        "Description": "Customer A Production Environment"
      },
      {
        "Name": "Production-Customer-B", 
        "MasterConnectionString": "Server=prod-sql-02.customer-b.com;Database=master;User Id=query_user;Password=AnotherPassword456!;TrustServerCertificate=true;",
        "Description": "Customer B Production Environment"
      }
    ],
    "QueryTimeout": 60,
    "ConnectionTimeout": 30
  },
  "OutputConfiguration": {
    "ShowEmptyResults": true,
    "MaxRowsPerDatabase": 1000
  }
}
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
- Check that `AppSettings.json` exists in the current directory or `src/CrossDatabaseQuery/`

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