using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Javienne.StartupDiagnostics.Tests
{
    public class DiagnosisGuideTests
    {
        [Test]
        public void GetUrl_Version_PointsToItsTag()
        {
            var url = DiagnosisGuide.GetUrl("1.1.0", DiagnosisGuide.UNITY_SPLASH_SCREEN);

            Assert.That(url, Is.EqualTo("https://github.com/Omadel/startup-diagnostics/blob/v1.1.0/Documentation~/optimization-guide.md#turn-off-the-splash-screen-of-unity"));
        }

        [Test]
        public void GetUrl_NoVersion_PointsToTheMainBranch()
        {
            Assert.That(DiagnosisGuide.GetUrl(null, DiagnosisGuide.JSON_CATALOG), Does.Contain("/blob/main/"));
        }

        [TestCase("Turn off the splash screen of Unity", "turn-off-the-splash-screen-of-unity")]
        [TestCase("Disable OnGUI until it draws", "disable-ongui-until-it-draws")]
        [TestCase("Engine initialization", "engine-initialization")]
        [TestCase("`scripts_start`, then the first scene", "scripts_start-then-the-first-scene")]
        public void GetAnchor_Heading_IsTheAnchorOfGitHub(string heading, string anchor)
        {
            Assert.That(DiagnosisGuide.GetAnchor(heading), Is.EqualTo(anchor));
        }

        [Test]
        public void Anchors_EveryOne_IsAHeadingOfTheGuide()
        {
            var package = PackageInfo.FindForAssembly(typeof(DiagnosisGuide).Assembly);
            var guide = File.ReadAllLines(Path.Combine(package.resolvedPath, DiagnosisGuide.GUIDE_PATH));
            var headings = guide.Where(line => line.StartsWith("#")).Select(line => DiagnosisGuide.GetAnchor(line.TrimStart('#'))).ToList();

            Assert.That(headings, Is.SupersetOf(DiagnosisGuide.Anchors));
        }
    }
}