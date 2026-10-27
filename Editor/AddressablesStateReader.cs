using System;
#if STARTUP_DIAGNOSTICS_ADDRESSABLES
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Reads into an <see cref="AddressablesState" /> the settings of Addressables and how its local content differs from
    ///     the last content build of the active build target, without changing them. Compiled with Addressables only.
    /// </summary>
    internal static class AddressablesStateReader
    {
        private const string RUNTIME_PATH = "Addressables.RuntimePath";

        /// <summary>
        ///     The settings and the local content of Addressables, or null when Addressables is not installed or not set up.
        /// </summary>
        public static AddressablesState Read()
        {
#if STARTUP_DIAGNOSTICS_ADDRESSABLES
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;

            var state = new AddressablesState
            {
                SettingsPath = AssetDatabase.GetAssetPath(settings),
                RemoteCatalog = settings.BuildRemoteCatalog,
                UpdateCatalogsOnStartup = !settings.DisableCatalogUpdateOnStartup,
#if STARTUP_DIAGNOSTICS_ADDRESSABLES_2
                JsonCatalog = settings.EnableJsonCatalog,
#endif
                ContentPlatform = EditorUserBuildSettings.activeBuildTarget.ToString()
            };
            foreach (var group in settings.groups)
            {
                var schema = group == null ? null : group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                state.Groups.Add(new AddressablesGroupState { Name = group.Name, AssetPath = AssetDatabase.GetAssetPath(group), Local = IsLocal(group, settings), Lzma = schema.Compression == BundledAssetGroupSchema.BundleCompressionMode.LZMA });
            }

            state.ContentChanges = ReadContentChanges(settings, state.ContentPlatform);
            return state;
#else
            return null;
#endif
        }

        /// <summary>
        ///     Whether bundles with this load path are loaded from the app: from the runtime path of Addressables.
        /// </summary>
        public static bool IsLocalLoadPath(string loadPath)
        {
            return !string.IsNullOrEmpty(loadPath) && loadPath.IndexOf(RUNTIME_PATH, StringComparison.Ordinal) >= 0;
        }

#if STARTUP_DIAGNOSTICS_ADDRESSABLES
        /// <summary>
        ///     The entries of the groups whose bundles are loaded from the app, the folders replaced by their assets.
        /// </summary>
        internal static List<AddressableAssetEntry> GetLocalEntries(AddressableAssetSettings settings)
        {
            var entries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(entries, false, group => IsLocal(group, settings));
            return entries;
        }

        /// <summary>
        ///     The hash of what an asset is built from, as the content state of Addressables records it.
        /// </summary>
        internal static string GetHash(string guid)
        {
            return AssetDatabase.GetAssetDependencyHash(AssetDatabase.GUIDToAssetPath(guid)).ToString();
        }

        private static bool IsLocal(AddressableAssetGroup group, AddressableAssetSettings settings)
        {
            var schema = group == null ? null : group.GetSchema<BundledAssetGroupSchema>();
            return schema != null && IsLocalLoadPath(schema.LoadPath.GetValue(settings));
        }

        /// <summary>
        ///     The local content that differs from the record of the last content build of a platform, or null when no
        ///     content build was recorded for it since the package is installed.
        /// </summary>
        private static List<ContentChange> ReadContentChanges(AddressableAssetSettings settings, string platform)
        {
            var record = ContentRecord.Read(platform);
            if (record == null) return null;

            var currentEntries = new Dictionary<string, string>();
            foreach (var entry in GetLocalEntries(settings))
            {
                currentEntries[entry.guid] = GetHash(entry.guid);
            }

            var changes = LocalContentChanges.Find(ContentRecord.ToDictionary(record.Entries), ContentRecord.ToDictionary(record.Dependencies), currentEntries, GetHash);
            foreach (var change in changes)
            {
                var path = AssetDatabase.GUIDToAssetPath(change.Guid);
                change.Path = string.IsNullOrEmpty(path) ? null : path;
            }

            return changes;
        }
#endif
    }
}