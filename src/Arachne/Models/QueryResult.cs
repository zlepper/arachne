using System.Data;

namespace Arachne.Models;

public class QueryResult
{
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public DataTable? Data { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public QueryDefinition? SuccessfulQuery { get; set; }
    public List<string> FailedQueryNames { get; set; } = new();
    public int RowCount => Data?.Rows.Count ?? 0;
    public bool HasData => Data != null && Data.Rows.Count > 0;
}

public enum QueryErrorType
{
    SchemaRelated,
    PermissionDenied,
    Timeout,
    ConnectionFailed,
    Other
}