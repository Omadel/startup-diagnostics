namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A milestone as the window of the measure shows it: the first launch, the cold launches, the time since the
    ///     milestone before it, and the change since the previous measure.
    /// </summary>
    internal sealed class MilestoneRow
    {
        public string Name;

        /// <summary>
        ///     The milestone at the first launch after the clean install, in milliseconds, or -1.
        /// </summary>
        public int FirstLaunch = -1;

        /// <summary>
        ///     The milestone over the cold launches, or null before the first one.
        /// </summary>
        public MilestoneSummary Cold;

        /// <summary>
        ///     The median time since the milestone before it, in milliseconds, or NaN for
        ///     <see cref="MilestoneSummary.OS_FIRST_FRAME" />, outside the timeline.
        /// </summary>
        public double Step = double.NaN;

        /// <summary>
        ///     The milestone over the cold launches of the previous measure, or null.
        /// </summary>
        public MilestoneSummary Previous;

        /// <summary>
        ///     The change of the median since the previous measure, as a fraction of it, or NaN.
        /// </summary>
        public double Change = double.NaN;

        /// <summary>
        ///     Whether the median is more than <see cref="MeasureReport.SLOWER_THRESHOLD" /> above that of the previous
        ///     measure.
        /// </summary>
        public bool IsSlower;
    }
}