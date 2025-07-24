namespace Arachne.Models;

public class SqlServerConfiguration
{
    public List<ServerInfo> Servers { get; set; } = new();
    public List<QueryDefinition> Queries { get; set; } = new();
    public int QueryTimeout { get; set; } = 30;
    public int ConnectionTimeout { get; set; } = 15;
    public bool ExcludeSystemDatabases { get; set; } = true;
    public bool StopOnFirstSuccessfulQuery { get; set; } = true;
    public int MaxConcurrentOperations { get; set; } = 10;
}

public class OutputConfiguration
{
    public bool ShowEmptyResults { get; set; } = false;
    public bool IncludeTimestamp { get; set; } = true;
    public bool ShowQueryVersion { get; set; } = true;
    public int MaxRowsPerDatabase { get; set; } = 100;
    public string NullDisplayValue { get; set; } = "<NULL>";
    public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
}