namespace Arachne.Services;

public interface ISecureQueryExecutionService
{
    Task<SecureQueryContext> StartSecureContextAsync(string connectionString);
}