namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     How an asset of the local Addressables content differs from the last content build.
    /// </summary>
    internal enum ContentChangeKind
    {
        /// <summary>
        ///     The asset, or one of its dependencies, changed.
        /// </summary>
        Changed,

        /// <summary>
        ///     The asset is in a local group and was not in the last content build.
        /// </summary>
        Added,

        /// <summary>
        ///     The asset was in a local group of the last content build and no longer is.
        /// </summary>
        Removed
    }
}