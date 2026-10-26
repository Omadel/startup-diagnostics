using System;
using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     One launch of the game on the device: its timeline, read from the <c>[Startup]</c> line, and what the OS reports.
    /// </summary>
    [Serializable]
    public sealed class Launch
    {
        /// <summary>
        ///     The origin of the timeline: <see cref="StartupTimeline.ORIGIN_PROCESS" /> on Android and iOS.
        /// </summary>
        public string Origin;

        /// <summary>
        ///     The milestones of the <c>[Startup]</c> line, in its order.
        /// </summary>
        public List<Milestone> Milestones = new();

        /// <summary>
        ///     The first frame of the app according to the OS, in milliseconds (<c>am start -W</c> on Android), or -1.
        /// </summary>
        public int OsFirstFrame = -1;

        /// <summary>
        ///     The kind of launch according to the OS (<c>COLD</c>, <c>WARM</c> or <c>HOT</c> on Android), or empty.
        /// </summary>
        public string OsLaunchState;

        public bool TryGetMilestone(string name, out int milliseconds)
        {
            foreach (var milestone in Milestones)
            {
                if (milestone.Name != name) continue;

                milliseconds = milestone.Milliseconds;
                return true;
            }

            milliseconds = 0;
            return false;
        }
    }
}