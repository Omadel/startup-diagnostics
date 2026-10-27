namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     An asset with the estimated size in bytes of what it takes into the build.
    /// </summary>
    internal sealed class SizedAsset
    {
        public string Path;
        public long Size;
    }
}