using System;
using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The time each step of the start took in a measure, from the medians of its milestones over the cold launches, and
    ///     the longest step: the diagnosis puts its checks first.
    /// </summary>
    internal sealed class StepTimes
    {
        private static readonly StartupStep[] _timedSteps = { StartupStep.EngineInitialization, StartupStep.FirstScene, StartupStep.FirstFrame, StartupStep.ProjectMilestones };

        private readonly Dictionary<StartupStep, double> _milliseconds;

        /// <summary>
        ///     Times by step, the longest being the first of the longest in the order of the start.
        /// </summary>
        private StepTimes(MeasureResult measure, Dictionary<StartupStep, double> milliseconds, double total)
        {
            Measure = measure;
            _milliseconds = milliseconds;
            Total = total;
            foreach (var step in _timedSteps)
            {
                if (milliseconds[step] > milliseconds[Longest])
                {
                    Longest = step;
                }
            }
        }

        /// <summary>
        ///     The measure the times come from.
        /// </summary>
        public MeasureResult Measure { get; }

        /// <summary>
        ///     The whole start, in milliseconds: the median of its last milestone.
        /// </summary>
        public double Total { get; }

        /// <summary>
        ///     The step that took the longest.
        /// </summary>
        public StartupStep Longest { get; }

        /// <summary>
        ///     The times of a measure that succeeded with cold launches, or null when it lacks the milestones of the package:
        ///     <c>scripts_start</c>, <c>first_scene_loaded</c> and <c>first_frame</c>. The milestones of the project run from
        ///     <c>first_frame</c> to the last milestone of the timeline.
        /// </summary>
        public static StepTimes FromMeasure(MeasureResult measure)
        {
            if (measure == null || measure.Status != MeasureStatus.SUCCEEDED || measure.ProfilerCapture) return null;

            var medians = new Dictionary<string, double>();
            var end = 0.0;
            foreach (var milestone in measure.ColdSummary)
            {
                if (milestone.Name == MilestoneSummary.OS_FIRST_FRAME) continue;

                medians[milestone.Name] = milestone.Median;
                end = Math.Max(end, milestone.Median);
            }

            if (!medians.TryGetValue(StartupTimeline.SCRIPTS_START, out var scriptsStart) || !medians.TryGetValue(StartupTimeline.FIRST_SCENE_LOADED, out var firstSceneLoaded) || !medians.TryGetValue(StartupTimeline.FIRST_FRAME, out var firstFrame)) return null;

            var milliseconds = new Dictionary<StartupStep, double>
            {
                { StartupStep.EngineInitialization, scriptsStart },
                { StartupStep.FirstScene, Math.Max(0, firstSceneLoaded - scriptsStart) },
                { StartupStep.FirstFrame, Math.Max(0, firstFrame - firstSceneLoaded) },
                { StartupStep.ProjectMilestones, Math.Max(0, end - firstFrame) }
            };
            return end > 0 ? new StepTimes(measure, milliseconds, end) : null;
        }

        /// <summary>
        ///     The times of the newest measure that gives them, of the platform first, of any platform otherwise; null
        ///     without one.
        /// </summary>
        public static StepTimes FromNewest(IReadOnlyList<MeasureFile> newestFirst, string platform)
        {
            StepTimes other = null;
            foreach (var file in newestFirst)
            {
                var times = FromMeasure(file.Result);
                if (times == null) continue;

                if (file.Result.Platform == platform) return times;

                other ??= times;
            }

            return other;
        }

        /// <summary>
        ///     The time of a step, in milliseconds, or null for a step that is not timed, such as the perceived wait.
        /// </summary>
        public double? GetMilliseconds(StartupStep step)
        {
            return _milliseconds.TryGetValue(step, out var milliseconds) ? milliseconds : null;
        }

        /// <summary>
        ///     The share of the start that a step took, from 0 to 1, or null for a step that is not timed.
        /// </summary>
        public double? GetShare(StartupStep step)
        {
            var milliseconds = GetMilliseconds(step);
            return milliseconds.HasValue ? milliseconds.Value / Total : null;
        }
    }
}