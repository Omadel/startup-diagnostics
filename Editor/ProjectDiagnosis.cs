using System;
using System.Collections.Generic;
using UnityEditor;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The checks of the diagnosis on the settings of the project, from a <see cref="ProjectState" />: the splash screen
    ///     of Unity, the Preloaded Assets and the Resources folders, the launch screens of the OS, the services of Unity, the
    ///     devices of the Input System and the settings and the local content of Addressables.
    /// </summary>
    internal static class ProjectDiagnosis
    {
        public const string UNITY_SPLASH_SCREEN = "unity-splash-screen";
        public const string PRELOADED_ASSETS_AND_RESOURCES = "preloaded-assets-and-resources";
        public const string LAUNCH_SCREENS = "launch-screens";
        public const string UNITY_SERVICES = "unity-services";
        public const string INPUT_DEVICES = "input-devices";
        public const string CATALOG_UPDATE = "catalog-update";
        public const string LOCAL_BUNDLE_COMPRESSION = "local-bundle-compression";
        public const string JSON_CATALOG = "json-catalog";
        public const string REMOTE_CATALOG_CONTENT = "remote-catalog-content";

        private const int DEFAULT_LAUNCH_SCREEN = 0;

        /// <summary>
        ///     The results of the checks that apply to the project: those of the Input System and of Addressables only when
        ///     they are installed, the local content only when it was built with a remote catalog.
        /// </summary>
        public static List<DiagnosisResult> Check(ProjectState state, DiagnosisThresholds thresholds)
        {
            var results = new List<DiagnosisResult>
            {
                CheckUnitySplashScreen(state),
                CheckPreloadedAssetsAndResources(state, thresholds),
                CheckLaunchScreens(state),
                CheckUnityServices(state)
            };
            if (state.InputSystem)
            {
                results.Add(CheckInputDevices(state));
            }

            var addressables = state.Addressables;
            if (addressables == null) return results;

            results.Add(CheckCatalogUpdate(addressables));
            results.Add(CheckLocalBundleCompression(addressables));
            results.Add(CheckJsonCatalog(addressables));
            if (addressables.RemoteCatalog && addressables.ContentChanges != null)
            {
                results.Add(CheckRemoteCatalogContent(addressables, thresholds));
            }

            return results;
        }

        public static DiagnosisResult CheckUnitySplashScreen(ProjectState state)
        {
            var result = Create(UNITY_SPLASH_SCREEN, "Splash screen of Unity", StartupStep.FirstScene, DiagnosisGuide.UNITY_SPLASH_SCREEN);
            if (!state.UnitySplashScreen)
            {
                result.Message = "The builds do not show the splash screen of Unity.";
                return result;
            }

            result.Level = DiagnosisLevel.Warning;
            result.Message = "The builds show the splash screen of Unity before they load the first scene, as long as its animation lasts: 2.1 s on a Pixel 6 in a new project. Turn off Show Splash Screen in Player Settings › Splash Image, which every plan can since Unity 6, and show a launch screen instead.";
            result.Items.Add(new DiagnosisItem { Label = "Player Settings › Splash Image › Show Splash Screen", Detail = "On" });
            return result;
        }

        public static DiagnosisResult CheckPreloadedAssetsAndResources(ProjectState state, DiagnosisThresholds thresholds)
        {
            var result = Create(PRELOADED_ASSETS_AND_RESOURCES, "Preloaded Assets and Resources", StartupStep.EngineInitialization, DiagnosisGuide.PRELOADED_ASSETS_AND_RESOURCES);
            if (state.PreloadedAssets.Count == 0 && state.Resources.Count == 0)
            {
                result.Message = "No Preloaded Assets in the Player Settings, and nothing in the Resources folders.";
                return result;
            }

            var items = new List<(SizedAsset Asset, string Kind)>();
            foreach (var asset in state.PreloadedAssets)
            {
                items.Add((asset, "Preloaded Asset"));
            }

            foreach (var asset in state.Resources)
            {
                items.Add((asset, "Resources"));
            }

            items.Sort((a, b) => b.Asset.Size.CompareTo(a.Asset.Size));
            var listed = Math.Min(items.Count, Math.Max(1, thresholds.ListedAssets));
            for (var i = 0; i < listed; i++)
            {
                var (asset, kind) = items[i];
                result.Items.Add(new DiagnosisItem { Label = asset.Path, Detail = $"{kind}, {EditorUtility.FormatBytes(asset.Size)} with its dependencies", AssetPath = asset.Path });
            }

            var above = state.PreloadedAndResourcesSize > thresholds.PreloadedAndResourcesSize;
            result.Level = above ? DiagnosisLevel.Warning : DiagnosisLevel.Info;
            var threshold = above ? $", above the threshold of {EditorUtility.FormatBytes(thresholds.PreloadedAndResourcesSize)}" : "";
            var more = items.Count > listed ? $" The {listed} largest are listed." : "";
            result.Message = $"The engine loads the Preloaded Assets before the first scene and keeps them in memory, and indexes every object of the Resources folders while it starts. {Count(state.PreloadedAssets.Count, "Preloaded Asset")} and {Count(state.Resources.Count, "asset")} in Resources take about {EditorUtility.FormatBytes(state.PreloadedAndResourcesSize)} with their dependencies, estimated in the editor{threshold}.{more} Keep only what must exist before the first scene, and move the rest to Addressables or to the scenes that use it.";
            return result;
        }

        public static DiagnosisResult CheckLaunchScreens(ProjectState state)
        {
            var result = Create(LAUNCH_SCREENS, "Launch screens of the OS", StartupStep.PerceivedWait, DiagnosisGuide.LAUNCH_SCREEN);
            if (state.PackageLaunchScreen)
            {
                result.Message = "The launch screen of the package shows its icon on the launch screens of Android and iOS, then in the first scene until the end of the start.";
                return result;
            }

            if (state.IphoneLaunchScreen == DEFAULT_LAUNCH_SCREEN)
            {
                result.Items.Add(new DiagnosisItem { Label = "Player Settings › iOS › Splash Image › iPhone Launch Screen", Detail = "Default launch screen of Unity" });
            }

            if (state.IpadLaunchScreen == DEFAULT_LAUNCH_SCREEN)
            {
                result.Items.Add(new DiagnosisItem { Label = "Player Settings › iOS › Splash Image › iPad Launch Screen", Detail = "Default launch screen of Unity" });
            }

            if (!state.AndroidStaticSplashImage)
            {
                result.Items.Add(new DiagnosisItem { Label = "Player Settings › Android › Splash Image › Static Splash Image", Detail = "None" });
            }

            if (result.Items.Count == 0)
            {
                result.Message = "The launch screens of iOS and the static splash screen of Unity on Android show an image of the game. The splash screen of the system on Android 12 and later stays empty without the launch screen of the package.";
                return result;
            }

            result.Level = DiagnosisLevel.Warning;
            result.Message = "Until the first frame of the first scene, the launch screens of the OS show nothing of the game: the default launch screen of Unity on iOS, an empty splash screen of the system on Android 12 and later, then the static splash screen of Unity without image. Turn on the launch screen of the package in Project Settings › Startup Diagnostics: the same icon from the launch screen of the OS to the end of the start.";
            return result;
        }

        public static DiagnosisResult CheckUnityServices(ProjectState state)
        {
            var result = Create(UNITY_SERVICES, "Unity services at launch", StartupStep.EngineInitialization, DiagnosisGuide.UNITY_SERVICES);
            if (state.EngineDiagnostics)
            {
                result.Items.Add(new DiagnosisItem { Label = "Engine Diagnostics", Detail = "Project Settings › Services › Diagnostics › Diagnostics Data" });
            }

            if (state.AnalyticsOnStartup)
            {
                result.Items.Add(new DiagnosisItem { Label = "Legacy Unity Analytics", Detail = "Initializes on startup, module com.unity.modules.unityanalytics" });
            }

            if (state.HardwareStatistics)
            {
                result.Items.Add(new DiagnosisItem { Label = "Hardware statistics", Detail = "submitAnalytics in the Player Settings" });
            }

            if (result.Items.Count == 0)
            {
                result.Message = "Engine Diagnostics, the legacy Unity Analytics and the hardware statistics are off.";
                return result;
            }

            result.Level = DiagnosisLevel.Info;
            result.Message = "These services of Unity start with the game: they ask the servers of Unity for their settings and write their events on the device. Their cost on the start was not measured. Turn off those the game does not need.";
            return result;
        }

        public static DiagnosisResult CheckInputDevices(ProjectState state)
        {
            var result = Create(INPUT_DEVICES, "Devices of the Input System", StartupStep.FirstScene, DiagnosisGuide.INPUT_DEVICES);
            if (state.SupportedInputDevices > 0)
            {
                result.Message = $"The Input System creates only the devices of its {Count(state.SupportedInputDevices, "supported device")}.";
                return result;
            }

            result.Level = DiagnosisLevel.Info;
            result.Message = "The Input System creates every device the system reports before the first scene, sensors included: 39 devices and 41 ms in a Profiler capture of a Pixel 6, not tried without them. List the devices the game uses in Project Settings › Input System Package › Supported Devices: the Input System ignores the others.";
            result.Items.Add(new DiagnosisItem { Label = "Project Settings › Input System Package › Supported Devices", Detail = "Every device" });
            return result;
        }

        public static DiagnosisResult CheckCatalogUpdate(AddressablesState addressables)
        {
            var result = Create(CATALOG_UPDATE, "Catalog update at startup", StartupStep.ProjectMilestones, DiagnosisGuide.CATALOG_UPDATE);
            if (!addressables.RemoteCatalog)
            {
                result.Message = "No remote catalog: Addressables has nothing to ask the server while it initializes.";
                return result;
            }

            if (!addressables.UpdateCatalogsOnStartup)
            {
                result.Message = "Addressables does not check for a newer remote catalog while it initializes.";
                return result;
            }

            result.Level = DiagnosisLevel.Warning;
            result.Message = "Addressables checks for a newer remote catalog while it initializes: a request to the server inside the start, slower on a poor network and offline. Turn on Only update catalogs manually in the Addressables settings, then update the catalogs after the first playable screen.";
            result.Items.Add(new DiagnosisItem { Label = "Addressables settings › Catalog › Only update catalogs manually", Detail = "Off", AssetPath = addressables.SettingsPath });
            return result;
        }

        public static DiagnosisResult CheckLocalBundleCompression(AddressablesState addressables)
        {
            var result = Create(LOCAL_BUNDLE_COMPRESSION, "Compression of the local bundles", StartupStep.ProjectMilestones, DiagnosisGuide.BUNDLE_COMPRESSION);
            foreach (var group in addressables.Groups)
            {
                if (group.Local && group.Lzma)
                {
                    result.Items.Add(new DiagnosisItem { Label = group.Name, Detail = "LZMA, loaded from the app", AssetPath = group.AssetPath });
                }
            }

            if (result.Items.Count == 0)
            {
                result.Message = "No group loaded from the app is compressed with LZMA.";
                return result;
            }

            result.Level = DiagnosisLevel.Warning;
            result.Message = "These groups ship in the app with LZMA bundles, which must be decompressed whole before they are read: Unity recompresses them to LZ4 in its cache. Set their Compression to LZ4, and keep LZMA for the remote groups.";
            return result;
        }

        public static DiagnosisResult CheckJsonCatalog(AddressablesState addressables)
        {
            var result = Create(JSON_CATALOG, "Format of the catalog", StartupStep.ProjectMilestones, DiagnosisGuide.JSON_CATALOG);
            if (!addressables.JsonCatalog)
            {
                result.Message = "The catalog is binary.";
                return result;
            }

            result.Level = DiagnosisLevel.Info;
            result.Message = "The catalog is written in JSON, slower to read than the binary catalog of Addressables 2: not compared in a measure. Turn off Enable Json Catalog in the Addressables settings.";
            result.Items.Add(new DiagnosisItem { Label = "Addressables settings › Catalog › Enable Json Catalog", Detail = "On", AssetPath = addressables.SettingsPath });
            return result;
        }

        public static DiagnosisResult CheckRemoteCatalogContent(AddressablesState addressables, DiagnosisThresholds thresholds)
        {
            var result = Create(REMOTE_CATALOG_CONTENT, "Local content and remote catalog", StartupStep.ProjectMilestones, DiagnosisGuide.REMOTE_CATALOG_CONTENT);
            var changes = addressables.ContentChanges;
            if (changes.Count == 0)
            {
                result.Message = $"The local content is the one of the last content build for {addressables.ContentPlatform}.";
                return result;
            }

            var listed = Math.Min(changes.Count, Math.Max(1, thresholds.ListedAssets));
            for (var i = 0; i < listed; i++)
            {
                var change = changes[i];
                result.Items.Add(new DiagnosisItem { Label = change.Path ?? change.Guid, Detail = Describe(change.Kind), AssetPath = change.Path });
            }

            var more = changes.Count > listed ? $" The first {listed} are listed." : "";
            result.Level = DiagnosisLevel.Warning;
            result.Message = $"The local content changed since the last content build for {addressables.ContentPlatform}: {Count(changes.Count, "asset")}.{more} A player build that builds it again without releasing the content gets local bundles that the remote catalog on the server does not know: once the game has downloaded that catalog, the playable scene no longer loads, from the second launch on. Build and release the content with the build, such as with Build & Release of Cloud Content Delivery.";
            return result;
        }

        private static DiagnosisResult Create(string check, string title, StartupStep step, string guideSection)
        {
            return new DiagnosisResult { Check = check, Title = title, Level = DiagnosisLevel.Passed, Step = step, GuideSection = guideSection };
        }

        private static string Describe(ContentChangeKind kind)
        {
            return kind switch
            {
                ContentChangeKind.Added => "Added to a local group",
                ContentChangeKind.Removed => "Removed from the local groups",
                _ => "Changed, or one of its dependencies"
            };
        }

        private static string Count(int count, string noun)
        {
            return count == 1 ? $"1 {noun}" : $"{count} {noun}s";
        }
    }
}