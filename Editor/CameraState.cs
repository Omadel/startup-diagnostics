using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A camera of a scene that renders to the screen.
    /// </summary>
    internal sealed class CameraState
    {
        /// <summary>
        ///     Its path in the hierarchy of the scene.
        /// </summary>
        public string Name;

        public float Depth;

        /// <summary>
        ///     How it clears the screen: to its background colour, to the skybox, or only the depth, as an overlay.
        /// </summary>
        public CameraClearFlags ClearFlags;

        public Color Background;

        /// <summary>
        ///     The layers it renders: 0 for none.
        /// </summary>
        public int CullingMask;
    }
}