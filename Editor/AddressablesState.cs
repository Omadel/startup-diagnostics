using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The settings and the local content of Addressables that the diagnosis reads.
    /// </summary>
    internal sealed class AddressablesState
    {
        /// <summary>
        ///     The path of the settings asset of Addressables, to select it.
        /// </summary>
        public string SettingsPath;

        /// <summary>
        ///     Whether the content build writes a remote catalog (Build Remote Catalog).
        /// </summary>
        public bool RemoteCatalog;

        /// <summary>
        ///     Whether Addressables checks for a newer remote catalog while it initializes: the opposite of Only update
        ///     catalogs manually.
        /// </summary>
        public bool UpdateCatalogsOnStartup;

        /// <summary>
        ///     Whether the catalog is written in JSON rather than in binary (Enable Json Catalog).
        /// </summary>
        public bool JsonCatalog;

        public List<AddressablesGroupState> Groups = new();

        /// <summary>
        ///     The platform of the last content build that <see cref="ContentChanges" /> compares with: the active build
        ///     target.
        /// </summary>
        public string ContentPlatform;

        /// <summary>
        ///     The local content that differs from the last content build of <see cref="ContentPlatform" />, or null when no
        ///     content build was recorded for it: see <see cref="ContentRecord" />.
        /// </summary>
        public List<ContentChange> ContentChanges;
    }
}