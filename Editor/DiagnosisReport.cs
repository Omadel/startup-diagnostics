using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     How the diagnosis shows its results: their order, the names of the steps, the summary of a run and the log of a
    ///     result in the console.
    /// </summary>
    internal static class DiagnosisReport
    {
        /// <summary>
        ///     The results, warnings first, then infos, then the passed checks, each level in the order of the steps of the
        ///     start, and in the order of the checks within a step.
        /// </summary>
        public static List<DiagnosisResult> Sort(IEnumerable<DiagnosisResult> results)
        {
            return Sort(results, null);
        }

        /// <summary>
        ///     The results as <see cref="Sort(IEnumerable{DiagnosisResult})" /> orders them, the warnings and infos of the
        ///     longest step of a measure first.
        /// </summary>
        public static List<DiagnosisResult> Sort(IEnumerable<DiagnosisResult> results, StartupStep? longest)
        {
            return results.OrderBy(result => result.Level == DiagnosisLevel.Passed).ThenBy(result => result.Step != longest).ThenByDescending(result => result.Level).ThenBy(result => result.Step).ToList();
        }

        /// <summary>
        ///     The step and its share of the start, such as <c>First scene · 12%</c>; the step alone without times or for a
        ///     step that is not timed.
        /// </summary>
        public static string GetStepLabel(StartupStep step, StepTimes times)
        {
            var share = times?.GetShare(step);
            return share.HasValue ? $"{GetStepName(step)} · {FormatShare(share.Value)}" : GetStepName(step);
        }

        /// <summary>
        ///     What the diagnosis takes from a measure: its longest step, how long it took and its share of the start; or how
        ///     to get one without a measure.
        /// </summary>
        public static string DescribeLongestStep(StepTimes times, string measureDate)
        {
            if (times == null) return "No measure of the start yet: measure it in the Measure tab, and the checks of its longest step come first.";

            var measure = times.Measure;
            var step = times.Longest;
            var milliseconds = times.GetMilliseconds(step) ?? 0;
            return $"Longest step of the last measure, {measure.DeviceModel} on {measureDate}: {GetStepName(step).ToLowerInvariant()}, {FormatSeconds(milliseconds)} of {FormatSeconds(times.Total)} ({FormatShare(milliseconds / times.Total)}). Its checks come first.";
        }

        private static string FormatShare(double share)
        {
            return (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatSeconds(double milliseconds)
        {
            return (milliseconds / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " s";
        }

        public static string GetStepName(StartupStep step)
        {
            return step switch
            {
                StartupStep.EngineInitialization => "Engine initialization",
                StartupStep.FirstScene => "First scene",
                StartupStep.FirstFrame => "First frame",
                StartupStep.ProjectMilestones => "Milestones of the project",
                _ => "Perceived wait"
            };
        }

        /// <summary>
        ///     Where the step lies in the timeline of the start.
        /// </summary>
        public static string GetStepRange(StartupStep step)
        {
            return step switch
            {
                StartupStep.EngineInitialization => "From the start of the process to scripts_start.",
                StartupStep.FirstScene => "From scripts_start to first_scene_loaded.",
                StartupStep.FirstFrame => "From first_scene_loaded to first_frame.",
                StartupStep.ProjectMilestones => "From first_frame to the end of the start, through the milestones of the project.",
                _ => "What shows while the game starts, which does not lengthen the start."
            };
        }

        /// <summary>
        ///     The counts of a run by level, such as <c>2 warnings, 1 info, 6 passed</c>.
        /// </summary>
        public static string GetSummary(IReadOnlyCollection<DiagnosisResult> results)
        {
            var warnings = results.Count(result => result.Level == DiagnosisLevel.Warning);
            var infos = results.Count(result => result.Level == DiagnosisLevel.Info);
            var passed = results.Count(result => result.Level == DiagnosisLevel.Passed);
            return $"{warnings} {(warnings == 1 ? "warning" : "warnings")}, {infos} info, {passed} passed";
        }

        /// <summary>
        ///     The log of a result in the console: its title and level, its message, its items and the address of its section
        ///     of the guide.
        /// </summary>
        public static string FormatLog(DiagnosisResult result, string guideUrl)
        {
            var log = new StringBuilder();
            log.Append($"{StartupMeasure.LOG_PREFIX} {result.Title} ({(result.Level == DiagnosisLevel.Warning ? "warning" : "info")}): {result.Message}");
            foreach (var item in result.Items)
            {
                log.Append($"\n- {item.Label}");
                if (!string.IsNullOrEmpty(item.Detail))
                {
                    log.Append($": {item.Detail}");
                }
            }

            log.Append($"\nGuide: {guideUrl}");
            return log.ToString();
        }
    }
}