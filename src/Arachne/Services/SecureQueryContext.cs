
namespace Arachne.Services;

public class SecureQueryContext : IDisposable, IAsyncDisposable
{
    private readonly SqlConnection _secureConnection;
    private readonly string _roleName;
    private readonly byte[]? _cookie;
    private bool _disposed = false;

    internal SecureQueryContext(SqlConnection secureConnection, string roleName, byte[]? cookie)
    {
        _secureConnection = secureConnection;
        _roleName = roleName;
        _cookie = cookie;
    }

    public SqlConnection GetSecuredSqlConnection()
    {
        if (_disposed) 
            throw new ObjectDisposedException(nameof(SecureQueryContext));
        
        return _secureConnection;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                // Revert application role context if we have a cookie
                if (_cookie != null)
                {
                    await RevertApplicationRoleAsync(_secureConnection, _cookie);
                }

                // Drop the temporary role
                await ExecuteNonQueryAsync(_secureConnection, $"DROP APPLICATION ROLE [{_roleName}]");
            }
            catch (Exception ex)
            {
                // Log cleanup errors but don't throw during disposal
                Console.WriteLine($"Warning: Failed to cleanup security context: {ex.Message}");
            }
            finally
            {
                await _secureConnection.DisposeAsync();
                _disposed = true;
            }
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    private static async Task RevertApplicationRoleAsync(SqlConnection connection, byte[] cookie)
    {
        await using var command = new SqlCommand("sp_unsetapprole", connection);
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add("@cookie", SqlDbType.VarBinary).Value = cookie;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}