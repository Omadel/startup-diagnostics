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
        ///     Whether to build the game first, to <see cref="BuildPath" />. Otherwise the build already there is measured.
        /// </summary>
        public bool Rebuild = true;

        /// <summary>
        ///     The build to measure: an APK or an AAB on Android, the folder of the Xcode project on iOS, which the measure
        ///     builds for the device with xcodebuild. Empty for the folder of the package,
        ///     <c>Build/StartupDiagnostics/&lt;platform&gt;</c>.
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
    }
}