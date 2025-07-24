
namespace Arachne.Services;

public interface IDatabaseDiscoveryService
{
    Task<List<DatabaseInfo>> DiscoverDatabasesAsync(string masterConnectionString, bool excludeSystemDatabases = true);
}

public class DatabaseDiscoveryService : IDatabaseDiscoveryService
{
    public async Task<List<DatabaseInfo>> DiscoverDatabasesAsync(string masterConnectionString, bool excludeSystemDatabases = true)
    {
        List<DatabaseInfo> databases = [];
        
        const string query = @"
            SELECT 
                name as DatabaseName,
                database_id,
                state_desc as Status,
                is_read_only
            FROM sys.databases 
            WHERE (@excludeSystemDbs = 0 OR database_id > 4)
              AND state_desc = 'ONLINE'
            ORDER BY name";

        try
        {
            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync();
            
            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@excludeSystemDbs", excludeSystemDatabases);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                databases.Add(new DatabaseInfo
                {
                    Name = reader["DatabaseName"].ToString()!,
                    DatabaseId = (int)reader["database_id"],
                    Status = reader["Status"].ToString()!,
                    IsReadOnly = (bool)reader["is_read_only"]
                });
            }
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException($"Failed to discover databases: {ex.Message}", ex);
        }
        
        return databases;
    }
}