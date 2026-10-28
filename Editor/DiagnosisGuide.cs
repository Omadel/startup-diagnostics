using System.Text;
using UnityEditor.PackageManager;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The sections of the optimization guide that the checks of the diagnosis link to, and their addresses on GitHub.
    ///     The anchors are those that GitHub gives the headings of the guide: see <see cref="GetAnchor" />.
    /// </summary>
    internal static class DiagnosisGuide
    {
        public const string REPOSITORY = "https://github.com/Omadel/startup-diagnostics";
        public const string GUIDE_PATH = "Documentation~/optimization-guide.md";

        public const string UNITY_SPLASH_SCREEN = "turn-off-the-splash-screen-of-unity";
        public const string PRELOADED_ASSETS_AND_RESOURCES = "preloaded-assets-and-resources";
        public const string UNITY_SERVICES = "unity-services-at-launch";
        public const string INPUT_DEVICES = "create-only-the-input-devices-the-game-uses";
        public const string LAUNCH_SCREEN = "the-launch-screen-of-the-package";
        public const string CATALOG_UPDATE = "do-not-wait-for-the-network";
        public const string JSON_CATALOG = "keep-the-catalog-binary";
        public const string BUNDLE_COMPRESSION = "compress-bundles-for-where-they-are";
        public const string REMOTE_CATALOG_CONTENT = "release-the-content-with-the-build";
        public const string SMALL_BOOT_SCENE = "a-small-boot-scene";
        public const string SCENE_CONTENT = "find-what-the-scene-drags-in";
        public const string ANIMATION_COMPRESSION = "compress-animations";
        public const string TEXTURE_SIZE = "limit-the-size-of-textures-per-platform";
        public const string ONGUI = "disable-ongui-until-it-draws";
        public const string FIRST_FRAME = "a-first-frame-like-the-launch-screen";
        public const string UI_TOOLKIT = "ui-toolkit-in-the-first-scenes";

        /// <summary>
        ///     Every anchor that a check links to.
        /// </summary>
        public static readonly string[] Anchors = { UNITY_SPLASH_SCREEN, PRELOADED_ASSETS_AND_RESOURCES, UNITY_SERVICES, INPUT_DEVICES, LAUNCH_SCREEN, CATALOG_UPDATE, JSON_CATALOG, BUNDLE_COMPRESSION, REMOTE_CATALOG_CONTENT, SMALL_BOOT_SCENE, SCENE_CONTENT, ANIMATION_COMPRESSION, TEXTURE_SIZE, ONGUI, FIRST_FRAME, UI_TOOLKIT };

        /// <summary>
        ///     The address of a section of the guide in the installed version of the package.
        /// </summary>
        public static string GetUrl(string anchor)
        {
            return GetUrl(PackageInfo.FindForAssembly(typeof(DiagnosisGuide).Assembly)?.version, anchor);
        }

        /// <summary>
        ///     The address of a section of the guide at the tag of a version, or on the main branch without a version.
        /// </summary>
        public static string GetUrl(string version, string anchor)
        {
            var reference = string.IsNullOrEmpty(version) ? "main" : $"v{version}";
            return $"{REPOSITORY}/blob/{reference}/{GUIDE_PATH}#{anchor}";
        }

        /// <summary>
        ///     The anchor that GitHub gives a heading of a Markdown file: in lower case, without punctuation, with hyphens
        ///     for spaces.
        /// </summary>
        public static string GetAnchor(string heading)
        {
            var anchor = new StringBuilder();
            foreach (var character in heading.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
                {
                    anchor.Append(character);
                }
                else if (character == ' ')
                {
                    anchor.Append('-');
                }
            }

            return anchor.ToString();
        }
    }
}