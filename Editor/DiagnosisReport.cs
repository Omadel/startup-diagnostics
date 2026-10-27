using System.Collections.Generic;
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
            return results.OrderByDescending(result => result.Level).ThenBy(result => result.Step).ToList();
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