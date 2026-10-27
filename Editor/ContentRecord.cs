using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The local Addressables content at the last content build of a platform: the hashes of the entries of the local
    ///     groups and of their dependencies, written by <see cref="LocalContentRecorder" /> after each content build in
    ///     <c>Library/StartupDiagnostics/Content</c>. The content state of Addressables cannot serve: it lists only the groups
    ///     that prevent updates.
    /// </summary>
    [Serializable]
    internal sealed class ContentRecord
    {
        public const string FOLDER = StartupMeasure.RESULTS_FOLDER + "/Content";

        /// <summary>
        ///     The build target of the content build.
        /// </summary>
        public string Platform;

        public List<HashedAsset> Entries = new();
        public List<HashedAsset> Dependencies = new();

        /// <summary>
        ///     The file of the record of a platform.
        /// </summary>
        public static string GetPath(string platform)
        {
            return $"{FOLDER}/{platform}.json";
        }

        /// <summary>
        ///     The record of the last content build of a platform, or null when none was written or it cannot be read.
        /// </summary>
        public static ContentRecord Read(string platform)
        {
            var path = GetPath(platform);
            if (!File.Exists(path)) return null;

            try
            {
                return JsonUtility.FromJson<ContentRecord>(File.ReadAllText(path));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        ///     The hashes by GUID.
        /// </summary>
        public static Dictionary<string, string> ToDictionary(IEnumerable<HashedAsset> assets)
        {
            var hashes = new Dictionary<string, string>();
            foreach (var asset in assets)
            {
                hashes[asset.Guid] = asset.Hash;
            }

            return hashes;
        }
    }
}