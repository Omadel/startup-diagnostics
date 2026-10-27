using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Puts the launch screen of the package into the builds and Play Mode when it is on: before an iOS build, sets the
    ///     launch screen of iOS (<see cref="IosLaunchScreen" />); adds <see cref="LaunchScreen" /> to the first scene of each
    ///     build; gives it what to show in Play Mode, where it creates itself. Android has its own step,
    ///     <c>AndroidLaunchScreenStep</c>.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class LaunchScreenBuild : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        private static bool _isFirstScenePending;

        static LaunchScreenBuild()
        {
            LaunchScreen.EditorLook = () => StartupDiagnosticsSettings.instance.GetLaunchScreenLook();
        }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = StartupDiagnosticsSettings.instance;
            _isFirstScenePending = false;
            if (!settings.LaunchScreen) return;

            if (settings.LaunchIcon == null)
            {
                Debug.LogWarning($"{StartupMeasure.LOG_PREFIX} The launch screen is on without icon: the build has the launch screens of Unity. Choose its icon in Project Settings › Startup Diagnostics.");
                return;
            }

            _isFirstScenePending = true;
            if (report.summary.platform == BuildTarget.iOS)
            {
                IosLaunchScreen.ApplyToPlayerSettings(settings.LaunchIcon, PlayerSettings.SplashScreen.backgroundColor);
            }
        }

        /// <summary>
        ///     Adds the launch screen to the first scene that the build processes, the first scene of the build: Unity
        ///     processes them in order. Nothing in Play Mode, where the report is null.
        /// </summary>
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !_isFirstScenePending || !BuildPipeline.isBuildingPlayer) return;

            _isFirstScenePending = false;
            var look = StartupDiagnosticsSettings.instance.GetLaunchScreenLook();
            if (look == null) return;

            var gameObject = new GameObject(LaunchScreen.NAME);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            gameObject.AddComponent<LaunchScreen>().Look = look;
        }
    }
}