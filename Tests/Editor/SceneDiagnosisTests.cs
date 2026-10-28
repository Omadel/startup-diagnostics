using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Javienne.StartupDiagnostics.Tests
{
    public class SceneDiagnosisTests
    {
        private static readonly Color _splash = new(0.137f, 0.122f, 0.125f, 1);

        private SceneState _boot;
        private SceneState _level;
        private DiagnosisThresholds _thresholds;

        [SetUp]
        public void SetUp()
        {
            _boot = new SceneState { Path = "Assets/Scenes/Boot.unity", FirstScene = true };
            _boot.Cameras.Add(new CameraState { Name = "Camera", ClearFlags = CameraClearFlags.SolidColor, Background = _splash, CullingMask = -1 });
            _level = new SceneState { Path = "Assets/Scenes/Level.unity" };
            _thresholds = new DiagnosisThresholds();
        }

        [Test]
        public void Check_FirstAndPlayableScenes_HasTheContentOfEachThenTheOtherChecks()
        {
            var checks = SceneDiagnosis.Check(new[] { _boot, _level }, false, _splash, _thresholds).Select(result => result.Check);

            Assert.That(checks, Is.EqualTo(new[] { SceneDiagnosis.SCENE_CONTENT, SceneDiagnosis.SCENE_CONTENT, SceneDiagnosis.ANIMATION_COMPRESSION, SceneDiagnosis.TEXTURE_SIZE, SceneDiagnosis.ONGUI, SceneDiagnosis.UI_DOCUMENTS, SceneDiagnosis.FIRST_FRAME }));
        }

        [Test]
        public void Check_NoScene_IsEmpty()
        {
            Assert.That(SceneDiagnosis.Check(new SceneState[0], false, _splash, _thresholds), Is.Empty);
        }

        [Test]
        public void Check_EveryResult_LinksToTheGuide()
        {
            foreach (var result in SceneDiagnosis.Check(new[] { _boot, _level }, false, _splash, _thresholds))
            {
                Assert.That(DiagnosisGuide.Anchors, Does.Contain(result.GuideSection), result.Check);
            }
        }

        [Test]
        public void CheckContent_HeavyFirstScene_WarnsAndListsTheLargestWithTheirChain()
        {
            _boot.Assets.Add(new SizedAsset { Path = "Assets/Big.png", Size = 2 * DiagnosisThresholds.MEGABYTE });
            _boot.Assets.Add(new SizedAsset { Path = "Assets/Small.png", Size = 1000 });
            _boot.Size = 2 * DiagnosisThresholds.MEGABYTE + 1000;
            _boot.Chains["Assets/Big.png"] = "Boot.unity (Camera › Loader._image)";

            var result = SceneDiagnosis.CheckContent(_boot, _thresholds);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Warning));
            Assert.That(result.Step, Is.EqualTo(StartupStep.FirstScene));
            Assert.That(result.GuideSection, Is.EqualTo(DiagnosisGuide.SMALL_BOOT_SCENE));
            Assert.That(result.Items[0].Detail, Does.EndWith(", through Boot.unity (Camera › Loader._image)"));
            Assert.That(result.Items[1].Detail, Does.Not.Contain("through"));
        }

        [Test]
        public void CheckContent_LightFirstScene_Informs()
        {
            _boot.Assets.Add(new SizedAsset { Path = "Assets/Small.png", Size = 1000 });
            _boot.Size = 1000;

            Assert.That(SceneDiagnosis.CheckContent(_boot, _thresholds).Level, Is.EqualTo(DiagnosisLevel.Info));
        }

        [Test]
        public void CheckContent_PlayableScene_InformsInTheMilestonesOfTheProject()
        {
            _level.Assets.Add(new SizedAsset { Path = "Assets/Big.png", Size = 20 * DiagnosisThresholds.MEGABYTE });
            _level.Size = 20 * DiagnosisThresholds.MEGABYTE;
            _level.LayoutDate = "2026-10-27 20:21";

            var result = SceneDiagnosis.CheckContent(_level, _thresholds);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Info));
            Assert.That(result.Step, Is.EqualTo(StartupStep.ProjectMilestones));
            Assert.That(result.Message, Does.Contain("build layout of Addressables of 2026-10-27 20:21"));
        }

        [Test]
        public void CheckContent_ManyAssets_ListsAsManyAsTheSettings()
        {
            for (var i = 0; i < 5; i++)
            {
                _level.Assets.Add(new SizedAsset { Path = $"Assets/{i}.png", Size = 100 - i });
            }

            _thresholds.ListedAssets = 3;

            Assert.That(SceneDiagnosis.CheckContent(_level, _thresholds).Items, Has.Count.EqualTo(3));
        }

        [Test]
        public void CheckAnimationCompression_UncompressedModelInThePlayableScene_Warns()
        {
            _level.UncompressedModels.Add(new ModelState { Path = "Assets/Dog/Dog.fbx", Clips = 58, Size = 28 * DiagnosisThresholds.MEGABYTE });

            var result = SceneDiagnosis.CheckAnimationCompression(new[] { _boot, _level }, _thresholds);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Warning));
            Assert.That(result.Step, Is.EqualTo(StartupStep.ProjectMilestones));
            Assert.That(result.Items[0].Detail, Does.StartWith("Level: 58 clips"));
        }

        [Test]
        public void CheckAnimationCompression_ModelInBothScenes_IsListedOnceInTheFirstScene()
        {
            var model = new ModelState { Path = "Assets/Dog/Dog.fbx", Clips = 1, Size = 1000 };
            _boot.UncompressedModels.Add(model);
            _level.UncompressedModels.Add(model);

            var result = SceneDiagnosis.CheckAnimationCompression(new[] { _boot, _level }, _thresholds);

            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Step, Is.EqualTo(StartupStep.FirstScene));
        }

        [Test]
        public void CheckAnimationCompression_NoModel_Passes()
        {
            Assert.That(SceneDiagnosis.CheckAnimationCompression(new[] { _boot, _level }, _thresholds).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckTextureSize_LargeOnOnePlatform_WarnsOnceForTheTexture()
        {
            var texture = new TextureState { Path = "Assets/Dog/Golden.png", AndroidSize = 1024, IosSize = 2048 };
            _boot.Textures.Add(texture);
            _level.Textures.Add(texture);
            _level.Textures.Add(new TextureState { Path = "Assets/Small.png", AndroidSize = 512, IosSize = 512 });

            var result = SceneDiagnosis.CheckTextureSize(new[] { _boot, _level }, _thresholds);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Warning));
            Assert.That(result.Step, Is.EqualTo(StartupStep.FirstScene));
            Assert.That(result.Items.Select(item => item.Label), Is.EqualTo(new[] { "Assets/Dog/Golden.png" }));
        }

        [Test]
        public void CheckTextureSize_AtTheThreshold_Passes()
        {
            _level.Textures.Add(new TextureState { Path = "Assets/Dog/Golden.png", AndroidSize = 1024, IosSize = 1024 });

            Assert.That(SceneDiagnosis.CheckTextureSize(new[] { _level }, _thresholds).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckTextureSize_MoreThanListed_SaysHowManyMore()
        {
            for (var i = 0; i < 4; i++)
            {
                _level.Textures.Add(new TextureState { Path = $"Assets/{i}.png", AndroidSize = 2048, IosSize = 2048 });
            }

            _thresholds.ListedAssets = 2;

            var result = SceneDiagnosis.CheckTextureSize(new[] { _level }, _thresholds);

            Assert.That(result.Items, Has.Count.EqualTo(3));
            Assert.That(result.Items[2].Label, Is.EqualTo("And 2 more"));
        }

        [Test]
        public void CheckOnGui_ComponentInThePlayableScene_WarnsInTheMilestonesOfTheProject()
        {
            _level.OnGuiComponents.Add("Test/Panel › TestPanelDrawer");

            var result = SceneDiagnosis.CheckOnGui(new[] { _boot, _level });

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Warning));
            Assert.That(result.Step, Is.EqualTo(StartupStep.ProjectMilestones));
            Assert.That(result.Items[0].Detail, Is.EqualTo("Level"));
        }

        [Test]
        public void CheckOnGui_ComponentInTheFirstScene_WarnsInTheFirstFrame()
        {
            _boot.OnGuiComponents.Add("Debug › Console");

            Assert.That(SceneDiagnosis.CheckOnGui(new[] { _boot }).Step, Is.EqualTo(StartupStep.FirstFrame));
        }

        [Test]
        public void CheckContent_NoAsset_Passes()
        {
            var result = SceneDiagnosis.CheckContent(_boot, _thresholds);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Passed));
            Assert.That(result.Message, Does.Contain("references no asset"));
        }

        [Test]
        public void CheckUiDocuments_DocumentInTheFirstScene_InformsInTheFirstScene()
        {
            _boot.UiDocuments.Add("Canvas › UIDocument");

            var result = SceneDiagnosis.CheckUiDocuments(new[] { _boot, _level });

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Info));
            Assert.That(result.Step, Is.EqualTo(StartupStep.FirstScene));
            Assert.That(result.Items[0].Detail, Is.EqualTo("Boot"));
        }

        [Test]
        public void CheckUiDocuments_NoDocument_Passes()
        {
            Assert.That(SceneDiagnosis.CheckUiDocuments(new[] { _boot, _level }).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckFirstFrame_LaunchScreenOfThePackage_Passes()
        {
            _boot.Cameras.Clear();

            Assert.That(SceneDiagnosis.CheckFirstFrame(_boot, true, _splash).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckFirstFrame_CameraOnTheSplashBackground_Passes()
        {
            Assert.That(SceneDiagnosis.CheckFirstFrame(_boot, false, _splash).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckFirstFrame_OtherBackgroundAndNoLayer_WarnsForBoth()
        {
            _boot.Cameras[0].Background = Color.blue;
            _boot.Cameras[0].CullingMask = 0;

            var result = SceneDiagnosis.CheckFirstFrame(_boot, false, _splash);

            Assert.That(result.Level, Is.EqualTo(DiagnosisLevel.Warning));
            Assert.That(result.Items, Has.Count.EqualTo(2));
            Assert.That(result.Items[0].Detail, Does.StartWith("Background #0000FF"));
        }

        [Test]
        public void CheckFirstFrame_OverlayCameraOnTop_ChecksTheCameraThatClears()
        {
            _boot.Cameras.Add(new CameraState { Name = "Overlay", Depth = 10, ClearFlags = CameraClearFlags.Depth, Background = Color.blue, CullingMask = 0 });

            Assert.That(SceneDiagnosis.CheckFirstFrame(_boot, false, _splash).Level, Is.EqualTo(DiagnosisLevel.Passed));
        }

        [Test]
        public void CheckFirstFrame_SkyboxOrNoCamera_Warns()
        {
            _boot.Cameras[0].ClearFlags = CameraClearFlags.Skybox;
            Assert.That(SceneDiagnosis.CheckFirstFrame(_boot, false, _splash).Items[0].Detail, Does.StartWith("Clears to the skybox"));

            _boot.Cameras.Clear();
            Assert.That(SceneDiagnosis.CheckFirstFrame(_boot, false, _splash).Items[0].Detail, Is.EqualTo("No active camera clears the screen"));
        }

        [Test]
        public void Check_PlayableSceneOnly_HasNoFirstFrame()
        {
            var checks = SceneDiagnosis.Check(new List<SceneState> { _level }, false, _splash, _thresholds).Select(result => result.Check);

            Assert.That(checks, Does.Not.Contain(SceneDiagnosis.FIRST_FRAME));
        }
    }
}