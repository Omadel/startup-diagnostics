using NUnit.Framework;
using UnityEngine;

namespace Javienne.StartupDiagnostics.Tests
{
    public class ScenePathsTests
    {
        private const string BOOT = "Assets/Scenes/Boot.unity";
        private const string LEVEL = "Assets/Scenes/Level.unity";

        [Test]
        public void Get_FirstEnabledSceneThenPlayableScenes_EachOnce()
        {
            var buildScenes = new[] { (LEVEL, false), (BOOT, true), ("Assets/Scenes/Credits.unity", true) };

            var paths = ScenePaths.Get(buildScenes, new[] { LEVEL, BOOT, "", LEVEL }, out var firstScene);

            Assert.That(firstScene, Is.EqualTo(BOOT));
            Assert.That(paths, Is.EqualTo(new[] { BOOT, LEVEL }));
        }

        [Test]
        public void Get_NoSceneInTheBuild_HasNoFirstScene()
        {
            var paths = ScenePaths.Get(new (string, bool)[0], new[] { LEVEL }, out var firstScene);

            Assert.That(firstScene, Is.Null);
            Assert.That(paths, Is.EqualTo(new[] { LEVEL }));
        }

        [TestCase(2048, 1024, 1024, 1024)]
        [TestCase(512, 512, 1024, 512)]
        [TestCase(4096, 2048, 2048, 2048)]
        [TestCase(300, 1500, 8192, 1500)]
        public void GetImportedTextureSize_SourceAndMaxSize_IsTheSmallest(int width, int height, int maxSize, int size)
        {
            Assert.That(SceneStateReader.GetImportedTextureSize(width, height, maxSize), Is.EqualTo(size));
        }

        [Test]
        public void Declares_TypeWithOnGui_IsTrueForItAndItsSubtypes()
        {
            Assert.That(OnGuiTypes.Declares(typeof(Panel)), Is.True);
            Assert.That(OnGuiTypes.Declares(typeof(DebugPanel)), Is.True);
            Assert.That(OnGuiTypes.Declares(typeof(Mover)), Is.False);
        }

        [Test]
        public void IsEditorOnly_OnGuiUnderUnityEditor_IsTrue()
        {
            const string source = "class Brain {\n#if UNITY_EDITOR\n    void OnPreCull() { }\n    void OnGUI()\n    { }\n#endif\n}";

            Assert.That(OnGuiTypes.IsEditorOnly(source), Is.True);
        }

        [Test]
        public void IsEditorOnly_OnGuiInTheElseOfUnityEditor_IsFalse()
        {
            const string source = "#if UNITY_EDITOR\nvoid Draw() { }\n#else\nvoid OnGUI() { }\n#endif";

            Assert.That(OnGuiTypes.IsEditorOnly(source), Is.False);
        }

        [Test]
        public void IsEditorOnly_OnGuiInTheElseOfNotUnityEditor_IsTrue()
        {
            const string source = "#if !UNITY_EDITOR\nvoid Draw() { }\n#else\nprivate void OnGUI() { }\n#endif";

            Assert.That(OnGuiTypes.IsEditorOnly(source), Is.True);
        }

        [Test]
        public void IsEditorOnly_OnGuiUnderAnotherSymbolOrOutsideBlocks_IsFalse()
        {
            Assert.That(OnGuiTypes.IsEditorOnly("#if DEBUG_PANEL\nvoid OnGUI() { }\n#endif"), Is.False);
            Assert.That(OnGuiTypes.IsEditorOnly("public class Panel {\n    private void OnGUI() { }\n}"), Is.False);
            Assert.That(OnGuiTypes.IsEditorOnly("public class Mover { }"), Is.False);
        }

        [Test]
        public void IsEditorOnly_NestedUnityEditorBlock_IsTrue()
        {
            const string source = "#if UNITY_EDITOR\n#if CINEMACHINE_UIELEMENTS\nvoid Handler() { }\n#endif\nvoid OnGUI() { }\n#endif";

            Assert.That(OnGuiTypes.IsEditorOnly(source), Is.True);
        }

        private class Panel : MonoBehaviour
        {
            // ReSharper disable once UnusedMember.Local
            private void OnGUI()
            {
            }
        }

        private class DebugPanel : Panel
        {
        }

        private class Mover : MonoBehaviour
        {
        }
    }
}