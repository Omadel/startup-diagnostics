using NUnit.Framework;
using UnityEditor;

namespace Javienne.StartupDiagnostics.Tests
{
    public class MeasureChecksTests
    {
        private const string BUILD = "Build/StartupDiagnostics/Android/Rouflac.aab";

        [Test]
        public void GetBlockingIssue_NewBuildOnTheActiveTarget_IsNull()
        {
            Assert.That(MeasureChecks.GetBlockingIssue(new MeasureSettings(), BUILD, false, BuildTarget.Android, false), Is.Null);
        }

        [Test]
        public void GetBlockingIssue_NewBuildOnAnotherTarget_AsksToSwitch()
        {
            var issue = MeasureChecks.GetBlockingIssue(new MeasureSettings(), BUILD, true, BuildTarget.iOS, true);

            Assert.That(issue, Does.StartWith("The active build target is iOS: switch it to Android"));
        }

        [Test]
        public void GetBlockingIssue_LastBuildOnAnotherTarget_IsNull()
        {
            var settings = new MeasureSettings { Rebuild = false };

            Assert.That(MeasureChecks.GetBlockingIssue(settings, BUILD, true, BuildTarget.iOS, true), Is.Null);
        }

        [Test]
        public void GetBlockingIssue_LastBuildMissing_SaysWhere()
        {
            var settings = new MeasureSettings { Rebuild = false };

            Assert.That(MeasureChecks.GetBlockingIssue(settings, BUILD, false, BuildTarget.Android, true), Is.EqualTo($"No build at {BUILD}: measure a new build."));
        }

        [Test]
        public void GetBlockingIssue_GameOnTheDevice_NeedsNoBuild()
        {
            var settings = new MeasureSettings { Rebuild = false, CleanInstall = false };

            Assert.That(MeasureChecks.GetBlockingIssue(settings, BUILD, false, BuildTarget.iOS, true), Is.Null);
        }

        [Test]
        public void GetBlockingIssue_NewBuildWithoutCleanInstall_AsksForOne()
        {
            var settings = new MeasureSettings { CleanInstall = false };

            Assert.That(MeasureChecks.GetBlockingIssue(settings, BUILD, true, BuildTarget.Android, true), Does.Contain("clean install"));
        }

        [Test]
        public void GetBlockingIssue_iOSOutsideAMac_NeedsOne()
        {
            var settings = new MeasureSettings { Platform = BuildTarget.iOS };

            Assert.That(MeasureChecks.GetBlockingIssue(settings, "Build/iOS/Rouflac", true, BuildTarget.iOS, false), Is.EqualTo("The measure on iOS needs a Mac with Xcode."));
        }

        [Test]
        public void GetBlockingIssue_OtherPlatform_IsRefused()
        {
            var settings = new MeasureSettings { Platform = BuildTarget.StandaloneOSX };

            Assert.That(MeasureChecks.GetBlockingIssue(settings, "Build/Game.app", true, BuildTarget.StandaloneOSX, true), Is.EqualTo("The measure runs on Android and iOS, not on StandaloneOSX."));
        }

        [TestCase(0, 15, 5)]
        [TestCase(10, -1, 5)]
        [TestCase(10, 15, -1)]
        public void GetBlockingIssue_InvalidLaunches_IsRefused(int runs, float firstLaunchWait, float launchWait)
        {
            var settings = new MeasureSettings { Runs = runs, FirstLaunchWait = firstLaunchWait, LaunchWait = launchWait };

            Assert.That(MeasureChecks.GetBlockingIssue(settings, BUILD, true, BuildTarget.Android, true), Is.Not.Null);
        }

        [Test]
        public void GetConfirmation_NewBuild_NamesTheGameTheDeviceAndTheLaunches()
        {
            var confirmation = MeasureChecks.GetConfirmation(new MeasureSettings { Runs = 10 }, "com.javienne.rouflac", "Pixel 6", BUILD);

            Assert.That(confirmation, Does.StartWith($"The measure builds the game to {BUILD}, uninstalls com.javienne.rouflac from Pixel 6, which deletes its data on the device, installs the new build and launches it 11 times."));
        }

        [Test]
        public void GetConfirmation_LastBuild_NamesTheBuild()
        {
            var confirmation = MeasureChecks.GetConfirmation(new MeasureSettings { Rebuild = false, Runs = 5 }, "com.javienne.rouflac", "iPad (A16) (iPad15,7)", "Build/iOS/Rouflac");

            Assert.That(confirmation, Does.StartWith("The measure uninstalls com.javienne.rouflac from iPad (A16) (iPad15,7), which deletes its data on the device, installs the build at Build/iOS/Rouflac and launches it 6 times."));
        }
    }
}