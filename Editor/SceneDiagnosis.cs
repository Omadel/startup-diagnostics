using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The checks of the diagnosis on the content of the scenes of the start, from their <see cref="SceneState" />s: what
    ///     each scene pulls in, the animations without compression, the large textures, the active <c>OnGUI</c> methods and
    ///     the first frame of the first scene.
    /// </summary>
    internal static class SceneDiagnosis
    {
        public const string SCENE_CONTENT = "scene-content";
        public const string ANIMATION_COMPRESSION = "animation-compression";
        public const string TEXTURE_SIZE = "texture-size";
        public const string ONGUI = "ongui";
        public const string UI_DOCUMENTS = "ui-documents";
        public const string FIRST_FRAME = "first-frame";

        /// <summary>
        ///     The results of the checks: the content of each scene, then the checks across the scenes, then the first frame
        ///     when the build has a first scene. <paramref name="packageLaunchScreen" /> tells whether the launch screen of the
        ///     package is on, with an icon.
        /// </summary>
        public static List<DiagnosisResult> Check(IReadOnlyList<SceneState> scenes, bool packageLaunchScreen, Color splashBackground, DiagnosisThresholds thresholds)
        {
            var results = scenes.Select(scene => CheckContent(scene, thresholds)).ToList();
            if (scenes.Count == 0) return results;

            results.Add(CheckAnimationCompression(scenes, thresholds));
            results.Add(CheckTextureSize(scenes, thresholds));
            results.Add(CheckOnGui(scenes));
            results.Add(CheckUiDocuments(scenes));
            var firstScene = scenes.FirstOrDefault(scene => scene.FirstScene);
            if (firstScene != null)
            {
                results.Add(CheckFirstFrame(firstScene, packageLaunchScreen, splashBackground));
            }

            return results;
        }

        public static DiagnosisResult CheckContent(SceneState scene, DiagnosisThresholds thresholds)
        {
            var result = scene.FirstScene ? Create(SCENE_CONTENT, $"Content of {scene.Name}, first scene of the build", StartupStep.FirstScene, DiagnosisGuide.SMALL_BOOT_SCENE) : Create(SCENE_CONTENT, $"Content of {scene.Name}, playable scene", StartupStep.ProjectMilestones, DiagnosisGuide.SCENE_CONTENT);
            var listed = Math.Min(scene.Assets.Count, Math.Max(1, thresholds.ListedAssets));
            for (var i = 0; i < listed; i++)
            {
                var asset = scene.Assets[i];
                var detail = EditorUtility.FormatBytes(asset.Size);
                if (scene.Chains.TryGetValue(asset.Path, out var chain) && !string.IsNullOrEmpty(chain))
                {
                    detail += $", through {chain}";
                }

                result.Items.Add(new DiagnosisItem { Label = asset.Path, Detail = detail, AssetPath = asset.Path });
            }

            if (scene.Assets.Count == 0)
            {
                result.Message = scene.FirstScene ? "The first scene of the build references no asset: it loads at once." : "The playable scene references no asset.";
                return result;
            }

            var sizes = scene.LayoutDate == null ? "estimated in the editor" : $"from the build layout of Addressables of {scene.LayoutDate} for the assets of its bundles, estimated in the editor for the others";
            var size = $"{EditorUtility.FormatBytes(scene.Size)} for {Count(scene.Assets.Count, "asset")}, {sizes}";
            var more = scene.Assets.Count > listed ? $" The {listed} largest are listed, each with the chain of references that pulls it in." : "";
            if (!scene.FirstScene)
            {
                result.Level = scene.Assets.Count == 0 ? DiagnosisLevel.Passed : DiagnosisLevel.Info;
                result.Message = $"The playable scene loads with everything it references, decompressed in memory: {size}.{more} Look for what the start does not need, such as variants to load on demand.";
                return result;
            }

            var above = scene.Size > thresholds.FirstSceneSize;
            result.Level = above ? DiagnosisLevel.Warning : scene.Assets.Count == 0 ? DiagnosisLevel.Passed : DiagnosisLevel.Info;
            var threshold = above ? $", above the threshold of {EditorUtility.FormatBytes(thresholds.FirstSceneSize)}" : "";
            var advice = above ? " Make it a small boot scene that loads the playable scene asynchronously while the launch screen shows." : "";
            result.Message = $"The first scene of the build loads before anything of the game shows, with everything it references: {size}{threshold}.{more}{advice}";
            return result;
        }

        public static DiagnosisResult CheckAnimationCompression(IReadOnlyList<SceneState> scenes, DiagnosisThresholds thresholds)
        {
            var result = Create(ANIMATION_COMPRESSION, "Compression of animations", StartupStep.FirstScene, DiagnosisGuide.ANIMATION_COMPRESSION);
            var flagged = new List<SceneState>();
            var listed = new HashSet<string>();
            foreach (var scene in scenes)
            {
                foreach (var model in scene.UncompressedModels)
                {
                    if (!listed.Add(model.Path)) continue;

                    result.Items.Add(new DiagnosisItem { Label = model.Path, Detail = $"{scene.Name}: {Count(model.Clips, "clip")}, {EditorUtility.FormatBytes(model.Size)} estimated in the editor", AssetPath = model.Path });
                    Flag(flagged, scene);
                }
            }

            if (result.Items.Count == 0)
            {
                result.Message = "The animation clips that the Animator controllers of the scenes use are compressed.";
                return result;
            }

            Limit(result, thresholds);
            result.Level = DiagnosisLevel.Warning;
            result.Step = GetLoadingStep(flagged);
            result.Message = "These models are imported without animation compression, and the scenes pull their clips in through Animator controllers: in Rouflac, 58 clips of a dog took 28.1 MB of the 53 MB of the playable scene. Set Anim. Compression to Optimal in their import settings, tab Animation, then check the animations in the game.";
            return result;
        }

        public static DiagnosisResult CheckTextureSize(IReadOnlyList<SceneState> scenes, DiagnosisThresholds thresholds)
        {
            var result = Create(TEXTURE_SIZE, "Size of textures", StartupStep.FirstScene, DiagnosisGuide.TEXTURE_SIZE);
            var flagged = new List<SceneState>();
            var listed = new HashSet<string>();
            foreach (var scene in scenes)
            {
                foreach (var texture in scene.Textures)
                {
                    if ((texture.AndroidSize <= thresholds.TextureSize && texture.IosSize <= thresholds.TextureSize) || !listed.Add(texture.Path)) continue;

                    result.Items.Add(new DiagnosisItem { Label = texture.Path, Detail = $"{scene.Name}: {texture.AndroidSize} px on Android, {texture.IosSize} px on iOS", AssetPath = texture.Path });
                    Flag(flagged, scene);
                }
            }

            if (result.Items.Count == 0)
            {
                result.Message = $"No texture that the scenes pull in is larger than {thresholds.TextureSize} px on Android or iOS.";
                return result;
            }

            Limit(result, thresholds);
            result.Level = DiagnosisLevel.Warning;
            result.Step = GetLoadingStep(flagged);
            result.Message = $"These textures that the scenes pull in are larger than {thresholds.TextureSize} px on Android or iOS: in Rouflac, a texture of 2048 px took 2.5 MB in ASTC. Override their Max Size for Android and iOS in their import settings, and check them on the devices.";
            return result;
        }

        public static DiagnosisResult CheckOnGui(IReadOnlyList<SceneState> scenes)
        {
            var result = Create(ONGUI, "OnGUI in the scenes of the start", StartupStep.FirstFrame, DiagnosisGuide.ONGUI);
            var firstScene = false;
            foreach (var scene in scenes)
            {
                foreach (var component in scene.OnGuiComponents)
                {
                    result.Items.Add(new DiagnosisItem { Label = component, Detail = scene.Name });
                    firstScene |= scene.FirstScene;
                }
            }

            if (result.Items.Count == 0)
            {
                result.Message = "No component active when its scene loads has an OnGUI method.";
                return result;
            }

            result.Level = DiagnosisLevel.Warning;
            result.Step = firstScene ? StartupStep.FirstFrame : StartupStep.ProjectMilestones;
            result.Message = "These components are active when their scene loads and have an OnGUI method: the first frame of the scene then loads the skin of IMGUI, even if they draw nothing, about 0.5 s on a Pixel 6. Put each OnGUI on a component of its own, disabled until it has something to draw.";
            return result;
        }

        public static DiagnosisResult CheckUiDocuments(IReadOnlyList<SceneState> scenes)
        {
            var result = Create(UI_DOCUMENTS, "UI Toolkit in the scenes of the start", StartupStep.FirstScene, DiagnosisGuide.UI_TOOLKIT);
            var flagged = new List<SceneState>();
            foreach (var scene in scenes)
            {
                foreach (var document in scene.UiDocuments)
                {
                    result.Items.Add(new DiagnosisItem { Label = document, Detail = scene.Name });
                    Flag(flagged, scene);
                }
            }

            if (result.Items.Count == 0)
            {
                result.Message = "No UIDocument is active when the scenes of the start load.";
                return result;
            }

            result.Level = DiagnosisLevel.Info;
            result.Step = GetLoadingStep(flagged);
            result.Message = "These UIDocuments are active when their scene loads: UI Toolkit sets up its runtime panel with the first one. Their cost on the start was not measured; in Rouflac, a Profiler capture of a Pixel 6 shows 76 ms for the initialization of UI Toolkit without any UIDocument, for a cause not found. If the start does not need them, enable them after the first playable screen.";
            return result;
        }

        public static DiagnosisResult CheckFirstFrame(SceneState firstScene, bool packageLaunchScreen, Color splashBackground)
        {
            var result = Create(FIRST_FRAME, "First frame of the first scene", StartupStep.PerceivedWait, DiagnosisGuide.FIRST_FRAME);
            if (packageLaunchScreen)
            {
                result.Message = "The launch screen of the package draws the first frame of the first scene like the launch screen of the OS.";
                return result;
            }

            var camera = firstScene.Cameras.Where(other => other.ClearFlags == CameraClearFlags.SolidColor || other.ClearFlags == CameraClearFlags.Skybox).OrderByDescending(other => other.Depth).FirstOrDefault();
            if (camera == null)
            {
                result.Items.Add(new DiagnosisItem { Label = firstScene.Name, Detail = "No active camera clears the screen" });
            }
            else
            {
                if (camera.ClearFlags == CameraClearFlags.Skybox)
                {
                    result.Items.Add(new DiagnosisItem { Label = camera.Name, Detail = "Clears to the skybox, not to the splash background colour" });
                }
                else if (!IosLaunchScreen.IsSameColor(camera.Background, splashBackground))
                {
                    result.Items.Add(new DiagnosisItem { Label = camera.Name, Detail = $"Background #{ColorUtility.ToHtmlStringRGB(camera.Background)}, the splash background colour is #{ColorUtility.ToHtmlStringRGB(splashBackground)}" });
                }

                if (camera.CullingMask == 0)
                {
                    result.Items.Add(new DiagnosisItem { Label = camera.Name, Detail = "Renders no layer: only its background shows, without the icon of the launch screen" });
                }
            }

            if (result.Items.Count == 0)
            {
                result.Message = "The camera of the first scene clears to the splash background colour of the Player Settings.";
                return result;
            }

            result.Level = DiagnosisLevel.Warning;
            result.Message = "The first frame of the first scene follows the launch screen of the OS, and differs from it: the screen jumps. Clear the camera of the first scene to the splash background colour of the Player Settings and draw the icon of the launch screen at the same size, or turn on the launch screen of the package.";
            return result;
        }

        private static DiagnosisResult Create(string check, string title, StartupStep step, string guideSection)
        {
            return new DiagnosisResult { Check = check, Title = title, Level = DiagnosisLevel.Passed, Step = step, GuideSection = guideSection };
        }

        private static void Flag(List<SceneState> flagged, SceneState scene)
        {
            if (!flagged.Contains(scene))
            {
                flagged.Add(scene);
            }
        }

        /// <summary>
        ///     The step that loads the flagged scenes: the first scene when it is one of them, the milestones of the project
        ///     otherwise.
        /// </summary>
        private static StartupStep GetLoadingStep(IEnumerable<SceneState> flagged)
        {
            return flagged.Any(scene => scene.FirstScene) ? StartupStep.FirstScene : StartupStep.ProjectMilestones;
        }

        /// <summary>
        ///     Keeps the number of items of the settings, and says how many more there are.
        /// </summary>
        private static void Limit(DiagnosisResult result, DiagnosisThresholds thresholds)
        {
            var listed = Math.Max(1, thresholds.ListedAssets);
            if (result.Items.Count <= listed) return;

            var hidden = result.Items.Count - listed;
            result.Items.RemoveRange(listed, hidden);
            result.Items.Add(new DiagnosisItem { Label = $"And {hidden} more" });
        }

        private static string Count(int count, string noun)
        {
            return count == 1 ? $"1 {noun}" : $"{count} {noun}s";
        }
    }
}