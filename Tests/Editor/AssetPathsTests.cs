using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class AssetPathsTests
    {
        [TestCase("Assets/Resources/Settings.asset")]
        [TestCase("Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset")]
        [TestCase("Packages/com.unity.render-pipelines.core/Runtime/Debugging/Prefabs/Resources/DebugUICanvas.prefab")]
        public void IsInBuildResources_AssetInResources_IsTrue(string path)
        {
            Assert.That(AssetPaths.IsInBuildResources(path), Is.True);
        }

        [TestCase("Assets/Resources")]
        [TestCase("Assets/Textures/Icon.png")]
        [TestCase("Assets/Editor/Resources/Tool.asset")]
        [TestCase("Assets/Resources/Editor/Tool.asset")]
        [TestCase("Assets/Editor Default Resources/Icons/tool.png")]
        [TestCase("Assets/Resources/Loader.cs")]
        [TestCase("Assets/Resources/Level.unity")]
        [TestCase("Assets/MyResources/Settings.asset")]
        public void IsInBuildResources_OtherAsset_IsFalse(string path)
        {
            Assert.That(AssetPaths.IsInBuildResources(path), Is.False);
        }

        [TestCase("Assets/Textures/Icon.png")]
        [TestCase("Assets/Fonts/Saira/Saira-Regular SDF.asset")]
        public void CountsInBuild_Content_IsTrue(string path)
        {
            Assert.That(AssetPaths.CountsInBuild(path), Is.True);
        }

        [TestCase("Packages/com.unity.ugui/Editor Resources/Gizmos/TMP - Sprite Asset Icon.psd")]
        [TestCase("Assets/Gizmos/Spawn.png")]
        [TestCase("Assets/Scripts/Editor/Tool.cs")]
        [TestCase("Assets/Scripts/Runtime/Player.cs")]
        [TestCase("Assets/Scripts/Runtime/Game.asmdef")]
        [TestCase("Assets/Plugins/Native.dll")]
        public void CountsInBuild_EditorFileOrCode_IsFalse(string path)
        {
            Assert.That(AssetPaths.CountsInBuild(path), Is.False);
        }
    }
}