using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Runs the diagnosis before each Android and iOS build when Project Settings › Startup Diagnostics asks for it: a
    ///     warning in the console for each warning, a log for each info, nothing for the passed checks. After the steps of
    ///     the package that change the Player Settings, such as the launch screen of iOS.
    /// </summary>
    internal sealed class DiagnosisBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var platform = report.summary.platform;
            if (!StartupDiagnosticsSettings.instance.DiagnosisOnBuild || (platform != BuildTarget.Android && platform != BuildTarget.iOS)) return;

            foreach (var result in StartupDiagnosis.Run().Results)
            {
                if (result.Level == DiagnosisLevel.Passed) continue;

                var log = DiagnosisReport.FormatLog(result, DiagnosisGuide.GetUrl(result.GuideSection));
                if (result.Level == DiagnosisLevel.Warning)
                {
                    Debug.LogWarning(log);
                }
                else
                {
                    Debug.Log(log);
                }
            }
        }
    }
}