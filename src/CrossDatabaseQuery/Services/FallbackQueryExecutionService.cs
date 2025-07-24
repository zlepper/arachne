using Microsoft.Data.SqlClient;
using System.Data;
using CrossDatabaseQuery.Models;

namespace CrossDatabaseQuery.Services;

public interface IFallbackQueryExecutionService
{
    Task<QueryResult> ExecuteQueriesAsync(string serverName, string databaseName, string connectionString, 
        List<QueryDefinition> queries, int queryTimeout, bool stopOnFirstSuccess = true);
}

public class FallbackQueryExecutionService : IFallbackQueryExecutionService
{
    public async Task<QueryResult> ExecuteQueriesAsync(string serverName, string databaseName, 
        string connectionString, List<QueryDefinition> queries, int queryTimeout, bool stopOnFirstSuccess = true)
    {
        var result = new QueryResult
        {
            ServerName = serverName,
            DatabaseName = databaseName
        };

        var startTime = DateTime.UtcNow;

        foreach (var query in queries)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(query.Query, connection)
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
                result.ErrorMessage = $"Unexpected error on query '{query.Name}': {ex.Message}";
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
        
        // Schema-related errors (try next query)
        if (message.Contains("invalid object name") ||
            message.Contains("invalid column name") ||
            message.Contains("column") && message.Contains("invalid") ||
            message.Contains("table") && message.Contains("doesn't exist") ||
            message.Contains("unknown column") ||
            message.Contains("no such column"))
        {
            return QueryErrorType.SchemaRelated;
        }
        
        // Permission errors
        if (message.Contains("permission") || 
            message.Contains("access denied") ||
            message.Contains("login failed"))
        {
            return QueryErrorType.PermissionDenied;
        }
        
        // Timeout errors
        if (message.Contains("timeout") || ex.Number == -2)
        {
            return QueryErrorType.Timeout;
        }
        
        // Connection errors
        if (message.Contains("connection") || 
            message.Contains("network") ||
            message.Contains("server not found"))
        {
            return QueryErrorType.ConnectionFailed;
        }
        
        return QueryErrorType.Other;
    }
}