using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The xcodebuild and devicectl commands of the measure on iOS, and how their output reads.
    /// </summary>
    internal static class IosCommands
    {
        public const string XCODE_DEVELOPER_DIR = "/Applications/Xcode.app/Contents/Developer";
        public const string PROJECT = "Unity-iPhone.xcodeproj";
        public const string SCHEME = "Unity-iPhone";
        public const string CONFIGURATION = "Release";

        /// <summary>
        ///     The file of the timeline in the data container of the app: <see cref="StartupTimeline.FILE_NAME" /> in
        ///     <see cref="Application.temporaryCachePath" />.
        /// </summary>
        public const string STARTUP_FILE = "Library/Caches/" + StartupTimeline.FILE_NAME;

        /// <summary>
        ///     The folder of <see cref="Application.persistentDataPath" /> in the data container of the app, where the
        ///     files of the Profiler capture go.
        /// </summary>
        public const string DATA_FOLDER = "Documents/";

        /// <summary>
        ///     The devicectl error of a connection to the device that could not be established, often passing over the
        ///     local network: worth another try.
        /// </summary>
        public const int CONNECTION_ERROR = 4000;

        private const string CORE_DEVICE_ERROR = "com.apple.dt.CoreDeviceError";
        private const string PHYSICAL = "physical";
        private const string TUNNEL_UNAVAILABLE = "unavailable";

        private static readonly Regex _team = new(@"DEVELOPMENT_TEAM = (\w+);");

        /// <summary>
        ///     The Xcode to run the tools of: <c>DEVELOPER_DIR</c> if set, else the Xcode of the Applications folder if
        ///     installed, which devicectl needs when <c>xcode-select</c> points to the Command Line Tools; null to leave it to
        ///     <c>xcode-select</c>.
        /// </summary>
        public static string GetDeveloperDir(string fromEnvironment, bool xcodeInstalled)
        {
            if (!string.IsNullOrEmpty(fromEnvironment)) return fromEnvironment;

            return xcodeInstalled ? XCODE_DEVELOPER_DIR : null;
        }

        /// <summary>
        ///     The team that signs the app: the one of the Player Settings, else the first one of the Xcode project. Empty
        ///     without one.
        /// </summary>
        public static string GetTeam(string playerSettingsTeam, string xcodeProject)
        {
            if (!string.IsNullOrEmpty(playerSettingsTeam)) return playerSettingsTeam;

            var team = _team.Match(xcodeProject ?? string.Empty);
            return team.Success ? team.Groups[1].Value : string.Empty;
        }

        public static DevicectlJson.Output Parse(string json)
        {
            return string.IsNullOrWhiteSpace(json) ? new DevicectlJson.Output() : JsonUtility.FromJson<DevicectlJson.Output>(json);
        }

        /// <summary>
        ///     The description of the error of a devicectl output, or null if it succeeded.
        /// </summary>
        public static string GetError(DevicectlJson.Output output)
        {
            var error = output.error;
            if (error == null || string.IsNullOrEmpty(error.domain)) return null;

            var description = error.userInfo?.NSLocalizedDescription?.@string;
            return string.IsNullOrEmpty(description) ? $"{error.domain} error {error.code}" : description;
        }

        public static bool IsConnectionError(DevicectlJson.Output output)
        {
            return output.error != null && output.error.domain == CORE_DEVICE_ERROR && output.error.code == CONNECTION_ERROR;
        }

        /// <summary>
        ///     The device to measure on, among the physical ones that devicectl can reach: the requested one, by UDID,
        ///     identifier or name, or else the only one. Throws a <see cref="MeasureException" /> saying what to do when
        ///     there is none or several.
        /// </summary>
        public static DevicectlJson.Device SelectDevice(IReadOnlyList<DevicectlJson.Device> devices, string requested)
        {
            var reachable = GetReachable(devices);
            if (!string.IsNullOrEmpty(requested))
            {
                var match = reachable.FirstOrDefault(device => device.hardwareProperties.udid == requested || device.identifier == requested || device.deviceProperties?.name == requested);
                if (match != null) return match;

                throw new MeasureException($"The iOS device {requested} is not available: plug it in, unlock it and trust this Mac. Available: {(reachable.Count == 0 ? "none" : string.Join(", ", reachable.Select(Describe)))}.");
            }

            if (reachable.Count == 1) return reachable[0];
            if (reachable.Count > 1) throw new MeasureException($"Several iOS devices are available ({string.Join(", ", reachable.Select(Describe))}): choose one.");

            throw new MeasureException("No iOS device available: plug it in, unlock it, trust this Mac and turn Developer Mode on.");
        }

        /// <summary>
        ///     The physical devices that devicectl can reach, which the measure can run on.
        /// </summary>
        public static List<DevicectlJson.Device> GetReachable(IReadOnlyList<DevicectlJson.Device> devices)
        {
            return devices.Where(device => device.hardwareProperties?.reality == PHYSICAL && device.connectionProperties?.tunnelState != TUNNEL_UNAVAILABLE).ToList();
        }

        /// <summary>
        ///     The device as a measure lists it: its UDID, and its name followed by its model.
        /// </summary>
        public static ListedDevice ToListedDevice(DevicectlJson.Device device)
        {
            var name = device.deviceProperties?.name;
            var model = string.IsNullOrEmpty(device.hardwareProperties.marketingName) ? device.hardwareProperties.productType : device.hardwareProperties.marketingName;
            return new ListedDevice(device.hardwareProperties.udid, string.IsNullOrEmpty(name) ? model : string.IsNullOrEmpty(model) ? name : $"{name}, {model}");
        }

        public static string GetModel(DevicectlJson.Device device)
        {
            var hardware = device.hardwareProperties;
            return string.IsNullOrEmpty(hardware.marketingName) ? hardware.productType : $"{hardware.marketingName} ({hardware.productType})";
        }

        public static string GetOsVersion(DevicectlJson.Device device)
        {
            return $"{device.hardwareProperties.platform} {device.deviceProperties?.osVersionNumber}".Trim();
        }

        /// <summary>
        ///     Where xcodebuild puts the app of a device build, in its derived data.
        /// </summary>
        public static string GetProductsFolder(string derivedData)
        {
            return Path.Combine(derivedData, "Build", "Products", CONFIGURATION + "-iphoneos");
        }

        /// <summary>
        ///     The arguments of a devicectl command that writes its result to a JSON file, and nothing else.
        /// </summary>
        public static string[] Devicectl(string jsonOutput, params string[] command)
        {
            return new[] { "devicectl" }.Concat(command).Concat(new[] { "--json-output", jsonOutput, "--quiet" }).ToArray();
        }

        public static string[] ListDevices()
        {
            return new[] { "list", "devices" };
        }

        public static string[] LockState(string udid)
        {
            return new[] { "device", "info", "lockState", "--device", udid };
        }

        public static string[] Uninstall(string udid, string bundleId)
        {
            return new[] { "device", "uninstall", "app", "--device", udid, bundleId };
        }

        public static string[] Install(string udid, string app)
        {
            return new[] { "device", "install", "app", "--device", udid, app };
        }

        public static string[] CopyTo(string udid, string bundleId, string source, string destination)
        {
            return new[] { "device", "copy", "to", "--device", udid, "--domain-type", "appDataContainer", "--domain-identifier", bundleId, "--source", source, "--destination", destination };
        }

        public static string[] CopyFrom(string udid, string bundleId, string source, string destination)
        {
            return new[] { "device", "copy", "from", "--device", udid, "--domain-type", "appDataContainer", "--domain-identifier", bundleId, "--source", source, "--destination", destination };
        }

        public static string[] Launch(string udid, string bundleId)
        {
            return new[] { "device", "process", "launch", "--device", udid, "--terminate-existing", bundleId };
        }

        public static string[] Terminate(string udid, int processId)
        {
            return new[] { "device", "process", "terminate", "--device", udid, "--pid", processId.ToString(CultureInfo.InvariantCulture) };
        }

        /// <summary>
        ///     The xcrun arguments that build the Xcode project of Unity for devices, signed by the team, without archive.
        /// </summary>
        public static string[] XcodeBuild(string project, string derivedData, string team)
        {
            return new[] { "xcodebuild", "-project", project, "-scheme", SCHEME, "-configuration", CONFIGURATION, "-destination", "generic/platform=iOS", "-derivedDataPath", derivedData, "-allowProvisioningUpdates", "-quiet", "build", $"DEVELOPMENT_TEAM={team}", "CODE_SIGN_STYLE=Automatic" };
        }

        private static string Describe(DevicectlJson.Device device)
        {
            return $"{device.deviceProperties?.name} ({device.hardwareProperties.udid})";
        }
    }
}