using System;

namespace CanHazFunny;

public class ConsoleOutput : IOutput
{
    public void WriteLine(string message)
    {
        Console.WriteLine(message);
    }
}