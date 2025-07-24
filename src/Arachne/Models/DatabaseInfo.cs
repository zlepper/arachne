namespace Arachne.Models;

public class DatabaseInfo
{
    public string Name { get; set; } = string.Empty;
    public int DatabaseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
}