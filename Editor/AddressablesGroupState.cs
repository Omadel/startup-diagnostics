namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     An Addressables group that builds asset bundles, as the diagnosis reads it.
    /// </summary>
    internal sealed class AddressablesGroupState
    {
        public string Name;

        /// <summary>
        ///     The path of the asset of the group, to select it.
        /// </summary>
        public string AssetPath;

        /// <summary>
        ///     Whether its bundles are loaded from the app rather than from a server.
        /// </summary>
        public bool Local;

        /// <summary>
        ///     Whether its bundles are compressed with LZMA.
        /// </summary>
        public bool Lzma;
    }
}