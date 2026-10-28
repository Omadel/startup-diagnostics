using System;
using System.Globalization;
using UnityEditor;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Runs the diagnosis of what weighs on the start, with the thresholds of Project Settings › Startup Diagnostics:
    ///     the settings of the project, then the content of the first scene of the build and of the playable scenes.
    ///     Reading the project loads the assets it checks.
    /// </summary>
    internal static class StartupDiagnosis
    {
        public static DiagnosisRun Run()
        {
            var thresholds = StartupDiagnosticsSettings.instance.GetDiagnosisThresholds();
            var project = ProjectStateReader.Read();
            var results = ProjectDiagnosis.Check(project, thresholds);
            results.AddRange(SceneDiagnosis.Check(SceneStateReader.Read(thresholds), project.PackageLaunchScreen, PlayerSettings.SplashScreen.backgroundColor, thresholds));
            return new DiagnosisRun { RunAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture), Results = DiagnosisReport.Sort(results) };
        }
    }
}