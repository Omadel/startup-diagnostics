using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Estimates in the editor what an asset takes into a build.
    /// </summary>
    internal static class AssetSizes
    {
        private static readonly MethodInfo _textureStorageSize = typeof(Editor).Assembly.GetType("UnityEditor.TextureUtil")?.GetMethod("GetStorageMemorySizeLong", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        /// <summary>
        ///     The estimated size in bytes of the objects of an asset: textures in their format for the active build target,
        ///     as the inspector shows it, and the rest by its size in memory in the editor. Nothing for a scene.
        /// </summary>
        public static long Estimate(string path)
        {
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return 0;

            var size = 0L;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset == null) continue;

                size += asset is Texture texture && _textureStorageSize != null ? (long)_textureStorageSize.Invoke(null, new object[] { texture }) : Profiler.GetRuntimeMemorySizeLong(asset);
            }

            return size;
        }
    }
}