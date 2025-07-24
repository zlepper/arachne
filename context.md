# Context: Arachne Cross-Database Query Tool

## Project Overview
Arachne is a C# console application that executes SQL queries across multiple databases on different SQL Server instances, with intelligent fallback query support for different database schema versions. The application now includes **mandatory security** to protect against accidental destructive queries.

## Recent Implementation: Secure Read-Only Query Execution

### Problem Solved
Users needed protection against accidentally running destructive SQL queries (INSERT, UPDATE, DELETE, schema changes) when querying production databases. The solution required mandatory security with no option to disable it.

### Solution Architecture
Implemented a secure execution system using temporary SQL Server application roles:

1. **Per-database security context**: One temporary application role created per database connection
2. **Read-only permissions**: All roles limited to `db_datareader` permissions only
3. **Automatic cleanup**: Disposable pattern ensures roles are always removed
4. **Transparent integration**: Existing query definitions work unchanged
5. **Efficient execution**: Role created once per database, reused for all queries

### Key Components Implemented

#### 1. SecureQueryContext (`SecureQueryContext.cs`)
- Disposable class managing a secured SQL connection
- `GetSecuredSqlConnection()`: Returns connection already in application role context
- Automatic cleanup of role and connection on dispose
- Exception-safe cleanup using try/catch in disposal

#### 2. SecureQueryExecutionService (`SecureQueryExecutionService.cs`)
- `StartSecureContextAsync()`: Creates temporary role and returns secure context
- Generates cryptographically secure passwords for temporary roles
- Uses `sp_setapprole` with cookie support for safe context switching
- Handles role creation, activation, and provides disposal mechanism

#### 3. Updated FallbackQueryExecutionService
- Modified to inject and use `ISecureQueryExecutionService`
- All queries now execute through secure contexts using `await using` pattern
- Maintains existing fallback logic and error classification
- Preserves all original functionality while adding security

#### 4. Service Registration
- Added `ISecureQueryExecutionService` to dependency injection container
- Updated constructor dependencies for `FallbackQueryExecutionService`

### Security Flow
```
1. StartSecureContextAsync() creates unique role name + secure password
2. CREATE APPLICATION ROLE with generated credentials
3. ALTER ROLE to add new role to db_datareader
4. sp_setapprole activates role with cookie for safe reversion
5. Multiple queries execute using GetSecuredSqlConnection()
6. Disposal automatically: sp_unsetapprole → DROP APPLICATION ROLE → close connection
```

### Testing Implementation
Created comprehensive test coverage:

#### Unit Tests (`SecureQueryExecutionServiceTests.cs`)
- Verify read-only context creation
- Confirm blocking of INSERT/UPDATE/DELETE/schema changes
- Test multiple queries in same context
- Validate automatic role cleanup
- Test disposal behavior and error handling

#### Integration Tests (`SecurityIntegrationTests.cs`)
- End-to-end testing through `FallbackQueryExecutionService`
- Verify destructive queries fail through normal application flow
- Confirm read-only queries continue working
- Validate data integrity preservation after blocked operations

### Current Test Status
- **Total: 52 tests** - All passing ✅
- **Security tests: 11** - All passing ✅
- **Original functionality: 41** - All preserved ✅

## Technical Architecture

### Dependencies
- **Microsoft.Data.SqlClient**: SQL Server connectivity
- **Spectre.Console**: Rich console output and progress tracking
- **Testcontainers.MsSql**: Integration testing with real SQL Server containers
- **NUnit**: Testing framework

### Configuration Structure
```json
{
  "SqlServerConfiguration": {
    "Servers": [/* server definitions */],
    "Queries": [/* fallback query definitions */],
    "QueryTimeout": 30,
    "MaxConcurrentOperations": 10
  },
  "OutputConfiguration": {
    "ShowEmptyResults": false,
    "GenerateMarkdownReport": false
    /* other display options */
  }
}
```

### Service-Oriented Architecture
- **ApplicationOrchestrator**: Main workflow coordination
- **ConfigurationService**: Configuration loading and validation
- **DatabaseDiscoveryService**: Discovers user databases on SQL Server instances
- **SecureQueryExecutionService**: **NEW** - Manages secure execution contexts
- **FallbackQueryExecutionService**: Query execution with fallback support (now secured)
- **TableFormatter/MarkdownFormatter**: Output formatting services

### Key Patterns Used
- **Dependency Injection**: All services registered in DI container
- **Disposable Pattern**: Automatic cleanup of security contexts
- **Fallback Strategy**: Multiple query versions tried in sequence
- **Parallel Execution**: Database operations with configurable concurrency
- **Error Classification**: Distinguishes schema vs. other error types

## Security Features

### Mandatory Protection
- **Always Active**: No configuration option to disable security
- **Read-Only Enforcement**: All queries limited to SELECT operations
- **Automatic Sandboxing**: Every query runs in temporary application role
- **Fail-Safe Design**: Security failure = query failure (no insecure fallback)

### Verified Protections
- ✅ Blocks INSERT operations
- ✅ Blocks UPDATE operations
- ✅ Blocks DELETE operations
- ✅ Blocks CREATE/DROP/ALTER schema changes
- ✅ Allows SELECT operations
- ✅ Preserves data integrity
- ✅ Automatic cleanup prevents role accumulation

## Development Notes

### Build Commands
```bash
dotnet build                    # Build entire solution
dotnet test                     # Run all tests
dotnet run --project src/Arachne/Arachne.csproj  # Run application
```

### Test Categories
- `[Category("Integration")]`: Tests requiring SQL Server containers
- `[Category("Unit")]`: Fast unit tests

### Current State
- **Implementation**: Complete and tested
- **Security**: Mandatory read-only execution active
- **Compatibility**: All existing functionality preserved
- **Performance**: Efficient (one role per database, not per query)
- **Reliability**: Exception-safe cleanup, comprehensive error handling
- **Code Quality**: Recently updated to use C# 12 collection expressions throughout codebase
- **QueryDefinition Model**: Updated with required properties for improved null safety
- **Async Disposal**: Fixed async disposal issues by converting `using` to `await using` for all IAsyncDisposable types (SqlCommand, SqlConnection)
- **Global Using Statements**: Implemented to reduce repetitive using declarations throughout the codebase

## Next Steps Considerations
- Monitor role cleanup in production to ensure no temporary roles accumulate
- Consider adding security audit logging for compliance requirements
- Evaluate performance impact under high concurrency scenarios
- Potential enhancement: configurable role permissions (while maintaining read-only default)

## Repository Structure
```
src/Arachne/
├── Services/
│   ├── SecureQueryContext.cs           # NEW: Disposable security context
│   ├── SecureQueryExecutionService.cs  # NEW: Security service
│   ├── ISecureQueryExecutionService.cs # NEW: Security interface
│   ├── FallbackQueryExecutionService.cs # MODIFIED: Now uses security
│   └── [other existing services]
├── Models/ [unchanged]
├── Extensions/
│   └── ServiceCollectionExtensions.cs  # MODIFIED: Added security service
└── [other existing files]

tests/Arachne.Tests/
├── Services/
│   ├── SecureQueryExecutionServiceTests.cs    # NEW: Security unit tests
│   └── FallbackQueryExecutionServiceTests.cs  # MODIFIED: Updated for DI
├── Integration/
│   ├── SecurityIntegrationTests.cs            # NEW: Security integration tests
│   └── [other existing tests]
└── [other existing test files]
```