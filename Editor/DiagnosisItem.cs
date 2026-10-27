using System;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     What a check of the diagnosis found in cause: an asset, a setting or a group, with what it costs or why.
    /// </summary>
    [Serializable]
    internal sealed class DiagnosisItem
    {
        /// <summary>
        ///     What the item is: the path of an asset, the name of a setting or of a group.
        /// </summary>
        public string Label;

        /// <summary>
        ///     More about the item, such as its size or where it comes from, or null.
        /// </summary>
        public string Detail;

        /// <summary>
        ///     The path of the asset to select, or null when the item is not an asset of the project.
        /// </summary>
        public string AssetPath;
    }
}