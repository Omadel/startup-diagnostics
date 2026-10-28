using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The scenes of the start that the diagnosis checks.
    /// </summary>
    internal static class ScenePaths
    {
        /// <summary>
        ///     The first enabled scene of the build, then the playable scenes, each once, without empty paths; the first
        ///     scene goes to <paramref name="firstScene" />, null when the build has none.
        /// </summary>
        public static List<string> Get(IEnumerable<(string Path, bool Enabled)> buildScenes, IEnumerable<string> playableScenes, out string firstScene)
        {
            var paths = new List<string>();
            firstScene = null;
            foreach (var (path, enabled) in buildScenes)
            {
                if (!enabled || string.IsNullOrEmpty(path)) continue;

                firstScene = path;
                paths.Add(path);
                break;
            }

            foreach (var path in playableScenes)
            {
                if (!string.IsNullOrEmpty(path) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }
    }
}