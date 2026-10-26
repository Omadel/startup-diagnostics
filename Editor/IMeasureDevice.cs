using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A device the start is measured on, through the tools of its platform.
    /// </summary>
    internal interface IMeasureDevice
    {
        string Id { get; }
        string Model { get; }
        string OsVersion { get; }

        /// <summary>
        ///     One of the network values of <see cref="MeasureResult" />.
        /// </summary>
        string Network { get; }

        /// <summary>
        ///     The devices of the platform ready for the measure.
        /// </summary>
        Task<List<ListedDevice>> ListAsync(CancellationToken cancellation);

        /// <summary>
        ///     Finds the device, checks that it is unlocked, and reads what it is.
        /// </summary>
        Task ConnectAsync(CancellationToken cancellation);

        /// <summary>
        ///     Uninstalls the game, which deletes its data, and installs the build.
        /// </summary>
        Task InstallAsync(string buildPath, CancellationToken cancellation);

        /// <summary>
        ///     Kills the game, launches it, and returns its timeline once the start has ended; kills it again after the wait.
        /// </summary>
        Task<Launch> LaunchAsync(TimeSpan waitAfterEnd, CancellationToken cancellation);

        /// <summary>
        ///     Kills the game, asks it for a Profiler capture of its start, launches it, and copies the capture from the
        ///     device to the path once complete; then kills it again. Returns its timeline.
        /// </summary>
        Task<Launch> CaptureProfilerAsync(float seconds, string capturePath, CancellationToken cancellation);
    }
}