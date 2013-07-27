using System;

namespace DtoGen
{
    public class ConsoleTraceWriter :ITraceWriter
    {
        public void WriteLine(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }
    }
}
