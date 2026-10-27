using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class ProjectDiagnosisTests
    {
        private const int IMAGE_AND_BACKGROUND_CONSTANT = 4;

        private ProjectState _state;
        private DiagnosisThresholds _thresholds;

        [SetUp]
        public void SetUp()
        {
            _state = new ProjectState { PackageLaunchScreen = true, IphoneLaunchScreen = IMAGE_AND_BACKGROUND_CONSTANT, IpadLaunchScreen = IMAGE_AND_BACKGROUND_CONSTANT, AndroidStaticSplashImage = true };
            _thresholds = new DiagnosisThresholds();
        }

        [Test]
        public void Check_ProjectWithoutInputSystemNorAddressables_HasTheChecksOfTheSettings()
        {
            var checks = ProjectDiagnosis.Check(_state, _thresholds).Select(result => result.Check);

            Assert.That(checks, Is.EqualTo(new[] { ProjectDiagnosis.UNITY_SPLASH_SCREEN, ProjectDiagnosis.PRELOADED_ASSETS_AND_RESOURCES, ProjectDiagnosis.LAUNCH_SCREENS, ProjectDiagnosis.UNITY_SERVICES }));
        }

        [Test]
        public void Check_InputSystemAndAddressablesBuiltWithRemoteCatalog_HasEveryCheck()
        {
            _state.InputSystem = true;
            _state.Addressables = new AddressablesState { RemoteCatalog = true, ContentChanges = new List<ContentChange>() };

            var checks = ProjectDiagnosis.Check(_state, _thresholds).Select(result => result.Check).ToList();

            Assert.That(checks, Has.Count.EqualTo(9));
            Assert.That(checks, Does.Contain(ProjectDiagnosis.INPUT_DEVICES).And.Contain(ProjectDiagnosis.REMOTE_CATALOG_CONTENT));
        }

        [Test]
        public void Check_AddressablesNeverBuilt_LeavesTheLocalContentOut()
        {
            _state.Addressables = new AddressablesState { RemoteCatalog = true };

            var checks = ProjectDiagnosis.Check(_state, _thresholds).Select(result => result.Check);

            Assert.That(checks, Does.Contain(ProjectDiagnosis.CATALOG_UPDATE).And.Not.Contain(ProjectDiagnosis.REMOTE_CATALOG_CONTENT));
        }

        [Test]
        public void Check_EveryResult_LinksToTheGuide()
        {
            _state.InputSystem = true;
            _state.Addressables = new AddressablesState { RemoteCatalog = true, ContentChanges = new List<ContentChange>() };

            foreach (var result in ProjectDiagnosis.Check(_state, _thresholds))
            {
                Assert.That(DiagnosisGuide.Anchors, Does.Contain(result.GuideSection), result.Check);
                Assert.That(result.Message, Is.Not.Empty, result.Check);
            }
        }

        [Test]
        public void CheckUnitySplashScreen_On_Warns()
        {
            _state.UnitySplashScreen = true;

            var result = ProjectDiagnosis.CheckUnitySplashScreen(_state);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Warning));
            Assert.That(result.Step, Is.EqualTo(StartupStep.FirstScene));
        }

        [Test]
        public void CheckUnitySplashScreen_Off_Passes()
        {
            Assert.That(ProjectDiagnosis.CheckUnitySplashScreen(_state).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckPreloadedAssetsAndResources_Nothing_Passes()
        {
            Assert.That(ProjectDiagnosis.CheckPreloadedAssetsAndResources(_state, _thresholds).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckPreloadedAssetsAndResources_BelowTheThreshold_ListsThemLargestFirst()
        {
            _state.PreloadedAssets.Add(new SizedAsset { Path = "Assets/Settings/Input.inputactions", Size = 1000 });
            _state.Resources.Add(new SizedAsset { Path = "Assets/Resources/Big.asset", Size = 5000 });
            _state.PreloadedAndResourcesSize = 6000;

            var result = ProjectDiagnosis.CheckPreloadedAssetsAndResources(_state, _thresholds);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Info));
            Assert.That(result.Items.Select(item => item.AssetPath), Is.EqualTo(new[] { "Assets/Resources/Big.asset", "Assets/Settings/Input.inputactions" }));
            Assert.That(result.Items[0].Detail, Does.StartWith("Resources"));
            Assert.That(result.Items[1].Detail, Does.StartWith("Preloaded Asset"));
        }

        [Test]
        public void CheckPreloadedAssetsAndResources_AboveTheThreshold_Warns()
        {
            _state.Resources.Add(new SizedAsset { Path = "Assets/Resources/Big.asset", Size = 2 * DiagnosisThresholds.MEGABYTE });
            _state.PreloadedAndResourcesSize = 2 * DiagnosisThresholds.MEGABYTE;

            Assert.That(ProjectDiagnosis.CheckPreloadedAssetsAndResources(_state, _thresholds).Level, Is.EqualTo(DiagnosisLevel.Warning));
        }

        [Test]
        public void CheckPreloadedAssetsAndResources_MoreAssetsThanListed_ListsTheLargest()
        {
            for (var i = 1; i <= 5; i++)
            {
                _state.Resources.Add(new SizedAsset { Path = $"Assets/Resources/{i}.asset", Size = i });
            }

            _thresholds.ListedAssets = 2;

            var result = ProjectDiagnosis.CheckPreloadedAssetsAndResources(_state, _thresholds);

            Assert.That(result.Items.Select(item => item.Label), Is.EqualTo(new[] { "Assets/Resources/5.asset", "Assets/Resources/4.asset" }));
            Assert.That(result.Message, Does.Contain("The 2 largest are listed."));
        }

        [Test]
        public void CheckLaunchScreens_LaunchScreenOfThePackage_Passes()
        {
            _state.IphoneLaunchScreen = 0;
            _state.AndroidStaticSplashImage = false;

            Assert.That(ProjectDiagnosis.CheckLaunchScreens(_state).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckLaunchScreens_DefaultScreensWithoutThePackage_WarnsForEachOne()
        {
            _state.PackageLaunchScreen = false;
            _state.IphoneLaunchScreen = 0;
            _state.IpadLaunchScreen = 0;
            _state.AndroidStaticSplashImage = false;

            var result = ProjectDiagnosis.CheckLaunchScreens(_state);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Warning));
            Assert.That(result.Step, Is.EqualTo(StartupStep.PerceivedWait));
            Assert.That(result.Items, Has.Count.EqualTo(3));
        }

        [Test]
        public void CheckLaunchScreens_ImagesWithoutThePackage_Passes()
        {
            _state.PackageLaunchScreen = false;

            Assert.That(ProjectDiagnosis.CheckLaunchScreens(_state).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckLaunchScreens_UnknownTypeWithoutThePackage_Passes()
        {
            _state.PackageLaunchScreen = false;
            _state.IphoneLaunchScreen = -1;
            _state.IpadLaunchScreen = -1;

            Assert.That(ProjectDiagnosis.CheckLaunchScreens(_state).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckUnityServices_Off_Passes()
        {
            Assert.That(ProjectDiagnosis.CheckUnityServices(_state).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckUnityServices_On_InformsOfEachOne()
        {
            _state.EngineDiagnostics = true;
            _state.AnalyticsOnStartup = true;
            _state.HardwareStatistics = true;

            var result = ProjectDiagnosis.CheckUnityServices(_state);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Info));
            Assert.That(result.Items.Select(item => item.Label), Is.EqualTo(new[] { "Engine Diagnostics", "Legacy Unity Analytics", "Hardware statistics" }));
        }

        [Test]
        public void CheckInputDevices_EveryDevice_Informs()
        {
            Assert.That(ProjectDiagnosis.CheckInputDevices(_state).Level, Is.EqualTo(DiagnosisLevel.Info));
        }

        [Test]
        public void CheckInputDevices_SupportedDevices_Passes()
        {
            _state.SupportedInputDevices = 2;

            var result = ProjectDiagnosis.CheckInputDevices(_state);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Passed));
            Assert.That(result.Message, Does.Contain("2 supported devices"));
        }

        [Test]
        public void CheckCatalogUpdate_RemoteCatalogUpdatedOnStartup_Warns()
        {
            var addressables = new AddressablesState { RemoteCatalog = true, UpdateCatalogsOnStartup = true, SettingsPath = "Assets/AddressableAssetsData/AddressableAssetSettings.asset" };

            var result = ProjectDiagnosis.CheckCatalogUpdate(addressables);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Warning));
            Assert.That(result.Items[0].AssetPath, Is.EqualTo(addressables.SettingsPath));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void CheckCatalogUpdate_NoRequestOnStartup_Passes(bool remoteCatalog, bool updateOnStartup)
        {
            var addressables = new AddressablesState { RemoteCatalog = remoteCatalog, UpdateCatalogsOnStartup = updateOnStartup };

            Assert.That(ProjectDiagnosis.CheckCatalogUpdate(addressables).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckLocalBundleCompression_LocalLzmaGroup_WarnsForItOnly()
        {
            var addressables = new AddressablesState();
            addressables.Groups.Add(new AddressablesGroupState { Name = "Base", Local = true, Lzma = true });
            addressables.Groups.Add(new AddressablesGroupState { Name = "Remote", Local = false, Lzma = true });
            addressables.Groups.Add(new AddressablesGroupState { Name = "Levels", Local = true, Lzma = false });

            var result = ProjectDiagnosis.CheckLocalBundleCompression(addressables);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Warning));
            Assert.That(result.Items.Select(item => item.Label), Is.EqualTo(new[] { "Base" }));
        }

        [Test]
        public void CheckLocalBundleCompression_RemoteLzmaGroup_Passes()
        {
            var addressables = new AddressablesState();
            addressables.Groups.Add(new AddressablesGroupState { Name = "Remote", Local = false, Lzma = true });

            Assert.That(ProjectDiagnosis.CheckLocalBundleCompression(addressables).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckJsonCatalog_Json_Informs()
        {
            Assert.That(ProjectDiagnosis.CheckJsonCatalog(new AddressablesState { JsonCatalog = true }).Level, Is.EqualTo(DiagnosisLevel.Info));
            Assert.That(ProjectDiagnosis.CheckJsonCatalog(new AddressablesState()).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckRemoteCatalogContent_NoChange_Passes()
        {
            var addressables = new AddressablesState { RemoteCatalog = true, ContentPlatform = "Android", ContentChanges = new List<ContentChange>() };

            var result = ProjectDiagnosis.CheckRemoteCatalogContent(addressables, _thresholds);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Passed));
            Assert.That(result.Message, Does.Contain("Android"));
        }

        [Test]
        public void CheckRemoteCatalogContent_Changes_WarnsAndNamesTheRemovedOnesByGuid()
        {
            var changes = new List<ContentChange>
            {
                new() { Guid = "1", Path = "Assets/Scenes/Level.unity", Kind = ContentChangeKind.Changed },
                new() { Guid = "2", Path = null, Kind = ContentChangeKind.Removed }
            };
            var addressables = new AddressablesState { RemoteCatalog = true, ContentPlatform = "iOS", ContentChanges = changes };

            var result = ProjectDiagnosis.CheckRemoteCatalogContent(addressables, _thresholds);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Warning));
            Assert.That(result.Items.Select(item => item.Label), Is.EqualTo(new[] { "Assets/Scenes/Level.unity", "2" }));
            Assert.That(result.Items[1].AssetPath, Is.Null);
            Assert.That(result.Message, Does.Contain("2 assets"));
        }
    }
}