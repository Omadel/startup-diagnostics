using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Sums up the milestones of several launches.
    /// </summary>
    internal static class LaunchStatistics
    {
        /// <summary>
        ///     The median, minimum and maximum of each milestone, in the order the milestones first appear, over the launches
        ///     that have it; then <see cref="MilestoneSummary.OS_FIRST_FRAME" /> if the OS reported it for every launch.
        /// </summary>
        public static List<MilestoneSummary> Summarize(IReadOnlyList<Launch> launches)
        {
            var names = new List<string>();
            foreach (var launch in launches)
            {
                foreach (var milestone in launch.Milestones)
                {
                    if (!names.Contains(milestone.Name))
                    {
                        names.Add(milestone.Name);
                    }
                }
            }

            var summaries = new List<MilestoneSummary>();
            foreach (var name in names)
            {
                var values = new List<int>();
                foreach (var launch in launches)
                {
                    if (launch.TryGetMilestone(name, out var milliseconds))
                    {
                        values.Add(milliseconds);
                    }
                }

                summaries.Add(Summarize(name, values));
            }

            var osFirstFrames = new List<int>();
            foreach (var launch in launches)
            {
                if (launch.OsFirstFrame < 0) return summaries;

                osFirstFrames.Add(launch.OsFirstFrame);
            }

            if (osFirstFrames.Count > 0)
            {
                summaries.Add(Summarize(MilestoneSummary.OS_FIRST_FRAME, osFirstFrames));
            }

            return summaries;
        }

        /// <summary>
        ///     The median time of each milestone since the milestone before it in the same launch, or since the origin for the
        ///     first milestone, over the launches that have it.
        /// </summary>
        public static Dictionary<string, double> SummarizeSteps(IReadOnlyList<Launch> launches)
        {
            var steps = new Dictionary<string, List<int>>();
            foreach (var launch in launches)
            {
                var previous = 0;
                foreach (var milestone in launch.Milestones)
                {
                    if (!steps.TryGetValue(milestone.Name, out var values))
                    {
                        values = new List<int>();
                        steps.Add(milestone.Name, values);
                    }

                    values.Add(milestone.Milliseconds - previous);
                    previous = milestone.Milliseconds;
                }
            }

            var medians = new Dictionary<string, double>();
            foreach (var step in steps)
            {
                medians.Add(step.Key, Median(step.Value));
            }

            return medians;
        }

        /// <summary>
        ///     The middle value, or the mean of the two middle values for an even count.
        /// </summary>
        public static double Median(IReadOnlyList<int> values)
        {
            var sorted = new List<int>(values);
            sorted.Sort();
            var middle = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
        }

        private static MilestoneSummary Summarize(string name, List<int> values)
        {
            var min = values[0];
            var max = values[0];
            foreach (var value in values)
            {
                if (value < min)
                {
                    min = value;
                }

                if (value > max)
                {
                    max = value;
                }
            }

            return new MilestoneSummary { Name = name, Median = Median(values), Min = min, Max = max };
        }
    }
}