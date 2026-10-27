namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     An asset of the local Addressables content that differs from the last content build.
    /// </summary>
    internal sealed class ContentChange
    {
        public string Guid;

        /// <summary>
        ///     The path of the asset, or null when it no longer exists.
        /// </summary>
        public string Path;

        public ContentChangeKind Kind;
    }
}