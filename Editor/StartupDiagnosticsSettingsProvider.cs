using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Project Settings page of the package: the launch screen, off by default, its icon, the background colour it takes
    ///     from the Player Settings and its loading ring, with the issues of the icon, checked again whenever the icon or
    ///     its file changes; then the diagnosis, its playable scenes, its run before the builds and its thresholds.
    /// </summary>
    internal sealed class StartupDiagnosticsSettingsProvider : SettingsProvider
    {
        private const string PATH = "Project/Startup Diagnostics";
        private const string PLAYER_SETTINGS_PATH = "Project/Player";

        private static readonly GUIContent _launchScreenTitle = new("Launch Screen");
        private static readonly GUIContent _launchScreenLabel = new("Launch Screen", "Keeps the launch screen of the OS on screen from the first frame of the first scene to the end of the start: the icon at the centre of the screen over the background colour, and a loading ring under it. The launch screens of Android and iOS show the same icon at the same place.");
        private static readonly GUIContent _iconLabel = new("Icon", "Image drawn as it is at the centre of the launch screens, fitted in a square of 192 points on iOS and 192 dp on Android. A PNG or JPEG file, which the Android build reads. Android 12 and later crop it to the shape of the icons of the device: keep what shows inside the circle inscribed in it.");
        private static readonly GUIContent _backgroundLabel = new("Background", "The splash background colour of the Player Settings, under the icon on every launch screen.");
        private static readonly GUIContent _playerSettingsLabel = new("Player Settings", "Opens the Player Settings, where the splash background colour is set.");
        private static readonly GUIContent _loadingRingLabel = new("Loading Ring", "A ring that turns under the icon from the second frame of the first scene: the launch screens of the OS cannot show it.");
        private static readonly GUIContent _loadingRingColorLabel = new("Colour");
        private static readonly GUIContent _loadingRingSizeLabel = new("Size", "Diameter of the ring, in points on iOS and dp on Android.");
        private static readonly GUIContent _loadingRingGapLabel = new("Gap", "Space between the icon and the ring, in points on iOS and dp on Android.");
        private static readonly GUIContent _diagnosisTitle = new("Diagnosis");
        private static readonly GUIContent _playableScenesLabel = new("Playable Scenes", "The scenes of the first playable screen, loaded after the first scene of the build, such as through Addressables: the diagnosis checks their content with the content of the first scene. Empty, it checks the first scene of the build only.");
        private static readonly GUIContent _diagnosisOnBuildLabel = new("Check Before Builds", "Runs the diagnosis before each Android and iOS build: a warning in the console for each warning, a log for each info.");
        private static readonly GUIContent _thresholdsTitle = new("Thresholds");
        private static readonly GUIContent _preloadedAndResourcesLabel = new("Preloaded and Resources (MB)", "Size of the Preloaded Assets and of the content of the Resources folders together, dependencies included, above which the diagnosis warns.");
        private static readonly GUIContent _firstSceneLabel = new("First Scene (MB)", "Size of the content of the first scene of the build above which the diagnosis warns: a small boot scene loads at once.");
        private static readonly GUIContent _textureSizeLabel = new("Texture Size (px)", "Largest size of a texture on Android or iOS, set by its Max Size, above which the diagnosis warns.");
        private static readonly GUIContent _listedAssetsLabel = new("Listed Assets", "Number of assets listed, largest first, in each list of assets of the diagnosis.");
        private static readonly GUIContent _openDiagnosisLabel = new("Open the Diagnosis", "Opens Window › Analysis › Startup Diagnostics on its Diagnosis tab.");
        private static readonly int[] _textureSizes = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };
        private static readonly GUIContent[] _textureSizeNames = Array.ConvertAll(_textureSizes, size => new GUIContent(size.ToString()));

        private SerializedObject _settings;
        private GUIStyle _pageStyle;
        private string _iconState;
        private string _iconIssue;

        private StartupDiagnosticsSettingsProvider() : base(PATH, SettingsScope.Project, new[] { "startup", "launch screen", "splash", "icon", "loading", "diagnosis", "playable scenes", "thresholds" })
        {
        }

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new StartupDiagnosticsSettingsProvider();
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _settings = new SerializedObject(StartupDiagnosticsSettings.instance);
            _iconState = null;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        public override void OnDeactivate()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        public override void OnGUI(string searchContext)
        {
            _pageStyle ??= new GUIStyle { margin = new RectOffset(10, 10, 10, 10) };
            _settings.Update();
            using (new EditorGUILayout.VerticalScope(_pageStyle))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(_launchScreenTitle, EditorStyles.boldLabel);
                var launchScreen = _settings.FindProperty("_launchScreen");
                EditorGUILayout.PropertyField(launchScreen, _launchScreenLabel);
                if (launchScreen.boolValue)
                {
                    DrawLaunchScreen();
                }
                else
                {
                    EditorGUILayout.HelpBox("Off: the builds have the launch screens of Unity, and the first scene draws its first frame as it is.", MessageType.None);
                }

                EditorGUILayout.Space();
                DrawDiagnosis();
                if (EditorGUI.EndChangeCheck())
                {
                    _settings.ApplyModifiedProperties();
                    StartupDiagnosticsSettings.instance.Save();
                }
            }
        }

        private void DrawLaunchScreen()
        {
            EditorGUI.indentLevel++;
            var icon = _settings.FindProperty("_launchIcon");
            EditorGUILayout.PropertyField(icon, _iconLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ColorField(_backgroundLabel, PlayerSettings.SplashScreen.backgroundColor, false, false, false);
                }

                if (GUILayout.Button(_playerSettingsLabel, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    SettingsService.OpenProjectSettings(PLAYER_SETTINGS_PATH);
                }
            }

            var loadingRing = _settings.FindProperty("_loadingRing");
            EditorGUILayout.PropertyField(loadingRing, _loadingRingLabel);
            if (loadingRing.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_settings.FindProperty("_loadingRingColor"), _loadingRingColorLabel);
                DrawPositive(_settings.FindProperty("_loadingRingSize"), _loadingRingSizeLabel);
                DrawPositive(_settings.FindProperty("_loadingRingGap"), _loadingRingGapLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
            var iconIssue = GetIconIssue((Texture2D)icon.objectReferenceValue);
            if (iconIssue != null)
            {
                EditorGUILayout.HelpBox(iconIssue, MessageType.Warning);
            }

            EditorGUILayout.HelpBox("Before each iOS build, the launch screen of iOS in the Player Settings is set to this icon at 192 points over the background colour (Image and background, constant size). Android builds get their splash screen from the same icon. The first scene shows the launch screen until the end of the start: call StartupTimeline.DeferEnd in that scene, or the first frame ends the start and the launch screen never shows.", MessageType.Info);
        }

        private void DrawDiagnosis()
        {
            EditorGUILayout.LabelField(_diagnosisTitle, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_settings.FindProperty("_playableScenes"), _playableScenesLabel, true);
            EditorGUILayout.PropertyField(_settings.FindProperty("_diagnosisOnBuild"), _diagnosisOnBuildLabel);
            EditorGUILayout.LabelField(_thresholdsTitle);
            EditorGUI.indentLevel++;
            DrawPositive(_settings.FindProperty("_preloadedAndResourcesMegabytes"), _preloadedAndResourcesLabel);
            DrawPositive(_settings.FindProperty("_firstSceneMegabytes"), _firstSceneLabel);
            var textureSize = _settings.FindProperty("_textureSize");
            textureSize.intValue = EditorGUILayout.IntPopup(_textureSizeLabel, textureSize.intValue, _textureSizeNames, _textureSizes);
            var listedAssets = _settings.FindProperty("_listedAssets");
            EditorGUILayout.PropertyField(listedAssets, _listedAssetsLabel);
            if (listedAssets.intValue < 1)
            {
                listedAssets.intValue = 1;
            }

            EditorGUI.indentLevel--;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(_openDiagnosisLabel, GUILayout.ExpandWidth(false)))
                {
                    StartupMeasureWindow.OpenDiagnosis();
                }
            }
        }

        private static void DrawPositive(SerializedProperty property, GUIContent label)
        {
            EditorGUILayout.PropertyField(property, label);
            if (property.floatValue < 0)
            {
                property.floatValue = 0;
            }
        }

        /// <summary>
        ///     The issue of the icon, or null, checked again only when the icon or the date of its file changed.
        /// </summary>
        private string GetIconIssue(Texture2D icon)
        {
            var path = icon == null ? null : AssetDatabase.GetAssetPath(icon);
            var date = !string.IsNullOrEmpty(path) && File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
            var state = $"{path}|{date}";
            if (state == _iconState) return _iconIssue;

            _iconState = state;
            _iconIssue = CheckIcon(icon, path);
            return _iconIssue;
        }

        private static string CheckIcon(Texture2D icon, string path)
        {
            if (icon == null) return "No icon: the builds have the launch screens of Unity, and Play Mode has no launch screen.";

            var image = string.IsNullOrEmpty(path) || !File.Exists(path) ? null : LaunchScreenImage.Decode(File.ReadAllBytes(path));
            if (image == null) return "The icon is not a PNG or JPEG file: the Android builds keep the launch screens of Unity.";

            return image.HasVisiblePixelsOutsideDisc() ? "The icon shows outside the circle inscribed in it: the splash screen of Android 12 and later crops it to the shape of the icons of the device, and the next launch screens do not." : null;
        }

        private void OnUndoRedo()
        {
            StartupDiagnosticsSettings.instance.Save();
            _settings.Update();
            Repaint();
        }
    }
}