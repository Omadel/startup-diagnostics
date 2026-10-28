using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The content of a scene of the start that <see cref="SceneDiagnosis" /> checks, gathered by
    ///     <see cref="SceneStateReader" />: the first scene of the build or a playable scene.
    /// </summary>
    internal sealed class SceneState
    {
        public string Path;

        /// <summary>
        ///     Whether it is the first scene of the build, rather than a playable scene loaded after it.
        /// </summary>
        public bool FirstScene;

        /// <summary>
        ///     The assets that the scene pulls in, directly or through other assets, each with its own size, largest first.
        /// </summary>
        public List<SizedAsset> Assets = new();

        /// <summary>
        ///     The size of all of them together.
        /// </summary>
        public long Size;

        /// <summary>
        ///     The date of the build layout of Addressables that gave the sizes of some assets, or null when every size is
        ///     estimated in the editor.
        /// </summary>
        public string LayoutDate;

        /// <summary>
        ///     The chain of references from the scene to each of the largest assets, by path.
        /// </summary>
        public Dictionary<string, string> Chains = new();

        /// <summary>
        ///     The models whose clips the scene pulls in through Animator controllers, imported without animation
        ///     compression.
        /// </summary>
        public List<ModelState> UncompressedModels = new();

        /// <summary>
        ///     The textures that the scene pulls in, with their size on Android and on iOS.
        /// </summary>
        public List<TextureState> Textures = new();

        /// <summary>
        ///     The components active when the scene loads whose type has an <c>OnGUI</c> method, as
        ///     <c>hierarchy path › type</c>.
        /// </summary>
        public List<string> OnGuiComponents = new();

        /// <summary>
        ///     The <c>UIDocument</c>s active when the scene loads, as <c>hierarchy path › type</c>.
        /// </summary>
        public List<string> UiDocuments = new();

        /// <summary>
        ///     The cameras of the scene that are active when it loads and render to the screen.
        /// </summary>
        public List<CameraState> Cameras = new();

        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
    }
}