using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     An Android device plugged in with USB debugging, driven by adb, with bundletool for an AAB.
    /// </summary>
    internal sealed class AndroidDevice : IMeasureDevice
    {
        private const string AAB_EXTENSION = ".aab";
        private const string DEVICE_APKS = "device.apks";
        private const int POLL_MILLISECONDS = 500;
        private const int STARTUP_TIMEOUT_SECONDS = 30;

        private static readonly TimeSpan _commandTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _launchTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan _installTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan _pullTimeout = TimeSpan.FromMinutes(5);

        private readonly AndroidTools _tools;
        private readonly string _package;
        private readonly string _requestedDevice;
        private readonly string _workFolder;
        private string _activity;

        public AndroidDevice(AndroidTools tools, string package, string requestedDevice, string workFolder)
        {
            _tools = tools;
            _package = package;
            _requestedDevice = requestedDevice;
            _workFolder = workFolder;
        }

        public string Id { get; private set; }
        public string Model { get; private set; }
        public string OsVersion { get; private set; }
        public string Network { get; private set; }

        public async Task<List<ListedDevice>> ListAsync(CancellationToken cancellation)
        {
            var devices = await ProcessRunner.RunCheckedAsync(_tools.Adb, new[] { "devices", "-l" }, _commandTimeout, cancellation);
            return AndroidCommands.ParseReadyDevices(devices);
        }

        public async Task ConnectAsync(CancellationToken cancellation)
        {
            var devices = await ProcessRunner.RunCheckedAsync(_tools.Adb, new[] { "devices" }, _commandTimeout, cancellation);
            Id = AndroidCommands.SelectDevice(AndroidCommands.ParseDevices(devices), _requestedDevice);
            await CheckUnlockedAsync(cancellation);
            Model = (await ShellAsync(cancellation, "getprop", "ro.product.model")).Trim();
            OsVersion = "Android " + (await ShellAsync(cancellation, "getprop", "ro.build.version.release")).Trim();
            Network = AndroidCommands.ParseNetwork(await ShellAsync(cancellation, "dumpsys", "connectivity"));
        }

        public async Task InstallAsync(string buildPath, CancellationToken cancellation)
        {
            var fullPath = Path.GetFullPath(buildPath);
            _activity = null;
            if (!string.Equals(Path.GetExtension(fullPath), AAB_EXTENSION, StringComparison.OrdinalIgnoreCase))
            {
                await ProcessRunner.RunAsync(_tools.Adb, AndroidCommands.Uninstall(Id, _package), _commandTimeout, cancellation);
                await ProcessRunner.RunCheckedAsync(_tools.Adb, AndroidCommands.Install(Id, fullPath), _installTimeout, cancellation);
                return;
            }

            if (_tools.Bundletool == null) throw new MeasureException("No bundletool in the Android module of this Unity version: build an APK instead of an AAB.");

            Directory.CreateDirectory(_workFolder);
            var apks = Path.GetFullPath(Path.Combine(_workFolder, DEVICE_APKS));
            await ProcessRunner.RunCheckedAsync(_tools.Java, AndroidCommands.BuildDeviceApks(_tools.Bundletool, fullPath, apks, _tools.Adb, Id), _installTimeout, cancellation);
            await ProcessRunner.RunAsync(_tools.Adb, AndroidCommands.Uninstall(Id, _package), _commandTimeout, cancellation);
            await ProcessRunner.RunCheckedAsync(_tools.Java, AndroidCommands.InstallApks(_tools.Bundletool, apks, _tools.Adb, Id), _installTimeout, cancellation);
        }

        public async Task<Launch> LaunchAsync(TimeSpan waitAfterEnd, CancellationToken cancellation)
        {
            var launch = await StartAsync(cancellation);
            await Task.Delay(waitAfterEnd, cancellation);
            await ShellAsync(cancellation, "am", "force-stop", _package);
            return launch;
        }

        public async Task<Launch> CaptureProfilerAsync(float seconds, string capturePath, CancellationToken cancellation)
        {
            var folder = AndroidCommands.GetDataFolder(_package);
            var request = Path.GetFullPath(Path.Combine(_workFolder, StartupProfilerCapture.REQUEST_FILE));
            Directory.CreateDirectory(_workFolder);
            File.WriteAllText(request, seconds.ToString(CultureInfo.InvariantCulture));
            await ShellAsync(cancellation, "mkdir", "-p", folder);
            await ShellAsync(cancellation, "rm", "-f", $"{folder}/{StartupProfilerCapture.CAPTURE_FILE}", $"{folder}/{StartupProfilerCapture.DONE_FILE}");
            await ProcessRunner.RunCheckedAsync(_tools.Adb, AndroidCommands.Push(Id, request, $"{folder}/{StartupProfilerCapture.REQUEST_FILE}"), _commandTimeout, cancellation);
            var launch = await StartAsync(cancellation);
            await WaitForCaptureAsync(folder, seconds, cancellation);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(capturePath)));
            await ProcessRunner.RunCheckedAsync(_tools.Adb, AndroidCommands.Pull(Id, $"{folder}/{StartupProfilerCapture.CAPTURE_FILE}", Path.GetFullPath(capturePath)), _pullTimeout, cancellation);
            await ShellAsync(cancellation, "am", "force-stop", _package);
            return launch;
        }

        /// <summary>
        ///     Kills the game, launches it after the pause, and returns its timeline once the start has ended, with what the
        ///     OS reports.
        /// </summary>
        private async Task<Launch> StartAsync(CancellationToken cancellation)
        {
            var activity = await GetActivityAsync(cancellation);
            await CheckUnlockedAsync(cancellation);
            await ShellAsync(cancellation, "am", "force-stop", _package);
            await Task.Delay(StartupMeasure.PAUSE_BEFORE_LAUNCH_MILLISECONDS, cancellation);
            await ProcessRunner.RunAsync(_tools.Adb, AndroidCommands.ClearLog(Id), _commandTimeout, cancellation);
            var report = await ProcessRunner.RunCheckedAsync(_tools.Adb, AndroidCommands.Shell(Id, "am", "start", "-W", "-n", activity), _launchTimeout, cancellation);
            var launch = await WaitForTimelineAsync(cancellation);
            AndroidCommands.ReadLaunchReport(report, launch);
            return launch;
        }

        /// <summary>
        ///     Wakes the screen up, then fails if the lock screen shows: the game would not start behind it. Done before
        ///     the game is killed and the device settles, for <c>dumpsys window</c> not to slow the launch down.
        /// </summary>
        private async Task CheckUnlockedAsync(CancellationToken cancellation)
        {
            await ProcessRunner.RunAsync(_tools.Adb, AndroidCommands.Shell(Id, "input", "keyevent", "KEYCODE_WAKEUP"), _commandTimeout, cancellation);
            var window = await ProcessRunner.RunAsync(_tools.Adb, AndroidCommands.Shell(Id, "dumpsys", "window"), _commandTimeout, cancellation);
            if (AndroidCommands.IsLocked(window.Output)) throw new MeasureException("The Android device is locked: unlock it and keep its screen on during the measure.");
        }

        private async Task<string> GetActivityAsync(CancellationToken cancellation)
        {
            if (_activity != null) return _activity;

            _activity = AndroidCommands.ParseActivity(await ShellAsync(cancellation, "cmd", "package", "resolve-activity", "--brief", _package));
            if (_activity == null) throw new MeasureException($"{_package} is not installed on the device: measure with a clean install.");

            return _activity;
        }

        private async Task<Launch> WaitForTimelineAsync(CancellationToken cancellation)
        {
            var waiting = Stopwatch.StartNew();
            while (true)
            {
                var log = await ProcessRunner.RunAsync(_tools.Adb, AndroidCommands.ReadUnityLog(Id), _commandTimeout, cancellation);
                if (StartupLine.TryParseLast(log.Output, out var launch)) return launch;
                if (waiting.Elapsed.TotalSeconds > STARTUP_TIMEOUT_SECONDS) throw new MeasureException($"No {StartupTimeline.LOG_PREFIX} line within {STARTUP_TIMEOUT_SECONDS} s: keep the device unlocked with its screen on, and check that the build has the Startup Diagnostics package.");

                await Task.Delay(POLL_MILLISECONDS, cancellation);
            }
        }

        /// <summary>
        ///     Waits for the game to write the end of its Profiler capture, a few seconds after its start.
        /// </summary>
        private async Task WaitForCaptureAsync(string folder, float seconds, CancellationToken cancellation)
        {
            var waiting = Stopwatch.StartNew();
            var timeout = seconds + STARTUP_TIMEOUT_SECONDS;
            while (true)
            {
                var done = await ProcessRunner.RunAsync(_tools.Adb, AndroidCommands.Shell(Id, "cat", $"{folder}/{StartupProfilerCapture.DONE_FILE}"), _commandTimeout, cancellation);
                if (done.ExitCode == 0 && done.Output.Trim() == StartupProfilerCapture.DONE_CONTENT) return;
                if (waiting.Elapsed.TotalSeconds > timeout) throw new MeasureException($"No Profiler capture within {timeout:0} s: the game on the device must be a development build with the Startup Diagnostics package.");

                await Task.Delay(POLL_MILLISECONDS, cancellation);
            }
        }

        private Task<string> ShellAsync(CancellationToken cancellation, params string[] command)
        {
            return ProcessRunner.RunCheckedAsync(_tools.Adb, AndroidCommands.Shell(Id, command), _commandTimeout, cancellation);
        }
    }
}