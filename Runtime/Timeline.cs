using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The milestones of one start and whether it has ended, without clock or output: <see cref="StartupTimeline" />
    ///     gives it the time of each event, and logs the timeline once it ends.
    /// </summary>
    internal sealed class Timeline
    {
        private readonly List<KeyValuePair<string, double>> _milestones = new();

        /// <summary>
        ///     The milestones in the order they were recorded, with their time in seconds.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, double>> Milestones => _milestones;

        /// <summary>
        ///     Whether the start goes on after the first frame, until <see cref="End" />.
        /// </summary>
        public bool IsEndDeferred { get; private set; }

        /// <summary>
        ///     Whether the start has ended. Its timeline is complete: later milestones are left out.
        /// </summary>
        public bool IsEnded { get; private set; }

        /// <summary>
        ///     Records a milestone, unless the start has ended.
        /// </summary>
        public void Mark(string milestone, double time)
        {
            if (IsEnded) return;

            _milestones.Add(new KeyValuePair<string, double>(milestone, time));
        }

        /// <summary>
        ///     Keeps the start going after the first frame, until <see cref="End" />. No effect once the start has ended.
        /// </summary>
        public void DeferEnd()
        {
            if (IsEnded) return;

            IsEndDeferred = true;
        }

        /// <summary>
        ///     Records the last milestone and ends the start. Returns false, and records nothing, if the start had already
        ///     ended.
        /// </summary>
        public bool End(string milestone, double time)
        {
            if (IsEnded) return false;

            Mark(milestone, time);
            IsEnded = true;
            return true;
        }

        /// <summary>
        ///     Records the first frame, which ends the start unless the end is deferred. Returns true if it ends the start.
        /// </summary>
        public bool MarkFirstFrame(double time)
        {
            if (!IsEndDeferred) return End(StartupTimeline.FIRST_FRAME, time);

            Mark(StartupTimeline.FIRST_FRAME, time);
            return false;
        }
    }
}