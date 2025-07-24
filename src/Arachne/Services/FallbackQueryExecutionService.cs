
namespace Arachne.Services;

public interface IFallbackQueryExecutionService
{
    Task<QueryResult> ExecuteQueriesAsync(string serverName, string databaseName, string connectionString, 
        List<QueryDefinition> queries, int queryTimeout, bool stopOnFirstSuccess = true);
}

public class FallbackQueryExecutionService : IFallbackQueryExecutionService
{
    private readonly ISecureQueryExecutionService _secureExecutionService;

    public FallbackQueryExecutionService(ISecureQueryExecutionService secureExecutionService)
    {
        _secureExecutionService = secureExecutionService;
    }

    public async Task<QueryResult> ExecuteQueriesAsync(string serverName, string databaseName, 
        string connectionString, List<QueryDefinition> queries, int queryTimeout, bool stopOnFirstSuccess = true)
    {
        var result = new QueryResult
        {
            ServerName = serverName,
            DatabaseName = databaseName
        };

        var startTime = DateTime.UtcNow;

        // Create secure context once per database
        await using var secureContext = await _secureExecutionService.StartSecureContextAsync(connectionString);

        foreach (var query in queries)
        {
            try
            {
                // Execute query using the secured connection
                await using var command = new SqlCommand(query.Query, secureContext.GetSecuredSqlConnection())
                {
                    CommandTimeout = queryTimeout
                };

                var dataTable = new DataTable();
                using var adapter = new SqlDataAdapter(command);
                adapter.Fill(dataTable);

                result.Data = dataTable;
                result.SuccessfulQuery = query;
                result.HasError = false;
                result.ExecutionTime = DateTime.UtcNow - startTime;
                
                if (stopOnFirstSuccess)
                    break;
            }
            catch (SqlException ex)
            {
                result.FailedQueryNames.Add($"{query.Name} ({ex.Message})");
                
                var errorType = ClassifyError(ex);
                
                if (errorType != QueryErrorType.SchemaRelated)
                {
                    result.HasError = true;
                    result.ErrorMessage = $"Non-schema error on query '{query.Name}': {ex.Message}";
                    result.ExecutionTime = DateTime.UtcNow - startTime;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.FailedQueryNames.Add($"{query.Name} ({ex.Message})");
                result.HasError = true;
                result.ErrorMessage = $"Security or execution error on query '{query.Name}': {ex.Message}";
                result.ExecutionTime = DateTime.UtcNow - startTime;
                return result;
            }
        }

        if (result.SuccessfulQuery == null)
        {
            result.HasError = true;
            result.ErrorMessage = "All queries failed with schema-related errors";
        }

        result.ExecutionTime = DateTime.UtcNow - startTime;
        return result;
    }

    private static QueryErrorType ClassifyError(SqlException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        
        return ex switch
        {
            // Timeout errors (check specific error number first)
            { Number: -2 } => QueryErrorType.Timeout,
            
            // Schema-related errors (try next query)
            _ when message.Contains("invalid object name") ||
                   message.Contains("invalid column name") ||
                   (message.Contains("column") && message.Contains("invalid")) ||
                   (message.Contains("table") && message.Contains("doesn't exist")) ||
                   message.Contains("unknown column") ||
                   message.Contains("no such column")
                => QueryErrorType.SchemaRelated,
            
            // Permission errors
            _ when message.Contains("permission") ||
                   message.Contains("access denied") ||
                   message.Contains("login failed")
                => QueryErrorType.PermissionDenied,
            
            // Timeout errors (message-based)
            _ when message.Contains("timeout")
                => QueryErrorType.Timeout,
            
            // Connection errors
            _ when message.Contains("connection") ||
                   message.Contains("network") ||
                   message.Contains("server not found")
                => QueryErrorType.ConnectionFailed,
            
            // Default case
            _ => QueryErrorType.Other
        };
    }
}