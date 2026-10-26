using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     An iOS device that trusts this Mac, with Developer Mode on, driven by devicectl; the Xcode project of Unity is
    ///     built for it by xcodebuild. Unity logs to the system log on iOS, out of reach without a debugger: the timeline is
    ///     read from the file that <see cref="StartupTimeline" /> writes in the data container of the app.
    /// </summary>
    internal sealed class IosDevice : IMeasureDevice
    {
        private const string XCRUN = "/usr/bin/xcrun";
        private const string DEVELOPER_DIR = "DEVELOPER_DIR";
        private const string JSON_OUTPUT = "devicectl.json";
        private const string EMPTY_FILE = "empty.txt";
        private const string LOCAL_STARTUP_FILE = "startup.txt";
        private const string BUILD_LOG = "xcodebuild.log";
        private const string DERIVED_DATA = "DerivedData";
        private const int POLL_MILLISECONDS = 500;
        private const int RETRY_MILLISECONDS = 2000;
        private const int CONNECTION_ATTEMPTS = 3;
        private const int STARTUP_TIMEOUT_SECONDS = 30;
        private const int BUILD_ERROR_LINES = 30;

        private static readonly TimeSpan _commandTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan _installTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan _buildTimeout = TimeSpan.FromMinutes(30);

        private readonly string _bundleId;
        private readonly string _team;
        private readonly string _requestedDevice;
        private readonly string _workFolder;
        private readonly Dictionary<string, string> _environment = new();
        private int _processId = -1;

        public IosDevice(string bundleId, string team, string requestedDevice, string workFolder)
        {
            _bundleId = bundleId;
            _team = team;
            _requestedDevice = requestedDevice;
            _workFolder = Path.GetFullPath(workFolder);
            var developerDir = IosCommands.GetDeveloperDir(Environment.GetEnvironmentVariable(DEVELOPER_DIR), Directory.Exists(IosCommands.XCODE_DEVELOPER_DIR));
            if (developerDir != null)
            {
                _environment[DEVELOPER_DIR] = developerDir;
            }
        }

        public string Id { get; private set; }
        public string Model { get; private set; }
        public string OsVersion { get; private set; }
        public string Network => MeasureResult.NETWORK_UNKNOWN;

        public async Task<List<ListedDevice>> ListAsync(CancellationToken cancellation)
        {
            Directory.CreateDirectory(_workFolder);
            var devices = await DevicectlAsync(IosCommands.ListDevices(), true, cancellation);
            return IosCommands.GetReachable(devices.result.devices).Select(IosCommands.ToListedDevice).ToList();
        }

        public async Task ConnectAsync(CancellationToken cancellation)
        {
            Directory.CreateDirectory(_workFolder);
            var devices = await DevicectlAsync(IosCommands.ListDevices(), true, cancellation);
            var device = IosCommands.SelectDevice(devices.result.devices, _requestedDevice);
            Id = device.hardwareProperties.udid;
            Model = IosCommands.GetModel(device);
            OsVersion = IosCommands.GetOsVersion(device);
            await CheckUnlockedAsync(cancellation);
        }

        public async Task InstallAsync(string buildPath, CancellationToken cancellation)
        {
            var app = await BuildAppAsync(buildPath, cancellation);
            _processId = -1;
            await DevicectlAsync(IosCommands.Uninstall(Id, _bundleId), false, cancellation);
            var install = await DevicectlAsync(IosCommands.Install(Id, app), false, _installTimeout, cancellation);
            var error = IosCommands.GetError(install);
            if (error != null) throw new MeasureException($"Unable to install {Path.GetFileName(app)} on the iOS device: {error}");
        }

        public async Task<Launch> LaunchAsync(TimeSpan waitAfterEnd, CancellationToken cancellation)
        {
            var timeline = await StartAsync(cancellation);
            await Task.Delay(waitAfterEnd, cancellation);
            await TerminateAsync(cancellation);
            return timeline;
        }

        public async Task<Launch> CaptureProfilerAsync(float seconds, string capturePath, CancellationToken cancellation)
        {
            var request = Path.Combine(_workFolder, StartupProfilerCapture.REQUEST_FILE);
            File.WriteAllText(request, seconds.ToString(CultureInfo.InvariantCulture));
            // devicectl does not delete files: the end of an older capture is emptied instead.
            await DevicectlAsync(IosCommands.CopyTo(Id, _bundleId, GetEmptyFile(), IosCommands.DATA_FOLDER + StartupProfilerCapture.DONE_FILE), false, cancellation);
            await DevicectlAsync(IosCommands.CopyTo(Id, _bundleId, request, IosCommands.DATA_FOLDER + StartupProfilerCapture.REQUEST_FILE), true, cancellation);
            var timeline = await StartAsync(cancellation);
            await WaitForCaptureAsync(seconds, cancellation);
            var fullPath = Path.GetFullPath(capturePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.Delete(fullPath);
            await DevicectlAsync(IosCommands.CopyFrom(Id, _bundleId, IosCommands.DATA_FOLDER + StartupProfilerCapture.CAPTURE_FILE, fullPath), true, _installTimeout, cancellation);
            await TerminateAsync(cancellation);
            return timeline;
        }

        /// <summary>
        ///     Kills the game, launches it after the pause, and returns its timeline once the start has ended.
        /// </summary>
        private async Task<Launch> StartAsync(CancellationToken cancellation)
        {
            await CheckUnlockedAsync(cancellation);
            await TerminateAsync(cancellation);
            await DevicectlAsync(IosCommands.CopyTo(Id, _bundleId, GetEmptyFile(), IosCommands.STARTUP_FILE), false, cancellation);
            await Task.Delay(StartupMeasure.PAUSE_BEFORE_LAUNCH_MILLISECONDS, cancellation);
            var launch = await DevicectlAsync(IosCommands.Launch(Id, _bundleId), false, cancellation);
            var error = IosCommands.GetError(launch);
            if (error != null) throw new MeasureException($"Unable to launch {_bundleId} on the iOS device: {error}. Is it installed? Measure with a clean install.");

            _processId = launch.result?.process?.processIdentifier ?? -1;
            return await WaitForTimelineAsync(cancellation);
        }

        private string GetEmptyFile()
        {
            var empty = Path.Combine(_workFolder, EMPTY_FILE);
            File.WriteAllText(empty, string.Empty);
            return empty;
        }

        /// <summary>
        ///     Builds the Xcode project for devices, signed by the team of the Player Settings or of the project, and returns
        ///     the app. The whole output of xcodebuild goes to a log file of the work folder.
        /// </summary>
        private async Task<string> BuildAppAsync(string buildPath, CancellationToken cancellation)
        {
            var project = Path.Combine(Path.GetFullPath(buildPath), IosCommands.PROJECT);
            if (!Directory.Exists(project)) throw new MeasureException($"No Xcode project at {project}: build the game for iOS.");

            var projectFile = Path.Combine(project, "project.pbxproj");
            var team = IosCommands.GetTeam(_team, File.Exists(projectFile) ? File.ReadAllText(projectFile) : null);
            if (string.IsNullOrEmpty(team)) throw new MeasureException("No Apple team to sign the app: set Signing Team ID in the Player Settings for iOS.");

            var derivedData = Path.Combine(_workFolder, DERIVED_DATA);
            var log = Path.Combine(_workFolder, BUILD_LOG);
            var build = await ProcessRunner.RunAsync(XCRUN, IosCommands.XcodeBuild(project, derivedData, team), _buildTimeout, cancellation, _environment);
            File.WriteAllText(log, build.Output + build.Error);
            if (build.ExitCode != 0) throw new MeasureException($"xcodebuild failed ({build.ExitCode}), see {log}:\n{ProcessRunner.GetLastLines(build.Output + build.Error, BUILD_ERROR_LINES)}");

            var products = IosCommands.GetProductsFolder(derivedData);
            var app = Directory.Exists(products) ? Directory.GetDirectories(products, "*.app").FirstOrDefault() : null;
            if (app == null) throw new MeasureException($"No app in {products} after xcodebuild, see {log}.");

            return app;
        }

        /// <summary>
        ///     Fails if the device asks for its passcode: the game would not start behind the lock screen.
        /// </summary>
        private async Task CheckUnlockedAsync(CancellationToken cancellation)
        {
            var lockState = await DevicectlAsync(IosCommands.LockState(Id), true, cancellation);
            if (lockState.result.passcodeRequired) throw new MeasureException("The iOS device is locked: unlock it and keep its screen on during the measure.");
        }

        private async Task TerminateAsync(CancellationToken cancellation)
        {
            if (_processId < 0) return;

            await DevicectlAsync(IosCommands.Terminate(Id, _processId), false, cancellation);
            _processId = -1;
        }

        private async Task<Launch> WaitForTimelineAsync(CancellationToken cancellation)
        {
            var local = Path.Combine(_workFolder, LOCAL_STARTUP_FILE);
            var waiting = Stopwatch.StartNew();
            while (true)
            {
                File.Delete(local);
                await DevicectlAsync(IosCommands.CopyFrom(Id, _bundleId, IosCommands.STARTUP_FILE, local), false, cancellation);
                if (File.Exists(local) && StartupLine.TryParseLast(File.ReadAllText(local), out var launch)) return launch;
                if (waiting.Elapsed.TotalSeconds > STARTUP_TIMEOUT_SECONDS) throw new MeasureException($"No {StartupTimeline.LOG_PREFIX} timeline in {StartupTimeline.FILE_NAME} within {STARTUP_TIMEOUT_SECONDS} s: keep the device unlocked with its screen on, and check that the build has the Startup Diagnostics package.");

                await Task.Delay(POLL_MILLISECONDS, cancellation);
            }
        }

        /// <summary>
        ///     Waits for the game to write the end of its Profiler capture, a few seconds after its start.
        /// </summary>
        private async Task WaitForCaptureAsync(float seconds, CancellationToken cancellation)
        {
            var local = Path.Combine(_workFolder, StartupProfilerCapture.DONE_FILE);
            var waiting = Stopwatch.StartNew();
            var timeout = seconds + STARTUP_TIMEOUT_SECONDS;
            while (true)
            {
                File.Delete(local);
                await DevicectlAsync(IosCommands.CopyFrom(Id, _bundleId, IosCommands.DATA_FOLDER + StartupProfilerCapture.DONE_FILE, local), false, cancellation);
                if (File.Exists(local) && File.ReadAllText(local).Trim() == StartupProfilerCapture.DONE_CONTENT) return;
                if (waiting.Elapsed.TotalSeconds > timeout) throw new MeasureException($"No Profiler capture within {timeout:0} s: the game on the device must be a development build with the Startup Diagnostics package.");

                await Task.Delay(POLL_MILLISECONDS, cancellation);
            }
        }

        private Task<DevicectlJson.Output> DevicectlAsync(string[] command, bool check, CancellationToken cancellation)
        {
            return DevicectlAsync(command, check, _commandTimeout, cancellation);
        }

        /// <summary>
        ///     Runs a devicectl command and returns its JSON output, trying again when the connection to the device could
        ///     not be established. With check, throws a <see cref="MeasureException" /> if the command fails.
        /// </summary>
        private async Task<DevicectlJson.Output> DevicectlAsync(string[] command, bool check, TimeSpan timeout, CancellationToken cancellation)
        {
            var jsonOutput = Path.Combine(_workFolder, JSON_OUTPUT);
            for (var attempt = 1; ; attempt++)
            {
                File.Delete(jsonOutput);
                var run = await ProcessRunner.RunAsync(XCRUN, IosCommands.Devicectl(jsonOutput, command), timeout, cancellation, _environment);
                var output = IosCommands.Parse(File.Exists(jsonOutput) ? File.ReadAllText(jsonOutput) : null);
                if (run.ExitCode == 0 || (!check && !IosCommands.IsConnectionError(output))) return output;
                if (IosCommands.IsConnectionError(output) && attempt < CONNECTION_ATTEMPTS)
                {
                    await Task.Delay(RETRY_MILLISECONDS, cancellation);
                    continue;
                }

                if (!check) return output;

                var error = IosCommands.GetError(output) ?? ProcessRunner.GetLastLines(run.Error + run.Output, 5);
                throw new MeasureException($"devicectl {string.Join(" ", command.Take(3))} failed: {error}");
            }
        }
    }
}