using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
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
    ///         <item>launches it N times, killed before each launch, and sums up each milestone over these cold launches.</item>
    ///     </list>
    ///     Each measure writes its progress and its result to a JSON file of <see cref="RESULTS_FOLDER" />. Script reloads
    ///     wait for the end of the measure.
    /// </summary>
    public static class StartupMeasure
    {
        public const string RESULTS_FOLDER = "Library/StartupDiagnostics";

        private const string WORK_FOLDER = "Temp/StartupDiagnostics";
        private const string LOG_PREFIX = "[Startup Diagnostics]";
        private const string PROGRESS_NAME = "Startup measure";

        private static readonly TimeSpan _gitTimeout = TimeSpan.FromSeconds(10);

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
            var result = new MeasureResult { Platform = settings.Platform.ToString(), StartedAt = startedAt.ToString("o", CultureInfo.InvariantCulture), UnityVersion = Application.unityVersion, Runs = settings.Runs, CleanInstall = settings.CleanInstall };
            var fileName = startedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + settings.Platform.ToString().ToLowerInvariant() + ".json";
            var run = new MeasureRun(settings, result, Path.Combine(RESULTS_FOLDER, fileName));
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
        ///     <c>-startupBuild &lt;path&gt;</c>, <c>-startupNoBuild</c> (measure the build already there) and
        ///     <c>-startupNoInstall</c>.
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
                if (settings.Runs < 1) throw new MeasureException("The number of cold launches is a whole number above 0.");

                var device = CreateDevice(settings, result);
                Report(run, progress, "Checking the device", 0);
                await device.ConnectAsync(cancellation);
                result.DeviceId = device.Id;
                result.DeviceModel = device.Model;
                result.OsVersion = device.OsVersion;
                result.Network = device.Network;
                result.Commit = await GetCommitAsync(cancellation);
                result.BuildPath = string.IsNullOrEmpty(settings.BuildPath) ? MeasureBuild.GetDefaultPath(settings.Platform) : settings.BuildPath;
                if (settings.Rebuild)
                {
                    Report(run, progress, "Building", 0);
                    await MeasureBuild.BuildAsync(settings.Platform, result.BuildPath, cancellation);
                    result.Rebuilt = true;
                }
                else if (!File.Exists(result.BuildPath) && !Directory.Exists(result.BuildPath))
                {
                    throw new MeasureException($"No build at {result.BuildPath}: measure with a new build.");
                }

                var firstLaunches = settings.CleanInstall ? 1 : 0;
                var launches = firstLaunches + settings.Runs;
                if (settings.CleanInstall)
                {
                    Report(run, progress, "Installing", 0);
                    await device.InstallAsync(result.BuildPath, cancellation);
                    Report(run, progress, "First launch", 0);
                    result.FirstLaunch = await device.LaunchAsync(TimeSpan.FromSeconds(settings.FirstLaunchWait), cancellation);
                }

                for (var i = 0; i < settings.Runs; i++)
                {
                    Report(run, progress, $"Cold launch {i + 1}/{settings.Runs}", (float)(firstLaunches + i) / launches);
                    result.ColdLaunches.Add(await device.LaunchAsync(TimeSpan.FromSeconds(settings.LaunchWait), cancellation));
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
            return result;
        }

        private static IMeasureDevice CreateDevice(MeasureSettings settings, MeasureResult result)
        {
            switch (settings.Platform)
            {
                case BuildTarget.Android:
                    result.ApplicationIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
                    return new AndroidDevice(AndroidTools.Find(), result.ApplicationIdentifier, settings.Device, Path.Combine(WORK_FOLDER, "Android"));
                case BuildTarget.iOS:
                    if (Application.platform != RuntimePlatform.OSXEditor) throw new MeasureException("The measure on iOS needs a Mac with Xcode.");

                    result.ApplicationIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
                    return new IosDevice(result.ApplicationIdentifier, PlayerSettings.iOS.appleDeveloperTeamID, settings.Device, Path.Combine(WORK_FOLDER, "iOS"));
                default:
                    throw new MeasureException($"The measure runs on Android and iOS, not on {settings.Platform}.");
            }
        }

        private static void Report(MeasureRun run, int progress, string step, float fraction)
        {
            run.Result.Step = step;
            run.Save();
            Progress.Report(progress, fraction, step);
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