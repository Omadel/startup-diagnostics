using System;
using System.Globalization;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Runs the diagnosis of what weighs on the start, with the thresholds of Project Settings › Startup Diagnostics.
    ///     Reading the project loads the assets it checks.
    /// </summary>
    internal static class StartupDiagnosis
    {
        public static DiagnosisRun Run()
        {
            var thresholds = StartupDiagnosticsSettings.instance.GetDiagnosisThresholds();
            var results = ProjectDiagnosis.Check(ProjectStateReader.Read(), thresholds);
            return new DiagnosisRun { RunAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture), Results = DiagnosisReport.Sort(results) };
        }
    }
}