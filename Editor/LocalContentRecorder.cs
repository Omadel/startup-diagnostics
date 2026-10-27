#if STARTUP_DIAGNOSTICS_ADDRESSABLES
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Writes the <see cref="ContentRecord" /> of the active build target after each content build of Addressables, Build
    ///     &amp; Release included, so that the diagnosis can tell when the local content changed since. Compiled with
    ///     Addressables only.
    /// </summary>
    internal static class LocalContentRecorder
    {
        [InitializeOnLoadMethod]
        private static void ListenToContentBuilds()
        {
            BuildScript.buildCompleted -= OnBuildCompleted;
            BuildScript.buildCompleted += OnBuildCompleted;
        }

        private static void OnBuildCompleted(AddressableAssetBuildResult result)
        {
            if (result is not AddressablesPlayerBuildResult || !string.IsNullOrEmpty(result.Error)) return;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                Write(settings);
            }
        }

        /// <summary>
        ///     Writes the record of the local content as it is now, for the active build target.
        /// </summary>
        internal static void Write(AddressableAssetSettings settings)
        {
            var record = new ContentRecord { Platform = EditorUserBuildSettings.activeBuildTarget.ToString() };
            var dependencies = new SortedDictionary<string, string>();
            foreach (var entry in AddressablesStateReader.GetLocalEntries(settings))
            {
                record.Entries.Add(new HashedAsset { Guid = entry.guid, Hash = AddressablesStateReader.GetHash(entry.guid) });
                foreach (var dependency in AssetDatabase.GetDependencies(entry.AssetPath, true))
                {
                    if (dependency == entry.AssetPath || !AssetPaths.CountsInBuild(dependency)) continue;

                    var guid = AssetDatabase.AssetPathToGUID(dependency);
                    if (!dependencies.ContainsKey(guid))
                    {
                        dependencies.Add(guid, AddressablesStateReader.GetHash(guid));
                    }
                }
            }

            foreach (var dependency in dependencies)
            {
                record.Dependencies.Add(new HashedAsset { Guid = dependency.Key, Hash = dependency.Value });
            }

            Directory.CreateDirectory(ContentRecord.FOLDER);
            File.WriteAllText(ContentRecord.GetPath(record.Platform), JsonUtility.ToJson(record, true));
        }
    }
}
#endif