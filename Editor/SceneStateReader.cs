using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Reads into <see cref="SceneState" />s the content of the first scene of the build and of the playable scenes of
    ///     the settings. Each scene opens as a preview scene, out of the scenes open in the editor, and its assets load to be
    ///     measured.
    /// </summary>
    internal static class SceneStateReader
    {
        private const string TITLE = "Startup Diagnostics";
        private const string ANDROID = "Android";
        private const string IOS = "iPhone";

        private const string UI_DOCUMENT = "UnityEngine.UIElements.UIDocument";

        private static readonly string[] _controllerExtensions = { ".controller", ".overrideController" };

        // For one run of the diagnosis: the scripts by class, and whether the OnGUI of a type is in the builds.
        private static Dictionary<Type, MonoScript> _scripts;
        private static readonly Dictionary<Type, bool> _onGuiInBuilds = new();

        public static List<SceneState> Read(DiagnosisThresholds thresholds)
        {
            _scripts = null;
            _onGuiInBuilds.Clear();
            var buildScenes = EditorBuildSettings.scenes.Select(scene => (scene.path, scene.enabled));
            var playableScenes = StartupDiagnosticsSettings.instance.PlayableScenes.Where(scene => scene != null).Select(AssetDatabase.GetAssetPath);
            var paths = ScenePaths.Get(buildScenes, playableScenes, out var firstScene);
            var layoutSizes = AddressablesLayoutReader.ReadSizes(out var layoutDate);
            var sizes = new Dictionary<string, long>();
            var scenes = new List<SceneState>();
            var showsProgress = !BuildPipeline.isBuildingPlayer && !Application.isBatchMode;
            try
            {
                for (var i = 0; i < paths.Count; i++)
                {
                    if (showsProgress)
                    {
                        EditorUtility.DisplayProgressBar(TITLE, $"Reading {Path.GetFileName(paths[i])}", (float)i / paths.Count);
                    }

                    scenes.Add(Read(paths[i], paths[i] == firstScene, layoutSizes, layoutDate, sizes, thresholds));
                }
            }
            finally
            {
                if (showsProgress)
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            return scenes;
        }

        /// <summary>
        ///     The largest side in pixels of a texture once imported with a Max Size: never larger than its source.
        /// </summary>
        public static int GetImportedTextureSize(int sourceWidth, int sourceHeight, int maxSize)
        {
            return Math.Min(Math.Max(sourceWidth, sourceHeight), maxSize);
        }

        private static SceneState Read(string path, bool firstScene, Dictionary<string, long> layoutSizes, string layoutDate, Dictionary<string, long> sizes, DiagnosisThresholds thresholds)
        {
            var state = new SceneState { Path = path, FirstScene = firstScene };
            var dependencies = AssetDatabase.GetDependencies(path, true).Where(dependency => dependency != path && AssetPaths.CountsInBuild(dependency)).ToList();
            var fromLayout = false;
            foreach (var dependency in dependencies)
            {
                if (layoutSizes != null && layoutSizes.ContainsKey(dependency))
                {
                    fromLayout = true;
                }

                if (!sizes.TryGetValue(dependency, out var size))
                {
                    size = layoutSizes != null && layoutSizes.TryGetValue(dependency, out var layoutSize) ? layoutSize : AssetSizes.Estimate(dependency);
                    sizes.Add(dependency, size);
                }

                state.Assets.Add(new SizedAsset { Path = dependency, Size = size });
                state.Size += size;
            }

            state.Assets.Sort((a, b) => b.Size.CompareTo(a.Size));
            state.LayoutDate = fromLayout ? layoutDate : null;
            ReadScene(state, thresholds);
            ReadModels(state, dependencies);
            ReadTextures(state, dependencies);
            return state;
        }

        /// <summary>
        ///     Opens the scene as a preview scene for its active components and the chains of references of its largest
        ///     assets.
        /// </summary>
        private static void ReadScene(SceneState state, DiagnosisThresholds thresholds)
        {
            var scene = EditorSceneManager.OpenPreviewScene(state.Path);
            try
            {
                var roots = scene.GetRootGameObjects();
                ReadComponents(state, roots);
                var finder = new ReferenceFinder(state.Path, () => GetObjects(roots));
                var directDependencies = new Dictionary<string, List<string>>();
                foreach (var asset in state.Assets.Take(thresholds.ListedAssets))
                {
                    var chain = ReferenceChains.FindPath(state.Path, asset.Path, dependency => GetDirectDependencies(dependency, directDependencies));
                    if (chain != null)
                    {
                        state.Chains[asset.Path] = ReferenceChains.Format(chain, finder.Describe);
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ReadComponents(SceneState state, GameObject[] roots)
        {
            foreach (var root in roots)
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || !behaviour.enabled || !behaviour.gameObject.activeInHierarchy) continue;

                    var type = behaviour.GetType();
                    var path = $"{ReferenceFinder.GetHierarchyPath(behaviour.transform)} › {type.Name}";
                    if (type.FullName == UI_DOCUMENT)
                    {
                        state.UiDocuments.Add(path);
                    }

                    if (HasOnGuiInBuilds(type))
                    {
                        state.OnGuiComponents.Add(path);
                    }
                }

                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (!camera.enabled || !camera.gameObject.activeInHierarchy || camera.targetTexture != null) continue;

                    state.Cameras.Add(new CameraState { Name = ReferenceFinder.GetHierarchyPath(camera.transform), Depth = camera.depth, ClearFlags = camera.clearFlags, Background = camera.backgroundColor, CullingMask = camera.cullingMask });
                }
            }
        }

        /// <summary>
        ///     Whether a component type has an <c>OnGUI</c> method in the builds: declared, and not only in a block compiled in
        ///     the editor, as far as the source of the declaring type tells. A type without source counts as having one.
        /// </summary>
        private static bool HasOnGuiInBuilds(Type type)
        {
            var declaringType = OnGuiTypes.GetDeclaringType(type);
            if (declaringType == null) return false;

            if (!_onGuiInBuilds.TryGetValue(declaringType, out var inBuilds))
            {
                _scripts ??= MonoImporter.GetAllRuntimeMonoScripts().Where(script => script != null && script.GetClass() != null).GroupBy(script => script.GetClass()).ToDictionary(group => group.Key, group => group.First());
                inBuilds = !_scripts.TryGetValue(declaringType, out var script) || !OnGuiTypes.IsEditorOnly(script.text);
                _onGuiInBuilds.Add(declaringType, inBuilds);
            }

            return inBuilds;
        }

        private static IEnumerable<Object> GetObjects(GameObject[] roots)
        {
            foreach (var root in roots)
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    yield return transform.gameObject;

                    foreach (var component in transform.GetComponents<Component>())
                    {
                        yield return component;
                    }
                }
            }
        }

        private static List<string> GetDirectDependencies(string asset, Dictionary<string, List<string>> directDependencies)
        {
            if (!directDependencies.TryGetValue(asset, out var dependencies))
            {
                dependencies = AssetDatabase.GetDependencies(asset, false).Where(dependency => dependency != asset && AssetPaths.CountsInBuild(dependency)).ToList();
                directDependencies.Add(asset, dependencies);
            }

            return dependencies;
        }

        /// <summary>
        ///     The models imported without animation compression whose clips the Animator controllers of the scene use.
        /// </summary>
        private static void ReadModels(SceneState state, List<string> dependencies)
        {
            var clipsByModel = new Dictionary<string, HashSet<AnimationClip>>();
            foreach (var dependency in dependencies)
            {
                if (!_controllerExtensions.Any(extension => dependency.EndsWith(extension, StringComparison.OrdinalIgnoreCase))) continue;

                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(dependency);
                if (controller == null) continue;

                foreach (var clip in controller.animationClips)
                {
                    var model = clip == null ? null : AssetDatabase.GetAssetPath(clip);
                    if (string.IsNullOrEmpty(model) || AssetImporter.GetAtPath(model) is not ModelImporter importer || importer.animationCompression != ModelImporterAnimationCompression.Off) continue;

                    if (!clipsByModel.TryGetValue(model, out var clips))
                    {
                        clips = new HashSet<AnimationClip>();
                        clipsByModel.Add(model, clips);
                    }

                    clips.Add(clip);
                }
            }

            foreach (var (model, clips) in clipsByModel)
            {
                state.UncompressedModels.Add(new ModelState { Path = model, Clips = clips.Count, Size = clips.Sum(Profiler.GetRuntimeMemorySizeLong) });
            }

            state.UncompressedModels.Sort((a, b) => b.Size.CompareTo(a.Size));
        }

        private static void ReadTextures(SceneState state, List<string> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                if (AssetImporter.GetAtPath(dependency) is not TextureImporter importer) continue;

                importer.GetSourceTextureWidthAndHeight(out var width, out var height);
                state.Textures.Add(new TextureState { Path = dependency, AndroidSize = GetImportedTextureSize(width, height, GetMaxSize(importer, ANDROID)), IosSize = GetImportedTextureSize(width, height, GetMaxSize(importer, IOS)) });
            }
        }

        private static int GetMaxSize(TextureImporter importer, string platform)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            return settings.overridden ? settings.maxTextureSize : importer.maxTextureSize;
        }
    }
}