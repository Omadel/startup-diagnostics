namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The thresholds of the checks of the diagnosis, set in Project Settings › Startup Diagnostics.
    /// </summary>
    internal sealed class DiagnosisThresholds
    {
        public const long MEGABYTE = 1024 * 1024;

        /// <summary>
        ///     Size in bytes of the Preloaded Assets and of the content of the Resources folders together, dependencies
        ///     included, above which the diagnosis warns.
        /// </summary>
        public long PreloadedAndResourcesSize = MEGABYTE;

        /// <summary>
        ///     Size in bytes of the content of the first scene of the build above which the diagnosis warns.
        /// </summary>
        public long FirstSceneSize = MEGABYTE;

        /// <summary>
        ///     Largest size in pixels of a texture on Android or iOS above which the diagnosis warns.
        /// </summary>
        public int TextureSize = 1024;

        /// <summary>
        ///     Number of assets listed, largest first, in a list of assets with their size.
        /// </summary>
        public int ListedAssets = 10;
    }
}