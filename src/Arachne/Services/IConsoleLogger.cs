namespace Arachne.Services;

public interface IConsoleLogger
{
    void WriteLine(string message);
    void WriteLine();
    void Clear();
}