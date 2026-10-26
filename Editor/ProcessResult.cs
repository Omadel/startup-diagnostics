namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     How a command-line tool ended: its exit code and what it wrote.
    /// </summary>
    internal sealed class ProcessResult
    {
        public ProcessResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }

        public int ExitCode { get; }
        public string Output { get; }
        public string Error { get; }
    }
}