using System.Globalization;
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
#endif

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Records the Profiler from the first scripts on, for a few seconds, when a startup measure asks for it: in
    ///     development builds only, and only at a launch that finds <see cref="REQUEST_FILE" />, with the seconds to record,
    ///     in the persistent data path. The request is deleted as it is read. The capture goes to <see cref="CAPTURE_FILE" />
    ///     next to it, then <see cref="DONE_FILE" />, not empty, tells the measure that the capture is complete.
    /// </summary>
    internal static class StartupProfilerCapture
    {
        public const string REQUEST_FILE = "startup-profiler-request.txt";
        public const string CAPTURE_EXTENSION = ".raw";
        public const string CAPTURE_FILE = CAPTURE_NAME + CAPTURE_EXTENSION;
        public const string DONE_FILE = "startup-profiler-done.txt";
        public const string DONE_CONTENT = "done";
        public const float DEFAULT_SECONDS = 6;

        // The Profiler appends the extension to the name of its log file.
        private const string CAPTURE_NAME = "startup-profiler";

        /// <summary>
        ///     The seconds to record, from the content of the request: a number above 0, or <see cref="DEFAULT_SECONDS" />.
        /// </summary>
        internal static float ParseSeconds(string request)
        {
            return float.TryParse(request?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 && !float.IsInfinity(seconds) ? seconds : DEFAULT_SECONDS;
        }

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        // The memory of the Profiler for the frames not yet written: the first frames of a start fill the default one.
        private const int MAX_USED_MEMORY = 256 * 1024 * 1024;

        private static string _folder;
        private static double _end;

        /// <summary>
        ///     Starts the capture as the first scripts run, if the measure asked for one.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void StartOnLoad()
        {
            _end = 0;
            string request;
            try
            {
                _folder = Application.persistentDataPath;
                var requestPath = Path.Combine(_folder, REQUEST_FILE);
                if (!File.Exists(requestPath)) return;

                request = File.ReadAllText(requestPath);
                File.Delete(requestPath);
                File.Delete(Path.Combine(_folder, DONE_FILE));
                File.Delete(Path.Combine(_folder, CAPTURE_FILE));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{StartupTimeline.LOG_PREFIX} Unable to read the request of a Profiler capture: {exception.Message}");
                return;
            }

            _end = StartupTimeline.Now + ParseSeconds(request);
            Profiler.maxUsedMemory = MAX_USED_MEMORY;
            Profiler.logFile = Path.Combine(_folder, CAPTURE_NAME);
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void FollowOnLoad()
        {
            if (_end > 0)
            {
                AfterRendering.Add(Follow);
            }
        }

        /// <summary>
        ///     Keeps the Profiler on after each frame until the end of the capture, then stops it.
        /// </summary>
        private static void Follow()
        {
            if (StartupTimeline.Now < _end)
            {
                if (!Profiler.enabled)
                {
                    Profiler.enabled = true;
                }

                AfterRendering.Add(Follow);
                return;
            }

            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = string.Empty;
            _end = 0;
            // One frame later, for the last frames to be written.
            AfterRendering.Add(WriteDone);
        }

        private static void WriteDone()
        {
            try
            {
                File.WriteAllText(Path.Combine(_folder, DONE_FILE), DONE_CONTENT);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{StartupTimeline.LOG_PREFIX} Unable to write the end of the Profiler capture: {exception.Message}");
            }
        }
#endif
    }
}