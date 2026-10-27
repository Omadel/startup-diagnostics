using System;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     An asset by its GUID, with the hash of what it is built from.
    /// </summary>
    [Serializable]
    internal sealed class HashedAsset
    {
        public string Guid;
        public string Hash;
    }
}