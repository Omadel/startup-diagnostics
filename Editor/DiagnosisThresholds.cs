using System;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The thresholds of the checks of the diagnosis, set in Project Settings › Startup Diagnostics.
    /// </summary>
    internal sealed class DiagnosisThresholds
    {
        public const long MEGABYTE = 1024 * 1024;
        public const float DEFAULT_MEGABYTES = 1;
        public const int DEFAULT_TEXTURE_SIZE = 1024;
        public const int DEFAULT_LISTED_ASSETS = 10;

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
        public int TextureSize = DEFAULT_TEXTURE_SIZE;

        /// <summary>
        ///     Number of assets listed, largest first, in a list of assets with their size.
        /// </summary>
        public int ListedAssets = DEFAULT_LISTED_ASSETS;

        /// <summary>
        ///     The thresholds of the settings, sizes in megabytes: no size below zero, at least one pixel and one asset listed.
        /// </summary>
        public static DiagnosisThresholds Create(float preloadedAndResourcesMegabytes, float firstSceneMegabytes, int textureSize, int listedAssets)
        {
            return new DiagnosisThresholds
            {
                PreloadedAndResourcesSize = ToBytes(preloadedAndResourcesMegabytes),
                FirstSceneSize = ToBytes(firstSceneMegabytes),
                TextureSize = Math.Max(1, textureSize),
                ListedAssets = Math.Max(1, listedAssets)
            };
        }

        private static long ToBytes(float megabytes)
        {
            return megabytes > 0 ? (long)Math.Round(megabytes * (double)MEGABYTE) : 0;
        }
    }
}