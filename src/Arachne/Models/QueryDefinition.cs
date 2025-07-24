namespace Arachne.Models;

public class QueryDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
}