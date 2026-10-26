namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A measure read from its JSON file.
    /// </summary>
    internal sealed class MeasureFile
    {
        public MeasureFile(string path, MeasureResult result)
        {
            Path = path;
            Result = result;
        }

        /// <summary>
        ///     The JSON file, relative to the project, with forward slashes.
        /// </summary>
        public string Path { get; }

        public MeasureResult Result { get; set; }
    }
}