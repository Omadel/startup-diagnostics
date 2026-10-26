using System;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     How far a measure is, one step at a time: the device check, the build, the install, the first launch, then each
    ///     cold launch.
    /// </summary>
    internal static class MeasureProgress
    {
        public static int CountSteps(MeasureSettings settings)
        {
            return 1 + (settings.Rebuild ? 1 : 0) + (settings.CleanInstall ? 2 : 0) + Math.Max(0, settings.Runs);
        }

        /// <summary>
        ///     The step, its number, how long it has run and how long the measure has: the build and the install take
        ///     minutes, during which only the times move.
        /// </summary>
        public static string GetTitle(string step, int stepIndex, int stepCount, TimeSpan inStep, TimeSpan inAll)
        {
            return $"Step {stepIndex + 1} of {stepCount}: {step}, {MeasureReport.FormatDuration(inStep)} · {MeasureReport.FormatDuration(inAll)} in all";
        }
    }
}