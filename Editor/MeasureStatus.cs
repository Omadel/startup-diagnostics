namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The states of a startup measure, as written in <see cref="MeasureResult.Status" />.
    /// </summary>
    public static class MeasureStatus
    {
        public const string RUNNING = "running";
        public const string SUCCEEDED = "succeeded";
        public const string FAILED = "failed";
        public const string CANCELLED = "cancelled";
    }
}