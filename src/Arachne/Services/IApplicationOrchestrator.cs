
namespace Arachne.Services;

public interface IApplicationOrchestrator
{
    Task<int> ExecuteAsync();
}