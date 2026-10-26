using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The adb and bundletool commands of the measure on Android, and how their output reads.
    /// </summary>
    internal static class AndroidCommands
    {
        public const string STATE_DEVICE = "device";
        public const string STATE_UNAUTHORIZED = "unauthorized";
        public const string UNITY_LOG = "Unity:V";

        private const string MODEL_FIELD = "model:";

        private static readonly Regex _locked = new(@"(?:mShowingLockscreen|isKeyguardShowing|mKeyguardShowing)=true");
        private static readonly Regex _totalTime = new(@"TotalTime: (\d+)");
        private static readonly Regex _launchState = new(@"LaunchState: (\w+)");
        private static readonly Regex _defaultNetwork = new(@"Active default network: (\S+)");

        /// <summary>
        ///     The serial and the state of each device of <c>adb devices</c>.
        /// </summary>
        public static List<(string Serial, string State)> ParseDevices(string output)
        {
            var devices = new List<(string Serial, string State)>();
            foreach (var line in output.Split('\n'))
            {
                var columns = line.Trim().Split('\t');
                if (columns.Length == 2)
                {
                    devices.Add((columns[0], columns[1]));
                }
            }

            return devices;
        }

        /// <summary>
        ///     The devices of <c>adb devices -l</c> ready for the measure, named after their model.
        /// </summary>
        public static List<ListedDevice> ParseReadyDevices(string output)
        {
            var devices = new List<ListedDevice>();
            foreach (var line in output.Split('\n'))
            {
                var columns = line.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 2 || columns[1] != STATE_DEVICE) continue;

                var model = columns.FirstOrDefault(column => column.StartsWith(MODEL_FIELD, StringComparison.Ordinal));
                devices.Add(new ListedDevice(columns[0], model == null ? columns[0] : model.Substring(MODEL_FIELD.Length).Replace('_', ' ')));
            }

            return devices;
        }

        /// <summary>
        ///     The serial of the device to measure on: the requested one, or else the only device ready. Throws a
        ///     <see cref="MeasureException" /> saying what to do when there is none or several.
        /// </summary>
        public static string SelectDevice(IReadOnlyList<(string Serial, string State)> devices, string requested)
        {
            var ready = devices.Where(device => device.State == STATE_DEVICE).Select(device => device.Serial).ToList();
            if (!string.IsNullOrEmpty(requested))
            {
                if (ready.Contains(requested)) return requested;

                throw new MeasureException($"The Android device {requested} is not ready: plug it in and allow USB debugging. Ready: {(ready.Count == 0 ? "none" : string.Join(", ", ready))}.");
            }

            if (ready.Count == 1) return ready[0];
            if (ready.Count > 1) throw new MeasureException($"Several Android devices are plugged in ({string.Join(", ", ready)}): choose one.");
            if (devices.Any(device => device.State == STATE_UNAUTHORIZED)) throw new MeasureException("The Android device does not allow USB debugging from this computer: allow it on the device.");

            throw new MeasureException("No Android device found: plug it in with USB debugging allowed.");
        }

        /// <summary>
        ///     Whether <c>dumpsys window</c> shows the lock screen.
        /// </summary>
        public static bool IsLocked(string dumpsysWindow)
        {
            return _locked.IsMatch(dumpsysWindow);
        }

        /// <summary>
        ///     The launcher activity from <c>cmd package resolve-activity --brief</c>, as <c>package/activity</c>, or null if
        ///     the package is not installed.
        /// </summary>
        public static string ParseActivity(string output)
        {
            var last = output.Trim().Split('\n').Last().Trim();
            return last.Contains("/") ? last : null;
        }

        /// <summary>
        ///     Adds to the launch what <c>am start -W</c> reports: the first frame of the app and the kind of launch.
        /// </summary>
        public static void ReadLaunchReport(string amStartOutput, Launch launch)
        {
            var totalTime = _totalTime.Match(amStartOutput);
            if (totalTime.Success)
            {
                launch.OsFirstFrame = int.Parse(totalTime.Groups[1].Value);
            }

            var state = _launchState.Match(amStartOutput);
            if (state.Success)
            {
                launch.OsLaunchState = state.Groups[1].Value;
            }
        }

        /// <summary>
        ///     Whether <c>dumpsys connectivity</c> shows a default network.
        /// </summary>
        public static string ParseNetwork(string dumpsysConnectivity)
        {
            var network = _defaultNetwork.Match(dumpsysConnectivity);
            if (!network.Success) return MeasureResult.NETWORK_UNKNOWN;

            return network.Groups[1].Value == "none" ? MeasureResult.NETWORK_OFFLINE : MeasureResult.NETWORK_ONLINE;
        }

        public static string[] Shell(string serial, params string[] command)
        {
            return new[] { "-s", serial, "shell" }.Concat(command).ToArray();
        }

        public static string[] Install(string serial, string apk)
        {
            return new[] { "-s", serial, "install", apk };
        }

        public static string[] Uninstall(string serial, string package)
        {
            return new[] { "-s", serial, "uninstall", package };
        }

        public static string[] ClearLog(string serial)
        {
            return new[] { "-s", serial, "logcat", "-c" };
        }

        /// <summary>
        ///     The log of Unity since it was last cleared, without waiting for more.
        /// </summary>
        public static string[] ReadUnityLog(string serial)
        {
            return new[] { "-s", serial, "logcat", "-d", "-s", UNITY_LOG };
        }

        /// <summary>
        ///     The java arguments that make, from an AAB, the APKs Google Play would serve to the device.
        /// </summary>
        public static string[] BuildDeviceApks(string bundletool, string aab, string apks, string adb, string serial)
        {
            return new[] { "-jar", bundletool, "build-apks", $"--bundle={aab}", $"--output={apks}", "--overwrite", "--connected-device", $"--device-id={serial}", $"--adb={adb}" };
        }

        public static string[] InstallApks(string bundletool, string apks, string adb, string serial)
        {
            return new[] { "-jar", bundletool, "install-apks", $"--apks={apks}", $"--device-id={serial}", $"--adb={adb}" };
        }
    }
}