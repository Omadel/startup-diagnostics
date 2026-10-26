using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A measure as the window shows it: a row per milestone, compared with the previous measure.
    /// </summary>
    internal static class MeasureReport
    {
        /// <summary>
        ///     How much slower than the previous measure a milestone is flagged: 10 %, about twice the spread seen between two
        ///     series of 10 cold launches of a same build.
        /// </summary>
        public const double SLOWER_THRESHOLD = 0.1;

        /// <summary>
        ///     The milestones of the measure, in the order of the timeline, then
        ///     <see cref="MilestoneSummary.OS_FIRST_FRAME" />. Before the first cold launch, those of the first launch.
        /// </summary>
        public static List<MilestoneRow> GetRows(MeasureResult result, MeasureResult previous)
        {
            var rows = new List<MilestoneRow>();
            var steps = LaunchStatistics.SummarizeSteps(result.ColdLaunches);
            var names = result.ColdSummary.Select(summary => summary.Name).ToList();
            if (names.Count == 0 && result.HasFirstLaunch)
            {
                names = result.FirstLaunch.Milestones.Select(milestone => milestone.Name).ToList();
            }

            foreach (var name in names)
            {
                var row = new MilestoneRow { Name = name, Cold = result.ColdSummary.FirstOrDefault(summary => summary.Name == name) };
                if (name == MilestoneSummary.OS_FIRST_FRAME)
                {
                    row.FirstLaunch = result.FirstLaunch?.OsFirstFrame ?? -1;
                }
                else
                {
                    row.FirstLaunch = result.FirstLaunch != null && result.FirstLaunch.TryGetMilestone(name, out var firstLaunch) ? firstLaunch : -1;
                    row.Step = steps.TryGetValue(name, out var step) ? step : double.NaN;
                }

                row.Previous = previous?.ColdSummary.FirstOrDefault(summary => summary.Name == name);
                if (row.Cold != null && row.Previous != null && row.Previous.Median > 0)
                {
                    row.Change = (row.Cold.Median - row.Previous.Median) / row.Previous.Median;
                    row.IsSlower = row.Change > SLOWER_THRESHOLD;
                }

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        ///     What the comparison with the previous measure shows, and whether it is a warning: a milestone more than
        ///     <see cref="SLOWER_THRESHOLD" /> slower.
        /// </summary>
        public static (string Message, bool IsWarning) GetComparison(IReadOnlyList<MilestoneRow> rows, string previousLabel)
        {
            if (previousLabel == null) return ("No earlier measure of this game on this device to compare with.", false);

            var slower = rows.Where(row => row.IsSlower).ToList();
            var threshold = (SLOWER_THRESHOLD * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            if (slower.Count == 0) return ($"No milestone is more than {threshold} slower than in the measure of {previousLabel}.", false);

            return ($"Slower by more than {threshold} than in the measure of {previousLabel}: {string.Join(", ", slower.Select(row => $"{row.Name} {FormatChange(row.Change)}"))}.", true);
        }

        /// <summary>
        ///     The share of the end of the start that the step of the row takes, from 0 to 1, or 0 without a step.
        /// </summary>
        public static float GetShare(MilestoneRow row, IReadOnlyList<MilestoneRow> rows)
        {
            var end = rows.LastOrDefault(other => other.Name != MilestoneSummary.OS_FIRST_FRAME && other.Cold != null);
            if (end == null || end.Cold.Median <= 0 || double.IsNaN(row.Step)) return 0;

            return (float)Math.Max(0, Math.Min(1, row.Step / end.Cold.Median));
        }

        /// <summary>
        ///     Milliseconds as a whole number, or with one decimal for the half of a median of an even count; empty for NaN.
        /// </summary>
        public static string FormatMilliseconds(double milliseconds)
        {
            return double.IsNaN(milliseconds) ? string.Empty : milliseconds.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     A change as a signed percentage, e.g. <c>+12%</c>; empty for NaN.
        /// </summary>
        public static string FormatChange(double change)
        {
            if (double.IsNaN(change)) return string.Empty;

            var percent = Math.Round(change * 100, MidpointRounding.AwayFromZero);
            if (percent == 0)
            {
                percent = 0;
            }

            return (percent > 0 ? "+" : string.Empty) + percent.ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        ///     A duration in minutes and seconds, e.g. <c>6 min 23 s</c>.
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            var seconds = (int)Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero);
            return seconds < 60 ? $"{seconds} s" : $"{seconds / 60} min {seconds % 60} s";
        }
    }
}