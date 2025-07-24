# Cross-Database Query Console Application Plan (Final with Fallback Queries)

## Overview
A C# console application that connects to SQL Server instances, discovers all user databases, executes configurable fallback SQL queries to handle different schema versions, and displays full tabular results with proper formatting.

## Project Structure
```
CrossDatabaseQuery/
├── CrossDatabaseQuery.csproj
├── Program.cs
├── AppSettings.json
├── Models/
│   ├── SqlServerConfiguration.cs
│   ├── QueryDefinition.cs (NEW - for fallback queries)
│   ├── ServerInfo.cs
│   ├── DatabaseInfo.cs
│   └── QueryResult.cs (enhanced with query version info)
├── Services/
│   ├── ConfigurationService.cs
│   ├── DatabaseDiscoveryService.cs
│   ├── FallbackQueryExecutionService.cs (NEW - handles query fallbacks)
│   └── TableFormatter.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

## Enhanced Configuration with Fallback Queries (AppSettings.json)
```json
{
  "SqlServerConfiguration": {
    "Servers": [
      {
        "Name": "Customer1-Prod",
        "MasterConnectionString": "Server=srv1;Database=master;Trusted_Connection=true;",
        "Description": "Customer 1 Production Server"
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
      },
      {
        "Name": "FeatureUsage_v1",
        "Description": "Legacy schema with basic tracking",
        "Query": "SELECT FeatureName, COUNT(*) as UsageCount FROM FeatureLog WHERE LogDate > DATEADD(day, -30, GETDATE()) GROUP BY FeatureName",
        "SchemaVersion": "1.0-1.9"
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
    "ShowServerDescription": true,
    "ShowQueryVersion": true,
    "MaxRowsPerDatabase": 100,
    "ColumnPadding": 2,
    "NullDisplayValue": "<NULL>",
    "DateTimeFormat": "yyyy-MM-dd HH:mm:ss"
  }
}
```

## Enhanced Result Models

### QueryDefinition.cs
```csharp
public class QueryDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Query { get; set; }
    public string SchemaVersion { get; set; }
}
```

### QueryResult.cs (Enhanced)
```csharp
public class QueryResult
{
    public string ServerName { get; set; }
    public string DatabaseName { get; set; }
    public DataTable Data { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; }
    public QueryDefinition SuccessfulQuery { get; set; } // NEW
    public List<string> FailedQueryNames { get; set; } // NEW
    public int RowCount => Data?.Rows.Count ?? 0;
    public bool HasData => Data != null && Data.Rows.Count > 0;
}
```

## FallbackQueryExecutionService Logic
1. **Try queries in order**: Execute queries sequentially until one succeeds
2. **Schema error detection**: Distinguish between schema errors (invalid object name, invalid column) vs other errors
3. **Continue on schema errors**: If error suggests schema incompatibility, try next query
4. **Stop on other errors**: For timeouts, permissions, etc., don't try remaining queries
5. **Track attempts**: Record which queries were tried and why they failed

## Error Classification
```csharp
public enum QueryErrorType
{
    SchemaRelated,    // Try next query
    PermissionDenied, // Skip database
    Timeout,          // Skip database  
    ConnectionFailed, // Skip database
    Other            // Skip database
}
```

## Expected Output Format with Query Versions
```
========================================
Cross-Database Query Results
========================================
Queries Configured: 3 (FeatureUsage_v3 → FeatureUsage_v2 → FeatureUsage_v1)
Executed: 2024-07-24 10:30:15

┌─────────────────────────────────────────┐
│ Customer1-Prod (srv1)                   │
└─────────────────────────────────────────┘

Database: CustomerDB_2024 [Query: FeatureUsage_v3] (2 rows)
┌──────────┬─────────────────┬─────────────────────┬────────────┐
│ UserName │ FeatureName     │ LastUsed            │ UsageCount │
├──────────┼─────────────────┼─────────────────────┼────────────┤
│ john.doe │ ReportBuilder   │ 2024-07-23 14:22:10 │ 47         │
│ jane.smith│ DataExport      │ 2024-07-24 09:15:33 │ 12         │
└──────────┴─────────────────┴─────────────────────┴────────────┘

Database: CustomerDB_Legacy [Query: FeatureUsage_v1] (3 rows)
⚠️  Queries failed: FeatureUsage_v3 (Invalid object 'Users'), FeatureUsage_v2 (Invalid column 'UserID')
┌─────────────────┬────────────┐
│ FeatureName     │ UsageCount │
├─────────────────┼────────────┤
│ ReportBuilder   │ 45         │
│ DataExport      │ 23         │
│ LegacyReports   │ 12         │
└─────────────────┴────────────┘

Database: CustomerDB_Archive [Query: FeatureUsage_v2] (1 row)
⚠️  Query failed: FeatureUsage_v3 (Invalid object 'Users')
┌────────┬─────────────────┬─────────────────────┬────────────┐
│ UserID │ FeatureName     │ LastUsed            │ UsageCount │
├────────┼─────────────────┼─────────────────────┼────────────┤
│ 2001   │ LegacyReports   │ 2024-07-20 11:30:15 │ 5          │
└────────┴─────────────────┴─────────────────────┴────────────┘

========================================
Summary:
- Servers processed: 1/1 successful
- Databases discovered: 3
- Databases with results: 3
- Query version usage:
  • FeatureUsage_v3: 1 database(s)
  • FeatureUsage_v2: 1 database(s) 
  • FeatureUsage_v1: 1 database(s)
- Total execution time: 12.4s
========================================
```

## Key Implementation Features
1. **Ordered query execution**: Try modern schema first, fallback to older versions
2. **Smart error handling**: Distinguish schema errors from other failures
3. **Schema version tracking**: Show which query worked for each database
4. **Flexible configuration**: Easy to add new query versions
5. **Detailed logging**: Track all attempts and failure reasons
6. **Performance optimized**: Stop on first successful query per database
7. **Backward compatibility**: Support databases with different schema ages
8. **Comprehensive reporting**: Show query version distribution across databases

## Database Discovery Logic
```sql
-- Query to discover user databases
SELECT 
    name as DatabaseName,
    database_id,
    state_desc as Status,
    is_read_only
FROM sys.databases 
WHERE database_id > 4  -- Exclude system databases
  AND state_desc = 'ONLINE'  -- Only online databases
  AND is_read_only = 0  -- Exclude read-only databases (optional)
ORDER BY name
```

## Implementation Approach
1. **Setup project**: Create console app with dependency injection
2. **Configuration**: Implement server-based configuration loading with fallback queries
3. **Database discovery**: Build service to enumerate databases per server
4. **Fallback query execution**: Implement service to try queries in order with smart error handling
5. **Output formatting**: Create hierarchical result display with query version info
6. **Error handling**: Comprehensive logging with server/database/query context
7. **Testing**: Validate against multiple server configurations and schema versions

## NuGet Dependencies
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Json  
- Microsoft.Data.SqlClient
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.DependencyInjection
- System.Data.Common (included in .NET)

This approach ensures maximum compatibility across your customer environments while providing clear visibility into which schema versions are in use.