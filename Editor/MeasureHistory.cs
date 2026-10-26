using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The measures written to <see cref="StartupMeasure.RESULTS_FOLDER" />, newest first, and the measure each one is
    ///     compared with.
    /// </summary>
    internal static class MeasureHistory
    {
        private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        ///     The measures of the folder, newest first. Files that do not read as a measure are left out.
        /// </summary>
        public static List<MeasureFile> Load(string folder)
        {
            var files = new List<MeasureFile>();
            if (!Directory.Exists(folder)) return files;

            foreach (var path in Directory.GetFiles(folder, "*.json"))
            {
                MeasureResult result;
                try
                {
                    result = JsonUtility.FromJson<MeasureResult>(File.ReadAllText(path));
                }
                catch (Exception)
                {
                    continue;
                }

                if (result != null && !string.IsNullOrEmpty(result.Platform))
                {
                    files.Add(new MeasureFile(path.Replace('\\', '/'), result));
                }
            }

            return SortNewestFirst(files);
        }

        public static List<MeasureFile> SortNewestFirst(IEnumerable<MeasureFile> files)
        {
            return files.OrderByDescending(file => GetStartedAt(file.Result)).ThenByDescending(file => file.Path, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        ///     The measure to compare a measure with: the newest one that succeeded before it, for the same game on the same
        ///     device. Null if there is none. Profiler captures are not compared.
        /// </summary>
        public static MeasureFile FindPrevious(IReadOnlyList<MeasureFile> newestFirst, MeasureFile shown)
        {
            if (shown == null || shown.Result.ProfilerCapture || string.IsNullOrEmpty(shown.Result.DeviceId)) return null;

            var startedAt = GetStartedAt(shown.Result);
            foreach (var file in newestFirst)
            {
                var result = file.Result;
                if (file == shown || result.Status != MeasureStatus.SUCCEEDED || result.ProfilerCapture || GetStartedAt(result) >= startedAt) continue;
                if (result.Platform == shown.Result.Platform && result.DeviceId == shown.Result.DeviceId && result.ApplicationIdentifier == shown.Result.ApplicationIdentifier) return file;
            }

            return null;
        }

        /// <summary>
        ///     The start of the measure in UTC, or <see cref="DateTime.MinValue" /> if it does not read.
        /// </summary>
        public static DateTime GetStartedAt(MeasureResult result)
        {
            return DateTime.TryParse(result.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var startedAt) ? startedAt : DateTime.MinValue;
        }

        /// <summary>
        ///     The start of the measure in the time zone, to the second.
        /// </summary>
        public static string FormatStartedAt(MeasureResult result, TimeZoneInfo zone)
        {
            var startedAt = GetStartedAt(result);
            return startedAt == DateTime.MinValue ? result.StartedAt : TimeZoneInfo.ConvertTimeFromUtc(startedAt, zone).ToString(DATE_FORMAT, CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     How long the measure took, or null while it runs.
        /// </summary>
        public static TimeSpan? GetDuration(MeasureResult result)
        {
            if (string.IsNullOrEmpty(result.FinishedAt) || !DateTime.TryParse(result.FinishedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var finishedAt)) return null;

            var startedAt = GetStartedAt(result);
            return startedAt == DateTime.MinValue ? (TimeSpan?)null : finishedAt - startedAt;
        }
    }
}