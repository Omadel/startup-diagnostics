using System;
using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A startup measure, written as JSON in <c>Library/StartupDiagnostics</c> as it goes: where it stands, what was
    ///     measured, and the raw values of each launch.
    /// </summary>
    [Serializable]
    public sealed class MeasureResult
    {
        public const string NETWORK_ONLINE = "online";
        public const string NETWORK_OFFLINE = "offline";
        public const string NETWORK_UNKNOWN = "unknown";

        /// <summary>
        ///     One of the <see cref="MeasureStatus" /> values.
        /// </summary>
        public string Status = MeasureStatus.RUNNING;

        /// <summary>
        ///     What the measure is doing, or did last.
        /// </summary>
        public string Step;

        /// <summary>
        ///     Why the measure failed, or empty.
        /// </summary>
        public string Error;

        public string Platform;

        /// <summary>
        ///     The start and the end of the measure, in UTC and ISO 8601.
        /// </summary>
        public string StartedAt;

        public string FinishedAt;
        public string UnityVersion;

        /// <summary>
        ///     The git commit of the project when the measure started, or empty outside git.
        /// </summary>
        public string Commit;

        public string ApplicationIdentifier;
        public string BuildPath;

        /// <summary>
        ///     Whether the measure built the game, or measured a build already there.
        /// </summary>
        public bool Rebuilt;

        public string DeviceId;
        public string DeviceModel;
        public string OsVersion;

        /// <summary>
        ///     Whether the device had a network when the measure started: <see cref="NETWORK_ONLINE" />,
        ///     <see cref="NETWORK_OFFLINE" /> or <see cref="NETWORK_UNKNOWN" />. The start may wait less without one.
        /// </summary>
        public string Network;

        public int Runs;
        public bool CleanInstall;

        /// <summary>
        ///     The first launch after the clean install, out of the summary. Empty without a clean install.
        /// </summary>
        public Launch FirstLaunch;

        public List<Launch> ColdLaunches = new();

        /// <summary>
        ///     Each milestone over the cold launches, in the order of the timeline, then
        ///     <see cref="MilestoneSummary.OS_FIRST_FRAME" /> when the OS reports it.
        /// </summary>
        public List<MilestoneSummary> ColdSummary = new();

        /// <summary>
        ///     Whether the measure captured the Profiler, in a development build, instead of timing cold launches.
        /// </summary>
        public bool ProfilerCapture;

        public float ProfilerSeconds;

        /// <summary>
        ///     The launch that captured the Profiler, out of the summary: a development build starts slower.
        /// </summary>
        public Launch CaptureLaunch;

        /// <summary>
        ///     The Profiler capture copied from the device, relative to the project, or empty.
        /// </summary>
        public string CapturePath;

        public bool HasFirstLaunch => FirstLaunch != null && FirstLaunch.Milestones.Count > 0;
    }
}