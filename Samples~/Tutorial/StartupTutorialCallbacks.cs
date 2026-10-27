using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Javienne.StartupDiagnostics.Tutorial
{
    /// <summary>
    ///     Checks of the pages of the tutorial, called by their criteria (<c>ArbitraryCriterion</c> of the Tutorial
    ///     Framework) at every editor update: each method tells whether the user did what the page asks.
    /// </summary>
    public sealed class StartupTutorialCallbacks : ScriptableObject
    {
        private const string STARTUP_ENDED_KEY = "Javienne.StartupDiagnostics.Tutorial.StartupEnded";
        private const string MEASURE_PAGE_KEY = "Javienne.StartupDiagnostics.Tutorial.MeasurePage";
        private const string CAPTURE_PAGE_KEY = "Javienne.StartupDiagnostics.Tutorial.CapturePage";
        private const double CHECK_INTERVAL_SECONDS = 1;

        private readonly Dictionary<string, (double CheckedAt, bool IsDone)> _checks = new();

        /// <summary>
        ///     Whether the start ended once in Play Mode in this editor session. It stays true out of Play Mode, so that
        ///     the page stays done.
        /// </summary>
        public bool HasStartupEnded()
        {
            if (EditorApplication.isPlaying && StartupTimeline.IsEnded)
            {
                SessionState.SetBool(STARTUP_ENDED_KEY, true);
            }

            return SessionState.GetBool(STARTUP_ENDED_KEY, false);
        }

        /// <summary>
        ///     Whether a measure of the cold start succeeded since the page asking for it was first shown.
        /// </summary>
        public bool HasNewMeasure()
        {
            return HasMeasureSince(MEASURE_PAGE_KEY, false);
        }

        /// <summary>
        ///     Whether a Profiler capture of the start succeeded since the page asking for it was first shown.
        /// </summary>
        public bool HasNewProfilerCapture()
        {
            return HasMeasureSince(CAPTURE_PAGE_KEY, true);
        }

        /// <summary>
        ///     Whether a measure of <see cref="StartupMeasure.RESULTS_FOLDER" />, with or without Profiler capture, finished
        ///     with success after the page was first shown. The files are read again at most once a second.
        /// </summary>
        private bool HasMeasureSince(string pageKey, bool profilerCapture)
        {
            var shownAt = GetFirstShownAt(pageKey);
            if (_checks.TryGetValue(pageKey, out var check) && (check.IsDone || EditorApplication.timeSinceStartup - check.CheckedAt < CHECK_INTERVAL_SECONDS)) return check.IsDone;

            var isDone = false;
            if (Directory.Exists(StartupMeasure.RESULTS_FOLDER))
            {
                foreach (var path in Directory.GetFiles(StartupMeasure.RESULTS_FOLDER, "*.json"))
                {
                    if (File.GetLastWriteTimeUtc(path) < shownAt) continue;

                    var result = Read(path);
                    if (result == null || result.Status != MeasureStatus.SUCCEEDED || result.ProfilerCapture != profilerCapture) continue;

                    if (DateTime.TryParse(result.FinishedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var finishedAt) && finishedAt.ToUniversalTime() >= shownAt)
                    {
                        isDone = true;
                        break;
                    }
                }
            }

            _checks[pageKey] = (EditorApplication.timeSinceStartup, isDone);
            return isDone;
        }

        /// <summary>
        ///     The time a page was first shown in this editor session, recorded at the first check of its criterion.
        /// </summary>
        private static DateTime GetFirstShownAt(string pageKey)
        {
            if (DateTime.TryParse(SessionState.GetString(pageKey, null), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var shownAt)) return shownAt;

            shownAt = DateTime.UtcNow;
            SessionState.SetString(pageKey, shownAt.ToString("o", CultureInfo.InvariantCulture));
            return shownAt;
        }

        /// <summary>
        ///     The measure of a JSON file, or null if it cannot be read: a measure under way may be read while it is
        ///     written.
        /// </summary>
        private static MeasureResult Read(string path)
        {
            try
            {
                return JsonUtility.FromJson<MeasureResult>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}