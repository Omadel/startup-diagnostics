using System.Globalization;
using UnityEditor;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     What stops a measure from starting, and what it asks before a clean install.
    /// </summary>
    internal static class MeasureChecks
    {
        /// <summary>
        ///     Why the measure cannot run with these settings, or null if it can. The build path is that of
        ///     <see cref="MeasureSettings.BuildPath" />, or the default one when empty.
        /// </summary>
        public static string GetBlockingIssue(MeasureSettings settings, string buildPath, bool buildExists, BuildTarget activeTarget, bool onMac)
        {
            if (settings.Platform != BuildTarget.Android && settings.Platform != BuildTarget.iOS) return $"The measure runs on Android and iOS, not on {settings.Platform}.";
            if (settings.Platform == BuildTarget.iOS && !onMac) return "The measure on iOS needs a Mac with Xcode.";
            if (!settings.ProfilerCapture && settings.Runs < 1) return "The number of cold launches is a whole number above 0.";
            if (settings.ProfilerCapture && !(settings.ProfilerSeconds > 0)) return "The Profiler capture lasts more than 0 s.";
            if (settings.FirstLaunchWait < 0 || settings.LaunchWait < 0) return "The waits after the start are 0 s or more.";
            if (settings.Rebuild && !settings.CleanInstall) return "A new build is measured after a clean install only: turn the clean install on, or measure the game on the device without building.";
            if (settings.Rebuild && settings.Platform != activeTarget) return $"The active build target is {activeTarget}: switch it to {settings.Platform} in Build Profiles to build for the measure, or measure the last build.";
            if (!settings.Rebuild && settings.CleanInstall && !buildExists) return $"No build at {buildPath}: measure a new build.";

            return null;
        }

        /// <summary>
        ///     What the measure does to the device, asked before a clean install.
        /// </summary>
        public static string GetConfirmation(MeasureSettings settings, string applicationIdentifier, string deviceModel, string buildPath)
        {
            var build = settings.ProfilerCapture ? "a development build" : "the game";
            var steps = settings.Rebuild ? $"builds {build} to {buildPath}, uninstalls {applicationIdentifier} from {deviceModel}, which deletes its data on the device, installs the new build" : $"uninstalls {applicationIdentifier} from {deviceModel}, which deletes its data on the device, installs the build at {buildPath}";
            var launches = settings.ProfilerCapture ? $"launches it once, then once more to capture the Profiler for {settings.ProfilerSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s" : $"launches it {settings.Runs + 1} times";
            return $"The measure {steps} and {launches}.\n\nKeep the device unlocked with its screen on, and leave it alone until the end.";
        }
    }
}