using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Reads into a <see cref="ProjectState" /> the settings of the project that the diagnosis checks, without changing
    ///     them.
    /// </summary>
    internal static class ProjectStateReader
    {
        private const string ANALYTICS_MODULE = "com.unity.modules.unityanalytics";
        private const string ANDROID_STATIC_SPLASH_IMAGE = "androidSplashScreen";
        private const string HARDWARE_STATISTICS = "submitAnalytics";
        private const string ACTIVE_INPUT_HANDLER = "activeInputHandler";
        private const string ENGINE_DIAGNOSTICS = "InsightsSettings.m_EngineDiagnosticsEnabled";
        private const string ANALYTICS_ON_STARTUP = "UnityAnalyticsSettings.m_InitializeOnStartup";
        private const int INPUT_MANAGER_ONLY = 0;
        private const int UNKNOWN = -1;

        public static ProjectState Read()
        {
            var playerSettings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton(nameof(PlayerSettings)));
            var connectSettings = Unsupported.GetSerializedAssetInterfaceSingleton("UnityConnectSettings");
            var connect = connectSettings == null ? null : new SerializedObject(connectSettings);
            var settings = StartupDiagnosticsSettings.instance;
            var state = new ProjectState
            {
                UnitySplashScreen = PlayerSettings.SplashScreen.show,
                PackageLaunchScreen = settings.LaunchScreen && settings.LaunchIcon != null,
                IphoneLaunchScreen = GetInt(playerSettings, IosLaunchScreen.IPHONE_TYPE),
                IpadLaunchScreen = GetInt(playerSettings, IosLaunchScreen.IPAD_TYPE),
                AndroidStaticSplashImage = playerSettings.FindProperty(ANDROID_STATIC_SPLASH_IMAGE)?.objectReferenceValue != null,
                EngineDiagnostics = GetBool(connect, ENGINE_DIAGNOSTICS),
                AnalyticsOnStartup = IsInstalled(ANALYTICS_MODULE) && GetBool(connect, ANALYTICS_ON_STARTUP),
                HardwareStatistics = GetBool(playerSettings, HARDWARE_STATISTICS),
                Addressables = AddressablesStateReader.Read()
            };
            ReadInputSystem(state, playerSettings);
            ReadPreloadedAssetsAndResources(state);
            return state;
        }

        private static void ReadInputSystem(ProjectState state, SerializedObject playerSettings)
        {
#if STARTUP_DIAGNOSTICS_INPUT_SYSTEM
            state.InputSystem = GetInt(playerSettings, ACTIVE_INPUT_HANDLER) != INPUT_MANAGER_ONLY;
            state.SupportedInputDevices = UnityEngine.InputSystem.InputSystem.settings.supportedDevices.Count;
#endif
        }

        /// <summary>
        ///     The Preloaded Assets and the assets of the Resources folders that go into the builds, with their size and
        ///     the size of their dependencies.
        /// </summary>
        private static void ReadPreloadedAssetsAndResources(ProjectState state)
        {
            var preloaded = new HashSet<string>();
            foreach (var asset in PlayerSettings.GetPreloadedAssets())
            {
                var path = asset == null ? null : AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(path))
                {
                    preloaded.Add(path);
                }
            }

            var resources = new HashSet<string>();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (AssetPaths.IsInBuildResources(path) && !AssetDatabase.IsValidFolder(path))
                {
                    resources.Add(path);
                }
            }

            var measured = DependencySizes.Measure(preloaded.Union(resources), GetBuildDependencies, AssetSizes.Estimate, out var total);
            foreach (var asset in measured)
            {
                if (preloaded.Contains(asset.Path))
                {
                    state.PreloadedAssets.Add(asset);
                }

                if (resources.Contains(asset.Path))
                {
                    state.Resources.Add(asset);
                }
            }

            state.PreloadedAndResourcesSize = total;
        }

        private static IEnumerable<string> GetBuildDependencies(string path)
        {
            return AssetDatabase.GetDependencies(path, true).Where(AssetPaths.CountsInBuild);
        }

        private static bool IsInstalled(string package)
        {
            return PackageInfo.GetAllRegisteredPackages().Any(info => info.name == package);
        }

        private static int GetInt(SerializedObject settings, string property)
        {
            return settings?.FindProperty(property)?.intValue ?? UNKNOWN;
        }

        private static bool GetBool(SerializedObject settings, string property)
        {
            return settings?.FindProperty(property)?.boolValue ?? false;
        }
    }
}