using NUnit.Framework;
using UnityEditor;

namespace Javienne.StartupDiagnostics.Tests
{
    public class MeasureBuildTests
    {
        [TestCase(true, "Rouflac.aab")]
        [TestCase(false, "Rouflac.apk")]
        public void GetFileName_Android_AddsTheExtensionOfTheBuild(bool appBundle, string expected)
        {
            Assert.That(MeasureBuild.GetFileName("Rouflac", BuildTarget.Android, appBundle), Is.EqualTo(expected));
        }

        [Test]
        public void GetFileName_iOS_GivesTheFolderAsIs()
        {
            Assert.That(MeasureBuild.GetFileName("Rouflac", BuildTarget.iOS, false), Is.EqualTo("Rouflac"));
        }

        [TestCase("My: Game?", "My_ Game_.apk")]
        [TestCase(" A/B\\C ", "A_B_C.apk")]
        [TestCase("   ", "Game.apk")]
        public void GetFileName_ProductName_ReplacesWhatFileSystemsRefuse(string productName, string expected)
        {
            Assert.That(MeasureBuild.GetFileName(productName, BuildTarget.Android, false), Is.EqualTo(expected));
        }
    }
}