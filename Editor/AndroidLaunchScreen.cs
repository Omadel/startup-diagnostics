using System;
using System.IO;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Shows the icon of the launch screen on the launch screens of an Android build, from the splash screen of the
    ///     system to the first frame of the first scene, in a square of 192 dp at the centre of the splash background (the
    ///     splash background colour of the Player Settings), where <see cref="LaunchScreen" /> draws it too.
    ///     <list type="bullet">
    ///         <item>
    ///             The splash screen of Android 12 and later, which Unity 6000.3 leaves empty: its theme gives a transparent
    ///             colour as splash icon. The theme of the Unity library module is copied into the launcher module, whose
    ///             resources take precedence, with an adaptive icon as splash icon whose foreground has the image in its
    ///             middle two thirds. Without icon background colour in the theme, the system draws the layers at 288 dp
    ///             and shows their middle 192 dp, cropped to the shape of the icons of the device: a circle on Pixel phones,
    ///             measured 502 px at 420 dpi on the Pixel 6.
    ///         </item>
    ///         <item>
    ///             The static splash view of Unity, which takes over from the system when the app draws its first frame,
    ///             long before the first scene: it draws the image <c>unity_static_splash</c> at its size in pixels. The
    ///             image is written in the launcher module at the size of 192 dp for each screen density.
    ///         </item>
    ///         <item>
    ///             The exit of the splash screen of the system, which fades the icon out before fading the app in: the
    ///             plugin <c>SplashScreenExit.java</c>, declared in the manifest of the Unity library module, removes it at
    ///             once instead.
    ///         </item>
    ///     </list>
    /// </summary>
    internal static class AndroidLaunchScreen
    {
        public const int ICON_SIZE = (int)LaunchScreen.ICON_SIZE;
        public const string SPLASH_ICON = "startup_diagnostics_splash_icon";
        public const string SPLASH_FOREGROUND = "startup_diagnostics_splash_foreground";
        public const string STATIC_SPLASH_IMAGE = "unity_static_splash";
        public const string SPLASH_SCREEN_EXIT = "com.javienne.startupdiagnostics.SplashScreenExit";

        private const string TRANSPARENT_ICON = "@color/windowSplashScreenAnimatedIconColor";
        private const string APPLICATION_END = "</application>";
        private const string STYLES_FOLDER = "values-v31";
        private const string STYLES_FILE = "styles.xml";
        private const string SPLASH_ICON_FOLDER = "drawable-v31";
        private const string SPLASH_FOREGROUND_FOLDER = "drawable-nodpi";
        private const int JPEG_QUALITY = 90;
        private const int BASELINE_DENSITY = 160;

        /// <summary>
        ///     Screen densities of Android, in dots per inch: the <c>DENSITY_</c> constants of <c>DisplayMetrics</c>.
        /// </summary>
        private static readonly int[] _densities = { 120, 140, 160, 180, 200, 213, 220, 240, 260, 280, 300, 320, 340, 360, 390, 400, 420, 440, 450, 480, 520, 560, 600, 640 };

        /// <summary>
        ///     Sets up the launch screens in the Gradle project of the Unity library module whose folder Unity passes, once
        ///     the files of a previous build are removed: without icon, the build keeps the launch screens of Unity.
        /// </summary>
        internal static void Write(string unityLibrary, LaunchScreenImage icon, Color background, Action<string> logWarning)
        {
            var launcherResources = Path.Combine(unityLibrary, "..", "launcher", "src", "main", "res");
            RemovePrevious(launcherResources);
            if (icon == null) return;

            WriteSplashTheme(unityLibrary, launcherResources, logWarning);
            WriteSplashIcon(launcherResources, icon);
            WriteStaticSplashImages(launcherResources, icon, background);
            DeclareSplashScreenExit(Path.Combine(unityLibrary, "src", "main", "AndroidManifest.xml"), logWarning);
        }

        /// <summary>
        ///     The styles of the theme with the splash icon of the package in place of the transparent one, or null if they
        ///     have no transparent splash icon.
        /// </summary>
        internal static string ReplaceSplashIcon(string styles)
        {
            return styles != null && styles.Contains(TRANSPARENT_ICON) ? styles.Replace(TRANSPARENT_ICON, $"@drawable/{SPLASH_ICON}") : null;
        }

        /// <summary>
        ///     The manifest with the content provider of <c>SplashScreenExit.java</c>, private to the app, created with it
        ///     before any activity: unchanged if it has it, null if it has no application element.
        /// </summary>
        internal static string AddSplashScreenExit(string manifest)
        {
            if (manifest == null || !manifest.Contains(APPLICATION_END)) return null;

            if (manifest.Contains(SPLASH_SCREEN_EXIT)) return manifest;

            var provider = $"  <provider android:name=\"{SPLASH_SCREEN_EXIT}\" android:authorities=\"${{applicationId}}.splash-screen-exit\" android:exported=\"false\" />\n  ";
            return manifest.Replace(APPLICATION_END, provider + APPLICATION_END);
        }

        /// <summary>
        ///     The adaptive icon of the splash screen, whose foreground layer has the image in its middle two thirds.
        /// </summary>
        internal static string GetSplashIconXml()
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- Splash icon of the launch screen of Startup Diagnostics: an adaptive icon, drawn at 288 dp, whose middle 192 dp show. -->
<adaptive-icon xmlns:android=""http://schemas.android.com/apk/res/android"">
    <background android:drawable=""@android:color/transparent"" />
    <foreground android:drawable=""@drawable/{SPLASH_FOREGROUND}"" />
</adaptive-icon>
";
        }

        /// <summary>
        ///     The side of a layer of adaptive icon whose middle two thirds are an image of the given side.
        /// </summary>
        internal static int GetLayerSize(int imageSize)
        {
            return imageSize + 2 * (int)Math.Round(imageSize / 4.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        ///     The side in pixels of 192 dp at a screen density.
        /// </summary>
        internal static int GetIconSize(int density)
        {
            return ICON_SIZE * density / BASELINE_DENSITY;
        }

        /// <summary>
        ///     Removes the files that a previous build with the launch screen on wrote into the launcher module.
        /// </summary>
        private static void RemovePrevious(string launcherResources)
        {
            var styles = Path.Combine(launcherResources, STYLES_FOLDER, STYLES_FILE);
            if (File.Exists(styles) && File.ReadAllText(styles).Contains(SPLASH_ICON))
            {
                File.Delete(styles);
            }

            DeleteFile(Path.Combine(launcherResources, SPLASH_ICON_FOLDER, $"{SPLASH_ICON}.xml"));
            DeleteFile(Path.Combine(launcherResources, SPLASH_FOREGROUND_FOLDER, $"{SPLASH_FOREGROUND}.png"));
            foreach (var density in _densities)
            {
                DeleteFile(Path.Combine(launcherResources, $"drawable-{density}dpi", $"{STATIC_SPLASH_IMAGE}.jpg"));
            }
        }

        private static void WriteSplashTheme(string unityLibrary, string launcherResources, Action<string> logWarning)
        {
            var styles = Path.Combine(unityLibrary, "src", "main", "res", STYLES_FOLDER, STYLES_FILE);
            var withIcon = ReplaceSplashIcon(File.Exists(styles) ? File.ReadAllText(styles) : null);
            if (withIcon == null)
            {
                logWarning($"{StartupMeasure.LOG_PREFIX} The splash icon {TRANSPARENT_ICON} is missing from {styles}: the splash screen of Android 12 and later is built without the icon of the launch screen.");
                return;
            }

            WriteFile(Path.Combine(launcherResources, STYLES_FOLDER, STYLES_FILE), withIcon);
        }

        private static void WriteSplashIcon(string launcherResources, LaunchScreenImage icon)
        {
            WriteFile(Path.Combine(launcherResources, SPLASH_ICON_FOLDER, $"{SPLASH_ICON}.xml"), GetSplashIconXml());
            var foreground = Path.Combine(launcherResources, SPLASH_FOREGROUND_FOLDER, $"{SPLASH_FOREGROUND}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(foreground));
            File.WriteAllBytes(foreground, icon.Pad(GetLayerSize(icon.Size)).EncodeToPng());
        }

        /// <summary>
        ///     One image per screen density, in a folder of that density, of as many pixels as 192 dp: Unity decodes the
        ///     image of the closest density without scaling it. Unity draws it over the splash background colour, so the
        ///     image is a JPEG of the icon over that colour, three times lighter than a PNG with transparency.
        /// </summary>
        private static void WriteStaticSplashImages(string launcherResources, LaunchScreenImage icon, Color background)
        {
            foreach (var density in _densities)
            {
                var folder = Path.Combine(launcherResources, $"drawable-{density}dpi");
                Directory.CreateDirectory(folder);
                foreach (var previous in Directory.GetFiles(folder, $"{STATIC_SPLASH_IMAGE}.*"))
                {
                    File.Delete(previous);
                }

                File.WriteAllBytes(Path.Combine(folder, $"{STATIC_SPLASH_IMAGE}.jpg"), icon.Resize(GetIconSize(density)).EncodeToJpg(background, JPEG_QUALITY));
            }
        }

        private static void DeclareSplashScreenExit(string manifestPath, Action<string> logWarning)
        {
            var manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null;
            var withProvider = AddSplashScreenExit(manifest);
            if (withProvider == null)
            {
                logWarning($"{StartupMeasure.LOG_PREFIX} No application element in {manifestPath}: the splash screen of Android 12 and later keeps its exit animation.");
                return;
            }

            if (withProvider != manifest)
            {
                File.WriteAllText(manifestPath, withProvider);
            }
        }

        private static void WriteFile(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}