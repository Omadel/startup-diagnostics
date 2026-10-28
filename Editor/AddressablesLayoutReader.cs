using System.Collections.Generic;
#if STARTUP_DIAGNOSTICS_ADDRESSABLES_2
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.Layout;
#endif

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Reads the real sizes of the assets in the asset bundles from the build layout of the last content build of
    ///     Addressables, written when Debug Build Layout is on in the preferences of Addressables. Compiled with
    ///     Addressables 2 only.
    /// </summary>
    internal static class AddressablesLayoutReader
    {
        public const string LAYOUT_PATH = "Library/com.unity.addressables/buildlayout.json";

        /// <summary>
        ///     The size in bytes of each asset of the bundles, uncompressed, by path, with the date of the build in
        ///     <paramref name="date" />; null without a build layout of the active build target.
        /// </summary>
        public static Dictionary<string, long> ReadSizes(out string date)
        {
            date = null;
#if STARTUP_DIAGNOSTICS_ADDRESSABLES_2
            if (!File.Exists(LAYOUT_PATH)) return null;

            BuildLayout layout = null;
            try
            {
                layout = BuildLayout.Open(LAYOUT_PATH, true, true);
                if (layout == null || layout.BuildTarget != EditorUserBuildSettings.activeBuildTarget) return null;

                var sizes = new Dictionary<string, long>();
                foreach (var group in layout.Groups)
                {
                    foreach (var bundle in group.Bundles)
                    {
                        foreach (var file in bundle.Files)
                        {
                            foreach (var asset in file.Assets)
                            {
                                sizes[asset.AssetPath] = (long)(asset.SerializedSize + asset.StreamedSize);
                            }

                            foreach (var asset in file.OtherAssets)
                            {
                                sizes[asset.AssetPath] = (long)(asset.SerializedSize + asset.StreamedSize);
                            }
                        }
                    }
                }

                date = layout.BuildStart.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                return sizes;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                layout?.Close();
            }
#else
            return null;
#endif
        }
    }
}