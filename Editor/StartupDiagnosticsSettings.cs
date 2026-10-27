using System.Collections.Generic;
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
        [SerializeField] private List<SceneAsset> _playableScenes = new();
        [SerializeField] private bool _diagnosisOnBuild;
        [SerializeField] private float _preloadedAndResourcesMegabytes = DiagnosisThresholds.DEFAULT_MEGABYTES;
        [SerializeField] private float _firstSceneMegabytes = DiagnosisThresholds.DEFAULT_MEGABYTES;
        [SerializeField] private int _textureSize = DiagnosisThresholds.DEFAULT_TEXTURE_SIZE;
        [SerializeField] private int _listedAssets = DiagnosisThresholds.DEFAULT_LISTED_ASSETS;

        /// <summary>
        ///     Whether the builds and Play Mode have the launch screen of the package. Off by default.
        /// </summary>
        public bool LaunchScreen => _launchScreen;

        /// <summary>
        ///     The scenes of the first playable screen, loaded after the first scene of the build, such as through
        ///     Addressables: the diagnosis checks their content with the first scene. Empty, the first scene only.
        /// </summary>
        public IReadOnlyList<SceneAsset> PlayableScenes => _playableScenes;

        /// <summary>
        ///     Whether the diagnosis runs before each Android and iOS build and logs what it finds. Off by default.
        /// </summary>
        public bool DiagnosisOnBuild => _diagnosisOnBuild;

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
        ///     The thresholds of the checks of the diagnosis.
        /// </summary>
        internal DiagnosisThresholds GetDiagnosisThresholds()
        {
            return DiagnosisThresholds.Create(_preloadedAndResourcesMegabytes, _firstSceneMegabytes, _textureSize, _listedAssets);
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