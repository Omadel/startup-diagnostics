using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The settings of the project that <see cref="ProjectDiagnosis" /> checks, gathered by
    ///     <see cref="ProjectStateReader" />: plain values, so that the checks can be tested without changing the project.
    /// </summary>
    internal sealed class ProjectState
    {
        /// <summary>
        ///     Whether the builds show the splash screen of Unity.
        /// </summary>
        public bool UnitySplashScreen;

        /// <summary>
        ///     The Preloaded Assets of the Player Settings, each with its dependencies.
        /// </summary>
        public List<SizedAsset> PreloadedAssets = new();

        /// <summary>
        ///     The assets of the Resources folders that go into the builds, each with its dependencies.
        /// </summary>
        public List<SizedAsset> Resources = new();

        /// <summary>
        ///     What the Preloaded Assets and the Resources folders take into the build together, each dependency counted
        ///     once.
        /// </summary>
        public long PreloadedAndResourcesSize;

        /// <summary>
        ///     Whether the launch screen of the package is on, with an icon.
        /// </summary>
        public bool PackageLaunchScreen;

        /// <summary>
        ///     The launch screen of iOS on iPhone, an <c>iOSLaunchScreenType</c>.
        /// </summary>
        public int IphoneLaunchScreen;

        /// <summary>
        ///     The launch screen of iOS on iPad, an <c>iOSLaunchScreenType</c>.
        /// </summary>
        public int IpadLaunchScreen;

        /// <summary>
        ///     Whether the Player Settings give an image to the static splash screen of Unity on Android.
        /// </summary>
        public bool AndroidStaticSplashImage;

        /// <summary>
        ///     Whether Engine Diagnostics is on (Project Settings › Services › Diagnostics).
        /// </summary>
        public bool EngineDiagnostics;

        /// <summary>
        ///     Whether the module of the legacy Unity Analytics is in the project and initializes on startup.
        /// </summary>
        public bool AnalyticsOnStartup;

        /// <summary>
        ///     Whether the builds send hardware statistics to Unity (<c>submitAnalytics</c> of the Player Settings).
        /// </summary>
        public bool HardwareStatistics;

        /// <summary>
        ///     Whether the Input System is installed and handles the input of the builds.
        /// </summary>
        public bool InputSystem;

        /// <summary>
        ///     Number of devices in the Supported Devices of the Input System: none means every device.
        /// </summary>
        public int SupportedInputDevices;

        /// <summary>
        ///     The settings and the content of Addressables, or null when Addressables is not installed or not set up.
        /// </summary>
        public AddressablesState Addressables;
    }
}