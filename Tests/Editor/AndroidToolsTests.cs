using System.IO;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class AndroidToolsTests
    {
        [Test]
        public void FromFolders_Unix_FindsAdbAndJavaInTheirFolders()
        {
            var tools = AndroidTools.FromFolders("/sdk", "/jdk", new string[0], false);

            Assert.That(tools.Adb, Is.EqualTo(Path.Combine("/sdk", "platform-tools", "adb")));
            Assert.That(tools.Java, Is.EqualTo(Path.Combine("/jdk", "bin", "java")));
            Assert.That(tools.Bundletool, Is.Null);
        }

        [Test]
        public void FromFolders_Windows_AddsTheExecutableExtension()
        {
            var tools = AndroidTools.FromFolders("/sdk", "/jdk", new string[0], true);

            Assert.That(tools.Adb, Does.EndWith("adb.exe"));
            Assert.That(tools.Java, Does.EndWith("java.exe"));
        }

        [Test]
        public void PickLatestBundletool_Versions_ComparesThemAsNumbers()
        {
            var latest = AndroidTools.PickLatestBundletool(new[] { "/tools/bundletool-all-1.9.0.jar", "/tools/bundletool-all-1.17.2.jar", "/tools/bundletool-all-1.15.6.jar" });

            Assert.That(latest, Is.EqualTo("/tools/bundletool-all-1.17.2.jar"));
        }

        [Test]
        public void PickLatestBundletool_OtherFiles_LeavesThemOut()
        {
            var latest = AndroidTools.PickLatestBundletool(new[] { "/tools/bundletool-all-beta.jar", "/tools/gradle.jar", "/tools/bundletool-all-1.8.0.jar" });

            Assert.That(latest, Is.EqualTo("/tools/bundletool-all-1.8.0.jar"));
        }

        [Test]
        public void PickLatestBundletool_None_GivesNull()
        {
            Assert.That(AndroidTools.PickLatestBundletool(new string[0]), Is.Null);
        }
    }
}