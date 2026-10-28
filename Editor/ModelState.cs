namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A model file whose animation clips a scene pulls in.
    /// </summary>
    internal sealed class ModelState
    {
        public string Path;

        /// <summary>
        ///     Number of its clips that the scene pulls in.
        /// </summary>
        public int Clips;

        /// <summary>
        ///     Estimated size in bytes of those clips.
        /// </summary>
        public long Size;
    }
}