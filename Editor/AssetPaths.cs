using System;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     What the path of an asset tells about what goes into a build.
    /// </summary>
    internal static class AssetPaths
    {
        private const string RESOURCES = "Resources";

        private static readonly string[] _editorOnlyFolders = { "Editor", "Editor Default Resources", "Editor Resources", "Gizmos" };
        private static readonly string[] _codeExtensions = { ".cs", ".asmdef", ".asmref", ".rsp", ".dll" };

        /// <summary>
        ///     Whether an asset is in a Resources folder whose content goes into the builds: not in an editor folder, and
        ///     neither code nor a scene.
        /// </summary>
        public static bool IsInBuildResources(string path)
        {
            var folders = path.Split('/');
            var resources = Array.IndexOf(folders, RESOURCES);
            if (resources < 0 || resources == folders.Length - 1) return false;

            return CountsInBuild(path) && !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Whether a dependency of an asset counts in the build: not in an editor folder, such as the icon of a script,
        ///     and not code, which the build compiles apart.
        /// </summary>
        public static bool CountsInBuild(string path)
        {
            foreach (var extension in _codeExtensions)
            {
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) return false;
            }

            foreach (var folder in path.Split('/'))
            {
                if (Array.IndexOf(_editorOnlyFolders, folder) >= 0) return false;
            }

            return true;
        }
    }
}