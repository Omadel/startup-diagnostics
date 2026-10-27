namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     What a check of the diagnosis found.
    /// </summary>
    internal enum DiagnosisLevel
    {
        /// <summary>
        ///     Nothing to change.
        /// </summary>
        Passed,

        /// <summary>
        ///     A cost found without a lever tried, or a list to read.
        /// </summary>
        Info,

        /// <summary>
        ///     A clear fix whose effect is shown, measured or documented by Unity, or a start that breaks.
        /// </summary>
        Warning
    }
}