using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditorInternal;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Measures the cold start of the game on a device plugged in, from the <c>[Startup]</c> line of
    ///     <see cref="StartupTimeline" />:
    ///     <list type="number">
    ///         <item>checks that the device is plugged in and unlocked;</item>
    ///         <item>builds the game, unless measuring a build already there (<see cref="MeasureSettings.Rebuild" />);</item>
    ///         <item>
    ///             uninstalls and installs the game, which deletes its data on the device, then launches it once
    ///             (<see cref="MeasureSettings.CleanInstall" />);
    ///         </item>
    ///         <item>
    ///             launches it N times, killed 5 s before each launch, and sums up each milestone over these cold launches;
    ///             or, for a Profiler capture (<see cref="MeasureSettings.ProfilerCapture" />), launches a development build
    ///             once more to record the Profiler of its start, copies the capture next to the result and opens it.
    ///         </item>
    ///     </list>
    ///     Each measure writes its progress and its result to a JSON file of <see cref="RESULTS_FOLDER" />. Script reloads
    ///     wait for the end of the measure.
    /// </summary>
    public static class StartupMeasure
    {
        public const string RESULTS_FOLDER = "Library/StartupDiagnostics";

        // From the kill of the game to its next launch. A Pixel 6 relaunched 2 s after the kill is still warm from the
        // previous launch and starts the game 190 ms faster than a device left alone; from 4.5 s on, the pause no longer
        // changes the start.
        internal const int PAUSE_BEFORE_LAUNCH_MILLISECONDS = 5000;

        internal const string LOG_PREFIX = "[Startup Diagnostics]";

        private const string WORK_FOLDER = "Temp/StartupDiagnostics";
        private const string PROGRESS_NAME = "Startup measure";

        private static readonly TimeSpan _gitTimeout = TimeSpan.FromSeconds(10);

        // The loading of the Load button of the Profiler window, internal: null if a version of Unity changes it.
        private static readonly MethodInfo _loadProfilingData = typeof(ProfilerWindow).GetMethod("LoadProfilingData", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool), typeof(string), typeof(bool) }, null);

        /// <summary>
        ///     The last measure started in this editor session, running or done, or null.
        /// </summary>
        public static MeasureRun Current { get; private set; }

        public static bool IsRunning => Current != null && !Current.IsDone;

        /// <summary>
        ///     Starts a measure and returns at once; <see cref="MeasureRun.Completion" /> ends with its result. Throws an
        ///     <see cref="InvalidOperationException" /> if a measure is already running.
        /// </summary>
        public static MeasureRun Start(MeasureSettings settings)
        {
            if (IsRunning) throw new InvalidOperationException("A startup measure is already running: wait for its end or cancel it.");

            var startedAt = DateTime.UtcNow;
            var result = new MeasureResult { Platform = settings.Platform.ToString(), StartedAt = startedAt.ToString("o", CultureInfo.InvariantCulture), UnityVersion = Application.unityVersion, Runs = settings.ProfilerCapture ? 0 : settings.Runs, CleanInstall = settings.CleanInstall, ProfilerCapture = settings.ProfilerCapture, ProfilerSeconds = settings.ProfilerCapture ? settings.ProfilerSeconds : 0 };
            var fileName = startedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + settings.Platform.ToString().ToLowerInvariant() + ".json";
            var run = new MeasureRun(settings, result, Path.Combine(RESULTS_FOLDER, fileName), startedAt);
            Current = run;
            run.Completion = RunAsync(run);
            return run;
        }

        /// <summary>
        ///     Runs a measure from the command line, with the editor closed, then quits the editor with the exit code 0 if
        ///     the measure succeeded and 1 otherwise:
        ///     <c>Unity -batchmode -projectPath &lt;project&gt; -buildTarget Android -executeMethod Javienne.StartupDiagnostics.StartupMeasure.RunFromCommandLine</c>,
        ///     without <c>-quit</c>, which would quit before the measure. Options: <c>-startupPlatform android|ios</c> (the
        ///     active build target by default), <c>-startupDevice &lt;id&gt;</c>, <c>-startupRuns &lt;N&gt;</c>,
        ///     <c>-startupBuild &lt;path&gt;</c>, <c>-startupNoBuild</c> (measure the build already there),
        ///     <c>-startupNoInstall</c> (measure the game on the device, without building nor installing it),
        ///     <c>-startupProfilerCapture</c> (capture the Profiler instead) and <c>-startupProfilerSeconds &lt;s&gt;</c>.
        /// </summary>
        public static void RunFromCommandLine()
        {
            MeasureRun run;
            try
            {
                run = Start(MeasureCommandLine.Parse(Environment.GetCommandLineArgs(), EditorUserBuildSettings.activeBuildTarget));
            }
            catch (MeasureException exception)
            {
                Debug.LogError($"{LOG_PREFIX} {exception.Message}");
                EditorApplication.Exit(1);
                return;
            }

            _ = ExitWhenDoneAsync(run);
        }

        private static async Task<MeasureResult> RunAsync(MeasureRun run)
        {
            var settings = run.Settings;
            var result = run.Result;
            var cancellation = run.Cancellation;
            var progress = Progress.Start(PROGRESS_NAME, settings.Platform.ToString());
            Progress.RegisterCancelCallback(progress, () =>
            {
                run.Cancel();
                return true;
            });
            EditorApplication.LockReloadAssemblies();
            try
            {
                var issue = GetBlockingIssue(settings);
                if (issue != null) throw new MeasureException(issue);

                result.ApplicationIdentifier = GetApplicationIdentifier(settings.Platform);
                var device = CreateDevice(settings);
                Report(run, progress, "Checking the device");
                await device.ConnectAsync(cancellation);
                result.DeviceId = device.Id;
                result.DeviceModel = device.Model;
                result.OsVersion = device.OsVersion;
                result.Network = device.Network;
                result.Commit = await GetCommitAsync(cancellation);
                result.BuildPath = settings.Rebuild || settings.CleanInstall ? GetBuildPath(settings) : string.Empty;
                if (settings.Rebuild)
                {
                    Report(run, progress, "Building");
                    await MeasureBuild.BuildAsync(settings.Platform, result.BuildPath, settings.ProfilerCapture, cancellation);
                    result.Rebuilt = true;
                }

                if (settings.CleanInstall)
                {
                    Report(run, progress, "Installing");
                    await device.InstallAsync(result.BuildPath, cancellation);
                    Report(run, progress, "First launch");
                    result.FirstLaunch = await LaunchAsync(device, settings.FirstLaunchWait, cancellation);
                }

                if (settings.ProfilerCapture)
                {
                    Report(run, progress, "Profiler capture");
                    var capturePath = Path.ChangeExtension(run.ResultPath, StartupProfilerCapture.CAPTURE_EXTENSION).Replace('\\', '/');
                    result.CaptureLaunch = await Task.Run(() => device.CaptureProfilerAsync(settings.ProfilerSeconds, capturePath, cancellation), cancellation);
                    result.CapturePath = capturePath;
                }

                for (var i = 0; i < result.Runs; i++)
                {
                    Report(run, progress, $"Cold launch {i + 1}/{settings.Runs}");
                    result.ColdLaunches.Add(await LaunchAsync(device, settings.LaunchWait, cancellation));
                    result.ColdSummary = LaunchStatistics.Summarize(result.ColdLaunches);
                }

                result.Status = MeasureStatus.SUCCEEDED;
            }
            catch (OperationCanceledException)
            {
                result.Status = MeasureStatus.CANCELLED;
            }
            catch (MeasureException exception)
            {
                result.Status = MeasureStatus.FAILED;
                result.Error = exception.Message;
            }
            catch (Exception exception)
            {
                result.Status = MeasureStatus.FAILED;
                result.Error = exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
            }

            result.FinishedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            run.Save();
            Progress.Finish(progress, GetProgressStatus(result.Status));
            LogEnd(run);
            if (result.Status == MeasureStatus.SUCCEEDED && result.ProfilerCapture && !Application.isBatchMode)
            {
                OpenProfilerCapture(result.CapturePath);
            }

            return result;
        }

        /// <summary>
        ///     Opens a Profiler capture of a measure in the Profiler window, in place of the frames it shows, and stops its
        ///     recording. Returns false if the capture does not load.
        /// </summary>
        public static bool OpenProfilerCapture(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

            var fullPath = Path.GetFullPath(path);
            var window = EditorWindow.GetWindow<ProfilerWindow>();
            if (_loadProfilingData != null)
            {
                // As the Load button of the window, which also stops its recording and shows the name of the file.
                _loadProfilingData.Invoke(window, new object[] { false, fullPath, false });
                return ProfilerDriver.lastFrameIndex >= 0;
            }

            if (!ProfilerDriver.LoadProfile(fullPath, false)) return false;

            ProfilerDriver.enabled = false;
            return true;
        }

        /// <summary>
        ///     Launches the game off the main thread: the pause before the launch and the reads of its log must not wait
        ///     for the next update of the editor, which comes 400 ms later or more when the editor is in the background.
        /// </summary>
        private static Task<Launch> LaunchAsync(IMeasureDevice device, float waitAfterEnd, CancellationToken cancellation)
        {
            return Task.Run(() => device.LaunchAsync(TimeSpan.FromSeconds(waitAfterEnd), cancellation), cancellation);
        }

        /// <summary>
        ///     Why the measure cannot run with these settings in this editor, or null if it can.
        /// </summary>
        internal static string GetBlockingIssue(MeasureSettings settings)
        {
            var buildPath = GetBuildPath(settings);
            return MeasureChecks.GetBlockingIssue(settings, buildPath, File.Exists(buildPath) || Directory.Exists(buildPath), EditorUserBuildSettings.activeBuildTarget, Application.platform == RuntimePlatform.OSXEditor);
        }

        /// <summary>
        ///     The build that the measure makes or installs: <see cref="MeasureSettings.BuildPath" />, or the default one of
        ///     the platform, development for a Profiler capture.
        /// </summary>
        internal static string GetBuildPath(MeasureSettings settings)
        {
            return string.IsNullOrEmpty(settings.BuildPath) ? MeasureBuild.GetDefaultPath(settings.Platform, settings.ProfilerCapture) : settings.BuildPath;
        }

        internal static string GetApplicationIdentifier(BuildTarget platform)
        {
            return PlayerSettings.GetApplicationIdentifier(platform == BuildTarget.iOS ? NamedBuildTarget.iOS : NamedBuildTarget.Android);
        }

        /// <summary>
        ///     The devices of the platform ready for the measure.
        /// </summary>
        internal static Task<List<ListedDevice>> ListDevicesAsync(BuildTarget platform, CancellationToken cancellation)
        {
            return CreateDevice(new MeasureSettings { Platform = platform }).ListAsync(cancellation);
        }

        /// <summary>
        ///     Finds the device of the settings and checks that it is unlocked, as the measure does first.
        /// </summary>
        internal static async Task<IMeasureDevice> CheckDeviceAsync(MeasureSettings settings, CancellationToken cancellation)
        {
            var device = CreateDevice(settings);
            await device.ConnectAsync(cancellation);
            return device;
        }

        private static IMeasureDevice CreateDevice(MeasureSettings settings)
        {
            switch (settings.Platform)
            {
                case BuildTarget.Android:
                    return new AndroidDevice(AndroidTools.Find(), GetApplicationIdentifier(settings.Platform), settings.Device, Path.Combine(WORK_FOLDER, "Android"));
                case BuildTarget.iOS:
                    return new IosDevice(GetApplicationIdentifier(settings.Platform), PlayerSettings.iOS.appleDeveloperTeamID, settings.Device, Path.Combine(WORK_FOLDER, "iOS"));
                default:
                    throw new MeasureException($"The measure runs on Android and iOS, not on {settings.Platform}.");
            }
        }

        private static void Report(MeasureRun run, int progress, string step)
        {
            run.StartStep(step);
            run.Save();
            Progress.Report(progress, run.Fraction, step);
        }

        /// <summary>
        ///     The short hash of the git commit of the project, or empty outside git.
        /// </summary>
        private static async Task<string> GetCommitAsync(CancellationToken cancellation)
        {
            try
            {
                var git = await ProcessRunner.RunAsync("git", new[] { "rev-parse", "--short", "HEAD" }, _gitTimeout, cancellation);
                return git.ExitCode == 0 ? git.Output.Trim() : string.Empty;
            }
            catch (MeasureException)
            {
                return string.Empty;
            }
        }

        private static Progress.Status GetProgressStatus(string status)
        {
            switch (status)
            {
                case MeasureStatus.SUCCEEDED:
                    return Progress.Status.Succeeded;
                case MeasureStatus.CANCELLED:
                    return Progress.Status.Canceled;
                default:
                    return Progress.Status.Failed;
            }
        }

        private static void LogEnd(MeasureRun run)
        {
            var result = run.Result;
            switch (result.Status)
            {
                case MeasureStatus.SUCCEEDED when result.ProfilerCapture:
                    Debug.Log($"{LOG_PREFIX} Profiler capture done on {result.DeviceModel}: {result.CapturePath}");
                    break;
                case MeasureStatus.SUCCEEDED:
                    var end = result.ColdSummary.LastOrDefault(summary => summary.Name != MilestoneSummary.OS_FIRST_FRAME);
                    var median = end == null ? string.Empty : $", {end.Name} median {end.Median:0} ms over {result.ColdLaunches.Count} cold launches";
                    Debug.Log($"{LOG_PREFIX} Measure done on {result.DeviceModel}{median}: {run.ResultPath}");
                    break;
                case MeasureStatus.CANCELLED:
                    Debug.Log($"{LOG_PREFIX} Measure cancelled: {run.ResultPath}");
                    break;
                default:
                    Debug.LogError($"{LOG_PREFIX} Measure failed: {result.Error}");
                    break;
            }
        }

        private static async Task ExitWhenDoneAsync(MeasureRun run)
        {
            var succeeded = false;
            try
            {
                succeeded = (await run.Completion).Status == MeasureStatus.SUCCEEDED;
            }
            finally
            {
                EditorApplication.Exit(succeeded ? 0 : 1);
            }
        }
    }
}