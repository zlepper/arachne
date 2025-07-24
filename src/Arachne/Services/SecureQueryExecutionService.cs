using System.Security.Cryptography;

namespace Arachne.Services;

public class SecureQueryExecutionService : ISecureQueryExecutionService
{
    public async Task<SecureQueryContext> StartSecureContextAsync(string connectionString)
    {
        var roleName = $"TempReadOnly_{Guid.NewGuid():N}";
        var rolePassword = GenerateSecurePassword();
        
        var connection = new SqlConnection(connectionString);
        
        try
        {
            await connection.OpenAsync();
            
            // Create temporary application role
            await ExecuteNonQueryAsync(connection, 
                $"CREATE APPLICATION ROLE [{roleName}] WITH PASSWORD = '{rolePassword}'");
            
            // Add role to db_datareader for read-only access
            await ExecuteNonQueryAsync(connection, 
                $"ALTER ROLE db_datareader ADD MEMBER [{roleName}]");
            
            // Activate the role and get cookie for safe reversion
            var cookie = await ActivateApplicationRoleAsync(connection, roleName, rolePassword);
            
            return new SecureQueryContext(connection, roleName, cookie);
        }
        catch
        {
            // If setup fails, cleanup and rethrow
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<byte[]?> ActivateApplicationRoleAsync(SqlConnection connection, string roleName, string password)
    {
        await using var command = new SqlCommand("sp_setapprole", connection);
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add("@rolename", SqlDbType.VarChar).Value = roleName;
        command.Parameters.Add("@password", SqlDbType.VarChar).Value = password;
        command.Parameters.Add("@fCreateCookie", SqlDbType.Bit).Value = true;
        
        var cookieParam = command.Parameters.Add("@cookie", SqlDbType.VarBinary, 8000);
        cookieParam.Direction = ParameterDirection.Output;
        
        await command.ExecuteNonQueryAsync();
        return (byte[]?)cookieParam.Value;
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string GenerateSecurePassword()
    {
        // Generate a cryptographically secure random password using modern memory-efficient techniques
        const int passwordLength = 32;
        ReadOnlySpan<char> chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        
        // Use stack allocation for small, fixed-size random bytes buffer
        Span<byte> bytes = stackalloc byte[passwordLength];
        RandomNumberGenerator.Fill(bytes);
        
        // Use string.Create for efficient string construction without intermediate allocations
        return string.Create(passwordLength, 0, (span, _) =>
        {
            ReadOnlySpan<char> characterSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            Span<byte> randomBytes = stackalloc byte[passwordLength];
            RandomNumberGenerator.Fill(randomBytes);
            
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = characterSet[randomBytes[i] % characterSet.Length];
            }
        });
    }
}