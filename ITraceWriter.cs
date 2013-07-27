namespace DtoGen
{
    /// <summary>
    /// Traces
    /// </summary>
    public interface ITraceWriter
    {
        void WriteLine(string message, params object[] args);
    }
}
