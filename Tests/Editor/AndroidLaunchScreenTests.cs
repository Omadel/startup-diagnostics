using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Javienne.StartupDiagnostics.Tests
{
    public class AndroidLaunchScreenTests
    {
        private const string STYLES = "<resources>\n  <style name=\"BaseUnityGameActivityTheme\" parent=\"Theme.AppCompat.Light.NoActionBar\">\n    <item name=\"android:windowSplashScreenAnimatedIcon\">@color/windowSplashScreenAnimatedIconColor</item>\n  </style>\n</resources>";
        private const string MANIFEST = "<manifest>\n  <application>\n    <activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" />\n  </application>\n</manifest>";

        private string _folder;
        private string _unityLibrary;
        private string _launcherResources;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "StartupDiagnosticsTests-" + Guid.NewGuid().ToString("N"));
            _unityLibrary = Path.Combine(_folder, "unityLibrary");
            _launcherResources = Path.Combine(_folder, "launcher", "src", "main", "res");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [Test]
        public void ReplaceSplashIcon_TransparentIcon_IsTheSplashIconOfThePackage()
        {
            var styles = AndroidLaunchScreen.ReplaceSplashIcon(STYLES);

            Assert.That(styles, Does.Contain($"<item name=\"android:windowSplashScreenAnimatedIcon\">@drawable/{AndroidLaunchScreen.SPLASH_ICON}</item>"));
        }

        [Test]
        public void ReplaceSplashIcon_NoTransparentIcon_IsNull()
        {
            Assert.That(AndroidLaunchScreen.ReplaceSplashIcon("<resources />"), Is.Null);
            Assert.That(AndroidLaunchScreen.ReplaceSplashIcon(null), Is.Null);
        }

        [Test]
        public void AddSplashScreenExit_Manifest_DeclaresTheProviderOnceInTheApplication()
        {
            var manifest = AndroidLaunchScreen.AddSplashScreenExit(MANIFEST);
            var again = AndroidLaunchScreen.AddSplashScreenExit(manifest);

            Assert.That(manifest, Does.Contain($"<provider android:name=\"{AndroidLaunchScreen.SPLASH_SCREEN_EXIT}\" android:authorities=\"${{applicationId}}.splash-screen-exit\" android:exported=\"false\" />\n  </application>"));
            Assert.That(again, Is.EqualTo(manifest));
        }

        [Test]
        public void AddSplashScreenExit_NoApplication_IsNull()
        {
            Assert.That(AndroidLaunchScreen.AddSplashScreenExit("<manifest />"), Is.Null);
        }

        [TestCase(512, ExpectedResult = 768)]
        [TestCase(500, ExpectedResult = 750)]
        [TestCase(10, ExpectedResult = 16)]
        public int GetLayerSize_ImageSize_PutsTheImageInTheMiddleTwoThirds(int imageSize)
        {
            return AndroidLaunchScreen.GetLayerSize(imageSize);
        }

        [TestCase(160, ExpectedResult = 192)]
        [TestCase(420, ExpectedResult = 504)]
        [TestCase(640, ExpectedResult = 768)]
        public int GetIconSize_Density_IsThePixelsOf192Dp(int density)
        {
            return AndroidLaunchScreen.GetIconSize(density);
        }

        [Test]
        public void Write_Icon_WritesTheThemeTheSplashIconTheStaticImagesAndTheProvider()
        {
            CreateUnityLibrary();

            AndroidLaunchScreen.Write(_unityLibrary, Icon(), Color.black, Assert.Fail);

            Assert.That(File.ReadAllText(Path.Combine(_launcherResources, "values-v31", "styles.xml")), Does.Contain(AndroidLaunchScreen.SPLASH_ICON));
            Assert.That(File.Exists(Path.Combine(_launcherResources, "drawable-v31", $"{AndroidLaunchScreen.SPLASH_ICON}.xml")), Is.True);
            var foreground = LaunchScreenImage.Decode(File.ReadAllBytes(Path.Combine(_launcherResources, "drawable-nodpi", $"{AndroidLaunchScreen.SPLASH_FOREGROUND}.png")));
            Assert.That(foreground.Size, Is.EqualTo(48));
            var staticImage = LaunchScreenImage.Decode(File.ReadAllBytes(Path.Combine(_launcherResources, "drawable-420dpi", $"{AndroidLaunchScreen.STATIC_SPLASH_IMAGE}.jpg")));
            Assert.That(staticImage.Size, Is.EqualTo(504));
            Assert.That(Directory.GetDirectories(_launcherResources, "drawable-*dpi").Length, Is.EqualTo(25));
            Assert.That(File.ReadAllText(Path.Combine(_unityLibrary, "src", "main", "AndroidManifest.xml")), Does.Contain(AndroidLaunchScreen.SPLASH_SCREEN_EXIT));
        }

        [Test]
        public void Write_NoIconAfterAnIcon_RemovesTheFilesOfThePreviousBuild()
        {
            CreateUnityLibrary();
            AndroidLaunchScreen.Write(_unityLibrary, Icon(), Color.black, Assert.Fail);
            var otherImage = Path.Combine(_launcherResources, "drawable-420dpi", "other.png");
            File.WriteAllText(otherImage, string.Empty);

            AndroidLaunchScreen.Write(_unityLibrary, null, Color.black, Assert.Fail);

            Assert.That(File.Exists(Path.Combine(_launcherResources, "values-v31", "styles.xml")), Is.False);
            Assert.That(File.Exists(Path.Combine(_launcherResources, "drawable-v31", $"{AndroidLaunchScreen.SPLASH_ICON}.xml")), Is.False);
            Assert.That(Directory.GetFiles(_launcherResources, "*.jpg", SearchOption.AllDirectories), Is.Empty);
            Assert.That(File.Exists(otherImage), Is.True);
        }

        [Test]
        public void Write_NoTransparentIconInTheTheme_WarnsAndWritesTheRest()
        {
            CreateUnityLibrary("<resources />");
            var warnings = new List<string>();

            AndroidLaunchScreen.Write(_unityLibrary, Icon(), Color.black, warnings.Add);

            Assert.That(warnings.Single(), Does.Contain("windowSplashScreenAnimatedIconColor"));
            Assert.That(File.Exists(Path.Combine(_launcherResources, "values-v31", "styles.xml")), Is.False);
            Assert.That(File.Exists(Path.Combine(_launcherResources, "drawable-420dpi", $"{AndroidLaunchScreen.STATIC_SPLASH_IMAGE}.jpg")), Is.True);
        }

        private void CreateUnityLibrary(string styles = STYLES)
        {
            var main = Path.Combine(_unityLibrary, "src", "main");
            Directory.CreateDirectory(Path.Combine(main, "res", "values-v31"));
            File.WriteAllText(Path.Combine(main, "res", "values-v31", "styles.xml"), styles);
            File.WriteAllText(Path.Combine(main, "AndroidManifest.xml"), MANIFEST);
            Directory.CreateDirectory(_launcherResources);
        }

        private static LaunchScreenImage Icon()
        {
            return new LaunchScreenImage(Enumerable.Repeat(Color.white, 32 * 32).ToArray(), 32);
        }
    }
}