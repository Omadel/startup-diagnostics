using UnityEditor;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Project settings of the package, saved in the ProjectSettings folder: versioned with the project, without an
    ///     asset in Assets. Edited in Project Settings › Startup Diagnostics.
    /// </summary>
    [FilePath("ProjectSettings/StartupDiagnosticsSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class StartupDiagnosticsSettings : ScriptableSingleton<StartupDiagnosticsSettings>
    {
        public const string ICON_SHADER = "Hidden/Startup Diagnostics/Launch Icon";
        public const string LOADING_RING_SHADER = "Hidden/Startup Diagnostics/Loading Ring";
        public const float DEFAULT_LOADING_RING_SIZE = 28;
        public const float DEFAULT_LOADING_RING_GAP = 40;

        [SerializeField] private bool _launchScreen;
        [SerializeField] private Texture2D _launchIcon;
        [SerializeField] private bool _loadingRing = true;
        [SerializeField] private Color _loadingRingColor = new(1, 1, 1, 0.85f);
        [SerializeField] private float _loadingRingSize = DEFAULT_LOADING_RING_SIZE;
        [SerializeField] private float _loadingRingGap = DEFAULT_LOADING_RING_GAP;

        /// <summary>
        ///     Whether the builds and Play Mode have the launch screen of the package. Off by default.
        /// </summary>
        public bool LaunchScreen => _launchScreen;

        /// <summary>
        ///     The image at the centre of the launch screens, drawn as it is.
        /// </summary>
        public Texture2D LaunchIcon => _launchIcon;

        /// <summary>
        ///     What the launch screen of the first scene shows, or null when it is off or has no icon. Its background is the
        ///     splash background colour of the Player Settings, as on the launch screens of the OS.
        /// </summary>
        internal LaunchScreenLook GetLaunchScreenLook()
        {
            if (!_launchScreen || _launchIcon == null) return null;

            return new LaunchScreenLook
            {
                Icon = _launchIcon,
                Background = PlayerSettings.SplashScreen.backgroundColor,
                LoadingRing = _loadingRing,
                LoadingRingColor = _loadingRingColor,
                LoadingRingSize = _loadingRingSize,
                LoadingRingGap = _loadingRingGap,
                IconShader = Shader.Find(ICON_SHADER),
                LoadingRingShader = Shader.Find(LOADING_RING_SHADER)
            };
        }

        /// <summary>
        ///     Lets the settings page edit the singleton, which Unity marks as not editable.
        /// </summary>
        private void OnEnable()
        {
            hideFlags &= ~HideFlags.NotEditable;
        }

        /// <summary>
        ///     Writes the settings to their file in the ProjectSettings folder, as text.
        /// </summary>
        internal void Save()
        {
            Save(true);
        }
    }
}