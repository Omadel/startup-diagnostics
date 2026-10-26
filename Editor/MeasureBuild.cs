using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Builds the game for a startup measure, with the settings of the project and as a non-development build.
    /// </summary>
    internal static class MeasureBuild
    {
        public const string FOLDER = "Build/StartupDiagnostics";

        private const string DEFAULT_NAME = "Game";
        private const string INVALID_CHARACTERS = "\\/:*?\"<>|";

        /// <summary>
        ///     Where the build of the platform goes by default: <see cref="FOLDER" />/&lt;platform&gt;/&lt;product name&gt;.
        /// </summary>
        public static string GetDefaultPath(BuildTarget target)
        {
            return Path.Combine(FOLDER, target.ToString(), GetFileName(PlayerSettings.productName, target, EditorUserBuildSettings.buildAppBundle));
        }

        /// <summary>
        ///     Calls the <see cref="IBeforeMeasureBuild" /> of the project, then builds the active build target to the path.
        /// </summary>
        public static async Task BuildAsync(BuildTarget target, string path, CancellationToken cancellation)
        {
            if (target != EditorUserBuildSettings.activeBuildTarget) throw new MeasureException($"The active build target is {EditorUserBuildSettings.activeBuildTarget}: switch it to {target} to build for the measure, or measure a build already there.");

            foreach (var step in CreateBeforeBuildSteps())
            {
                cancellation.ThrowIfCancellationRequested();
                await step.OnBeforeMeasureBuildAsync(target);
            }

            cancellation.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var summary = Build(target, path).summary;
            if (summary.result != BuildResult.Succeeded) throw new MeasureException($"The build failed ({summary.result}, {summary.totalErrors} errors): see the console.");
            if ((summary.options & BuildOptions.Development) != 0) throw new MeasureException("The active build profile makes a development build, slower to start: turn Development Build off in it for the measure.");
        }

        /// <summary>
        ///     The file name of a build: the product name without the characters that file systems refuse, with the
        ///     extension of an AAB or an APK on Android, and as is for a folder elsewhere.
        /// </summary>
        internal static string GetFileName(string productName, BuildTarget target, bool appBundle)
        {
            var name = new StringBuilder();
            foreach (var character in productName.Trim())
            {
                name.Append(INVALID_CHARACTERS.IndexOf(character) >= 0 || char.IsControl(character) ? '_' : character);
            }

            if (name.Length == 0)
            {
                name.Append(DEFAULT_NAME);
            }

            if (target == BuildTarget.Android)
            {
                name.Append(appBundle ? ".aab" : ".apk");
            }

            return name.ToString();
        }

        private static BuildReport Build(BuildTarget target, string path)
        {
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null) return BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions { buildProfile = profile, locationPathName = path, options = BuildOptions.None });

            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            return BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = path, target = target, targetGroup = BuildPipeline.GetBuildTargetGroup(target), options = BuildOptions.None });
        }

        private static IBeforeMeasureBuild[] CreateBeforeBuildSteps()
        {
            return TypeCache.GetTypesDerivedFrom<IBeforeMeasureBuild>().Where(type => !type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) != null).OrderBy(type => type.FullName, StringComparer.Ordinal).Select(type => (IBeforeMeasureBuild)Activator.CreateInstance(type)).ToArray();
        }
    }
}