namespace Arachne.Models;

public class QueryDefinition
{
    public required string Name { get; set; }
    public string? Query { get; set; }
    public string? QueryFile { get; set; }
}