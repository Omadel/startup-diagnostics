#if UNITY_ANDROID
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Sets up the launch screens of the Android builds when the launch screen of the package is on, and removes them
    ///     otherwise. Compiled only for the Android platform, as its interface comes with the Android build support.
    /// </summary>
    internal sealed class AndroidLaunchScreenStep : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        /// <summary>
        ///     Unity passes the folder of the Unity library module.
        /// </summary>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var settings = StartupDiagnosticsSettings.instance;
            var icon = settings.LaunchScreen && settings.LaunchIcon != null ? LaunchScreenImage.Read(settings.LaunchIcon, Debug.LogWarning) : null;
            if (icon != null && icon.HasVisiblePixelsOutsideDisc())
            {
                Debug.LogWarning($"{StartupMeasure.LOG_PREFIX} The icon of the launch screen shows outside the circle inscribed in it: the splash screen of Android 12 and later crops it, and the next launch screens do not.");
            }

            AndroidLaunchScreen.Write(path, icon, PlayerSettings.SplashScreen.backgroundColor, Debug.LogWarning);
        }
    }
}
#endif