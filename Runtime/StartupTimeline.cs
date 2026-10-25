using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
#if STARTUP_DIAGNOSTICS_ADDRESSABLES
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Times the cold start, from the start of the process to the end of the start, and logs it in one line once it
    ///     ends, in milliseconds since the origin:
    ///     <c>[Startup] origin=process scripts_start=640 first_scene_loaded=760 first_frame=812</c>.
    ///     <list type="bullet">
    ///         <item>
    ///             The package marks <see cref="SCRIPTS_START" />, <see cref="FIRST_SCENE_LOADED" /> and
    ///             <see cref="FIRST_FRAME" />. The project adds its own milestones with <see cref="Mark" />.
    ///         </item>
    ///         <item>
    ///             The start ends at the first frame of the first scene of the build. A project whose first playable screen
    ///             comes later calls <see cref="DeferEnd" /> before that frame, then <see cref="End" /> on that screen.
    ///         </item>
    ///         <item>
    ///             The line goes to the log, and in players to the file <see cref="FILE_NAME" /> of the temporary cache: the
    ///             log of iOS is out of reach without a debugger.
    ///         </item>
    ///         <item>
    ///             The origin is the start of the process on Android and iOS, the start of Play Mode in the editor, and the
    ///             start of the engine elsewhere.
    ///         </item>
    ///     </list>
    /// </summary>
    public static class StartupTimeline
    {
        public const string LOG_PREFIX = "[Startup]";
        public const string FILE_NAME = "startup.txt";
        public const string SCRIPTS_START = "scripts_start";
        public const string FIRST_SCENE_LOADED = "first_scene_loaded";
        public const string FIRST_FRAME = "first_frame";
        public const string ORIGIN_PROCESS = "process";
        public const string ORIGIN_PLAY_MODE = "play_mode";
        public const string ORIGIN_ENGINE = "engine";

        private static Timeline _timeline = new();
#if UNITY_EDITOR
        private static double _playModeStart;
#endif

        /// <summary>
        ///     Whether the start has ended and its line has been logged.
        /// </summary>
        public static bool IsEnded => _timeline.IsEnded;

        /// <summary>
        ///     The time of the milestones, in seconds, on a monotonic clock of the system. Not
        ///     <see cref="Time.realtimeSinceStartupAsDouble" />, which goes back during the start (seen on iOS): a milestone
        ///     recorded before the first scene loads would come after later ones.
        /// </summary>
        private static double Now => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;

        /// <summary>
        ///     Records a milestone of the start, at the current time. Ignored once the start has ended.
        /// </summary>
        public static void Mark(string milestone)
        {
            _timeline.Mark(milestone, Now);
        }

        /// <summary>
        ///     Records a milestone once the current frame is rendered.
        /// </summary>
        public static async Awaitable MarkAfterRenderingAsync(string milestone)
        {
            await AfterRendering.WaitAsync();
            Mark(milestone);
        }

        /// <summary>
        ///     Keeps the start going after the first frame of the first scene, until <see cref="End" />. Call it before that
        ///     frame is rendered, from <c>Awake</c> or <c>Start</c> of a component of the first scene, or from a
        ///     <see cref="RuntimeInitializeOnLoadMethodAttribute" /> at <see cref="RuntimeInitializeLoadType.BeforeSceneLoad" />
        ///     or later: the timeline starts over at <see cref="RuntimeInitializeLoadType.SubsystemRegistration" />.
        /// </summary>
        public static void DeferEnd()
        {
            _timeline.DeferEnd();
        }

        /// <summary>
        ///     Ends the start with a last milestone, at the current time, and logs the timeline. Logs a warning instead if
        ///     the start has already ended.
        /// </summary>
        public static void End(string milestone)
        {
            if (!_timeline.End(milestone, Now))
            {
                Debug.LogWarning($"{LOG_PREFIX} The start has already ended, {milestone} is left out. To end it after the first frame, call {nameof(StartupTimeline)}.{nameof(DeferEnd)} before that frame.");
                return;
            }

            Log();
        }

        /// <summary>
        ///     Ends the start once the current frame is rendered, with a last milestone.
        /// </summary>
        public static async Awaitable EndAfterRenderingAsync(string milestone)
        {
            await AfterRendering.WaitAsync();
            End(milestone);
        }

        /// <summary>
        ///     Records when a scene loaded by <see cref="UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(string)" /> is
        ///     loaded, then ends the start once its first frame is rendered. Not tied to the scene that starts the loading,
        ///     which may be unloaded meanwhile.
        /// </summary>
        public static async Awaitable EndAtFirstFrameAsync(AsyncOperation loading, string loadedMilestone, string endMilestone)
        {
            while (!loading.isDone)
            {
                await Awaitable.NextFrameAsync();
            }

            Mark(loadedMilestone);
            await EndAfterRenderingAsync(endMilestone);
        }

#if STARTUP_DIAGNOSTICS_ADDRESSABLES
        /// <summary>
        ///     Records when a scene loaded by Addressables is loaded, then ends the start once its first frame is rendered.
        ///     Nothing is recorded if the loading fails. Not tied to the scene that starts the loading, which may be unloaded
        ///     meanwhile.
        /// </summary>
        public static async Awaitable EndAtFirstFrameAsync(AsyncOperationHandle<SceneInstance> loading, string loadedMilestone, string endMilestone)
        {
            while (!loading.IsDone)
            {
                await Awaitable.NextFrameAsync();
            }

            if (loading.Status != AsyncOperationStatus.Succeeded) return;

            Mark(loadedMilestone);
            await EndAfterRenderingAsync(endMilestone);
        }
#endif

        /// <summary>
        ///     The log line of the timeline: the origin, then each milestone in milliseconds since the origin, rounded.
        /// </summary>
        internal static string FormatTimeline(string originName, double origin, IReadOnlyList<KeyValuePair<string, double>> milestones)
        {
            var line = new StringBuilder(LOG_PREFIX).Append(" origin=").Append(originName);
            foreach (var milestone in milestones)
            {
                var milliseconds = Math.Round((milestone.Value - origin) * 1000, MidpointRounding.AwayFromZero);
                line.Append(' ').Append(milestone.Key).Append('=').Append(milliseconds.ToString("0", CultureInfo.InvariantCulture));
            }

            return line.ToString();
        }

        /// <summary>
        ///     The start of the process on the clock of the milestones, in seconds, from the current time on that clock and the
        ///     age of the process.
        /// </summary>
        internal static double GetProcessStart(double now, double processAge)
        {
            return now - processAge;
        }

        /// <summary>
        ///     Starts the timeline as the first scripts run, once the engine is initialized and before the first scene
        ///     loads.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void StartOnLoad()
        {
            _timeline = new Timeline();
#if UNITY_EDITOR
            _playModeStart = Now;
#endif
            Mark(SCRIPTS_START);
        }

        /// <summary>
        ///     Records when the first scene of the build is loaded and awake, then its first frame.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void MarkFirstSceneLoaded()
        {
            Mark(FIRST_SCENE_LOADED);
            AfterRendering.Add(MarkFirstFrame);
        }

        /// <summary>
        ///     Records the first frame, once it is rendered, and logs the timeline if it ends the start.
        /// </summary>
        private static void MarkFirstFrame()
        {
            if (_timeline.MarkFirstFrame(Now))
            {
                Log();
            }
        }

        private static void Log()
        {
            var origin = GetOrigin(out var originName);
            var line = FormatTimeline(originName, origin, _timeline.Milestones);
            Debug.Log(line);
#if !UNITY_EDITOR
            WriteFile(line);
#endif
        }

        /// <summary>
        ///     Overwrites the file of the timeline, in the temporary cache, with the line of this start.
        /// </summary>
        private static void WriteFile(string line)
        {
            try
            {
                File.WriteAllText(Path.Combine(Application.temporaryCachePath, FILE_NAME), line);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{LOG_PREFIX} Unable to write the timeline to {FILE_NAME}: {exception.Message}");
            }
        }

        private static double GetOrigin(out string originName)
        {
#if UNITY_EDITOR
            originName = ORIGIN_PLAY_MODE;
            return _playModeStart;
#else
            var processAge = GetProcessAge();
            if (double.IsNaN(processAge))
            {
                originName = ORIGIN_ENGINE;
                return Now - Time.realtimeSinceStartupAsDouble;
            }

            originName = ORIGIN_PROCESS;
            return GetProcessStart(Now, processAge);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private const int ANDROID_START_REQUESTED_SDK = 33;

        /// <summary>
        ///     Seconds since the process start was requested (Android 13 and later), or since the process started.
        /// </summary>
        private static double GetProcessAge()
        {
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                using var process = new AndroidJavaClass("android.os.Process");
                using var clock = new AndroidJavaClass("android.os.SystemClock");
                var start = version.GetStatic<int>("SDK_INT") >= ANDROID_START_REQUESTED_SDK ? process.CallStatic<long>("getStartRequestedElapsedRealtime") : process.CallStatic<long>("getStartElapsedRealtime");
                return (clock.CallStatic<long>("elapsedRealtime") - start) / 1000.0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{LOG_PREFIX} Unable to read the start of the process: {exception.Message}");
                return double.NaN;
            }
        }
#elif UNITY_IOS && !UNITY_EDITOR
        private const int CTL_KERN = 1;
        private const int KERN_PROC = 14;
        private const int KERN_PROC_PID = 1;
        private const int KINFO_PROC_SIZE = 1024;

        [DllImport("__Internal", EntryPoint = "getpid")]
        private static extern int GetPid();

        [DllImport("__Internal", EntryPoint = "sysctl")]
        private static extern int Sysctl(int[] name, uint nameLength, byte[] oldValue, ref UIntPtr oldLength, IntPtr newValue, UIntPtr newLength);

        /// <summary>
        ///     Seconds since the process started, from the kernel's kinfo_proc of this process, whose first field is the
        ///     start time: a timeval of 64-bit seconds then 32-bit microseconds since the Unix epoch.
        /// </summary>
        private static double GetProcessAge()
        {
            var name = new[] { CTL_KERN, KERN_PROC, KERN_PROC_PID, GetPid() };
            var info = new byte[KINFO_PROC_SIZE];
            var length = (UIntPtr)info.Length;
            if (Sysctl(name, (uint)name.Length, info, ref length, IntPtr.Zero, UIntPtr.Zero) != 0)
            {
                Debug.LogWarning($"{LOG_PREFIX} Unable to read the start of the process: sysctl failed");
                return double.NaN;
            }

            var start = BitConverter.ToInt64(info, 0) + BitConverter.ToInt32(info, 8) / 1e6;
            return (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds - start;
        }
#else
        private static double GetProcessAge()
        {
            return double.NaN;
        }
#endif
    }
}