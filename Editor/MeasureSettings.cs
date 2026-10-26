using System;
using UnityEditor;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     What a startup measure does: the platform and the device, the build, and the launches.
    /// </summary>
    [Serializable]
    public sealed class MeasureSettings
    {
        public const int DEFAULT_RUNS = 10;
        public const float DEFAULT_FIRST_LAUNCH_WAIT = 15;
        public const float DEFAULT_LAUNCH_WAIT = 5;
        public const float DEFAULT_PROFILER_SECONDS = StartupProfilerCapture.DEFAULT_SECONDS;

        /// <summary>
        ///     The platform to measure. Building needs it to be the active build target.
        /// </summary>
        public BuildTarget Platform = BuildTarget.Android;

        /// <summary>
        ///     The device to measure on: its serial on Android, its UDID, identifier or name on iOS. Empty for the only device
        ///     available.
        /// </summary>
        public string Device;

        /// <summary>
        ///     The number of cold launches, after the first launch.
        /// </summary>
        public int Runs = DEFAULT_RUNS;

        /// <summary>
        ///     Whether to build the game first, to <see cref="BuildPath" />, as a development build for a
        ///     <see cref="ProfilerCapture" />. Otherwise the build already there is measured.
        /// </summary>
        public bool Rebuild = true;

        /// <summary>
        ///     The build to measure: an APK or an AAB on Android, the folder of the Xcode project on iOS, which the measure
        ///     builds for the device with xcodebuild. Empty for the folder of the package,
        ///     <c>Build/StartupDiagnostics/&lt;platform&gt;</c>, or its <c>Development</c> folder for a Profiler capture.
        /// </summary>
        public string BuildPath;

        /// <summary>
        ///     Whether to uninstall the game and install the build before the launches, then launch it once after the install.
        ///     Uninstalling deletes the data of the game on the device.
        /// </summary>
        public bool CleanInstall = true;

        /// <summary>
        ///     Seconds to wait after the end of the start of the first launch, before the game is killed: the first launch
        ///     often downloads or caches content that the next launches use.
        /// </summary>
        public float FirstLaunchWait = DEFAULT_FIRST_LAUNCH_WAIT;

        /// <summary>
        ///     Seconds to wait after the end of the start of each cold launch, before the game is killed.
        /// </summary>
        public float LaunchWait = DEFAULT_LAUNCH_WAIT;

        /// <summary>
        ///     Whether to capture the Profiler instead of timing cold launches: a development build, launched once more after
        ///     the first launch, records the Profiler from its first scripts for <see cref="ProfilerSeconds" />. The capture
        ///     is copied next to the JSON file of the measure, then opened in the Profiler window. A development build starts
        ///     slower: its launch stays out of the cold launches.
        /// </summary>
        public bool ProfilerCapture;

        /// <summary>
        ///     Seconds of the Profiler capture, from the first scripts.
        /// </summary>
        public float ProfilerSeconds = DEFAULT_PROFILER_SECONDS;

        internal MeasureSettings Copy()
        {
            return (MeasureSettings)MemberwiseClone();
        }
    }
}