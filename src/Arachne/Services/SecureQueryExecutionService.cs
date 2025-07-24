using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

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
        using var command = new SqlCommand("sp_setapprole", connection);
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
        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string GenerateSecurePassword()
    {
        // Generate a cryptographically secure random password
        const int passwordLength = 32;
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[passwordLength];
        rng.GetBytes(bytes);
        
        var result = new StringBuilder(passwordLength);
        for (int i = 0; i < passwordLength; i++)
        {
            result.Append(chars[bytes[i] % chars.Length]);
        }
        
        return result.ToString();
    }
}