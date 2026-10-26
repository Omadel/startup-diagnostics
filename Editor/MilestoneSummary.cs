using System;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A milestone over the cold launches, in milliseconds: median, minimum and maximum.
    /// </summary>
    [Serializable]
    public sealed class MilestoneSummary
    {
        /// <summary>
        ///     The name of the summary of <see cref="Launch.OsFirstFrame" />, after the milestones of the timeline.
        /// </summary>
        public const string OS_FIRST_FRAME = "os_first_frame";

        public string Name;
        public double Median;
        public int Min;
        public int Max;
    }
}