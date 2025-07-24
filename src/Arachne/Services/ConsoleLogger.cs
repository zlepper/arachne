namespace Arachne.Services;

public class ConsoleLogger : IConsoleLogger
{
    public void WriteLine(string message)
    {
        Console.WriteLine(message);
    }

    public void WriteLine()
    {
        Console.WriteLine();
    }

    public void Clear()
    {
        Console.Clear();
    }
}