using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Keeps the launch screen of the OS on screen while the start goes on after the first frame of the first scene
    ///     (<see cref="StartupTimeline.DeferEnd" />): the icon at the centre of the screen over the background colour,
    ///     <see cref="ICON_SIZE" /> points wide on iOS and dp on Android, where the launch screens of both systems set up by
    ///     the package draw it, and a ring under it that appears from the second frame and turns.
    ///     <list type="bullet">
    ///         <item>
    ///             Drawn by a camera of its own over all others, which renders only a layer that the project does not name.
    ///         </item>
    ///         <item>
    ///             Destroyed as the end of the start is requested (<see cref="StartupTimeline.IsEndRequested" />), so that
    ///             the frame that ends the start shows the game. Without a deferred end, the first frame ends the start and
    ///             the launch screen never shows.
    ///         </item>
    ///         <item>
    ///             Added to the first scene of the build by the editor, and created before the first scene loads in Play
    ///             Mode.
    ///         </item>
    ///     </list>
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class LaunchScreen : MonoBehaviour
    {
        public const string NAME = "Startup Diagnostics Launch Screen";
        public const float ICON_SIZE = 192;
        public const float LOADING_RING_FADE_DURATION = 0.3f;

        private const float REFERENCE_DPI = 160;
        private const float CAMERA_DEPTH = 1000;
        private const float DRAWING_DISTANCE = 1;
        private const int FIRST_PROJECT_LAYER = 8;
        private const int LAST_LAYER = 31;

#if UNITY_EDITOR
        /// <summary>
        ///     What the launch screen shows in Play Mode, or null when it is off: set by the editor assembly, which reads the
        ///     settings of the project.
        /// </summary>
        internal static Func<LaunchScreenLook> EditorLook;
#endif

        [SerializeField] private LaunchScreenLook _look;

        private Camera _camera;
        private Mesh _quad;
        private Material _iconMaterial;
        private Material _loadingRingMaterial;
        private int _layer;
        private float _pixelsPerPoint;
        private float _firstFrameTime = float.NaN;

        internal LaunchScreenLook Look
        {
            get => _look;
            set => _look = value;
        }

        /// <summary>
        ///     Whether the launch screen stays on screen: the start goes on after the first frame, and its end is not near.
        /// </summary>
        internal static bool IsShown(bool isEndDeferred, bool isEndRequested)
        {
            return isEndDeferred && !isEndRequested;
        }

        /// <summary>
        ///     Width and height of an image of the given size in pixels, fitted in a square of the given side.
        /// </summary>
        internal static Vector2 FitInSquare(int width, int height, float side)
        {
            if (width <= 0 || height <= 0 || width == height) return new Vector2(side, side);

            return width > height ? new Vector2(side, side * height / width) : new Vector2(side * width / height, side);
        }

        /// <summary>
        ///     Distance from the centre of the icon down to the centre of the loading ring, in the unit of the sizes.
        /// </summary>
        internal static float GetLoadingRingOffset(float iconSize, float ringSize, float gap)
        {
            return iconSize / 2 + gap + ringSize / 2;
        }

        /// <summary>
        ///     Opacity of the loading ring, from the first frame of the launch screen: hidden on that frame, like on the launch
        ///     screens of the OS, then fading in over the duration.
        /// </summary>
        internal static float GetLoadingRingOpacity(float elapsed, float duration)
        {
            if (elapsed <= 0) return 0;

            return duration > 0 ? Mathf.Clamp01(elapsed / duration) : 1;
        }

        /// <summary>
        ///     The last layer that the project does not name, which nothing renders: the camera of the launch screen renders
        ///     only that one. The last layer if the project names them all.
        /// </summary>
        internal static int FindFreeLayer(Func<int, string> getLayerName)
        {
            for (var layer = LAST_LAYER; layer >= FIRST_PROJECT_LAYER; layer--)
            {
                if (string.IsNullOrEmpty(getLayerName(layer))) return layer;
            }

            return LAST_LAYER;
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ShowInPlayMode()
        {
            var look = EditorLook?.Invoke();
            if (look == null) return;

            var gameObject = new GameObject(NAME);
            gameObject.SetActive(false);
            gameObject.AddComponent<LaunchScreen>()._look = look;
            gameObject.SetActive(true);
        }
#endif

        private void Awake()
        {
            if (_look == null || _look.Icon == null || _look.IconShader == null || StartupTimeline.IsEndRequested)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            _layer = FindFreeLayer(LayerMask.LayerToName);
            _pixelsPerPoint = GetPixelsPerPoint();
            _camera = gameObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _look.Background;
            _camera.cullingMask = 1 << _layer;
            _camera.orthographic = true;
            _camera.depth = CAMERA_DEPTH;
            _camera.nearClipPlane = DRAWING_DISTANCE / 2;
            _camera.farClipPlane = DRAWING_DISTANCE * 2;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;
            _quad = CreateQuad();
            _iconMaterial = new Material(_look.IconShader) { mainTexture = _look.Icon };
            if (_look.LoadingRing && _look.LoadingRingShader != null)
            {
                _loadingRingMaterial = new Material(_look.LoadingRingShader);
            }

            StartupTimeline.EndRequested += Hide;
        }

        /// <summary>
        ///     Draws the launch screen for the frame, or hides it once the start no longer needs it: the end of the start is
        ///     near, or the first frame ends it.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsShown(StartupTimeline.IsEndDeferred, StartupTimeline.IsEndRequested))
            {
                Hide();
                return;
            }

            // One unit per pixel, whatever the orientation.
            _camera.orthographicSize = _camera.pixelHeight / 2f;
            Draw(_iconMaterial, 0, FitInSquare(_look.Icon.width, _look.Icon.height, ICON_SIZE * _pixelsPerPoint));
            if (_loadingRingMaterial == null) return;

            if (float.IsNaN(_firstFrameTime))
            {
                _firstFrameTime = Time.unscaledTime;
            }

            var opacity = GetLoadingRingOpacity(Time.unscaledTime - _firstFrameTime, LOADING_RING_FADE_DURATION);
            if (opacity <= 0) return;

            var color = _look.LoadingRingColor;
            color.a *= opacity;
            _loadingRingMaterial.color = color;
            var ringSize = _look.LoadingRingSize * _pixelsPerPoint;
            Draw(_loadingRingMaterial, -GetLoadingRingOffset(ICON_SIZE, _look.LoadingRingSize, _look.LoadingRingGap) * _pixelsPerPoint, new Vector2(ringSize, ringSize));
        }

        private void OnDestroy()
        {
            StartupTimeline.EndRequested -= Hide;
            DestroyCreated(_quad);
            DestroyCreated(_iconMaterial);
            DestroyCreated(_loadingRingMaterial);
        }

        /// <summary>
        ///     Removes the launch screen at once: before the current frame is rendered, if it is not yet.
        /// </summary>
        private void Hide()
        {
            StartupTimeline.EndRequested -= Hide;
            if (_camera != null)
            {
                _camera.enabled = false;
            }

            enabled = false;
            Destroy(gameObject);
        }

        /// <summary>
        ///     Draws a quad of the given size, centred at the given height, with the camera of the launch screen only.
        /// </summary>
        private void Draw(Material material, float height, Vector2 size)
        {
            var parameters = new RenderParams(material) { camera = _camera, layer = _layer };
            Graphics.RenderMesh(parameters, _quad, 0, Matrix4x4.TRS(new Vector3(0, height, DRAWING_DISTANCE), Quaternion.identity, new Vector3(size.x, size.y, 1)));
        }

        private static Mesh CreateQuad()
        {
            return new Mesh
            {
                name = NAME,
                vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
        }

        private static void DestroyCreated(UnityEngine.Object created)
        {
            if (created != null)
            {
                Destroy(created);
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern float StartupDiagnostics_GetPixelsPerPoint();

        /// <summary>
        ///     Pixels of the rendered image per point of the screen, the unit of the launch storyboard.
        /// </summary>
        private static float GetPixelsPerPoint()
        {
            return StartupDiagnostics_GetPixelsPerPoint();
        }
#elif UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        ///     Pixels per dp of the display, the unit of the splash screen of the system.
        /// </summary>
        private static float GetPixelsPerPoint()
        {
            try
            {
                using var resources = new AndroidJavaClass("android.content.res.Resources");
                using var system = resources.CallStatic<AndroidJavaObject>("getSystem");
                using var metrics = system.Call<AndroidJavaObject>("getDisplayMetrics");
                return metrics.Get<float>("density");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{StartupTimeline.LOG_PREFIX} Unable to read the density of the display, the launch screen is drawn from the dpi of the screen: {exception.Message}");
                return GetPixelsPerPointFromDpi();
            }
        }
#else
        private static float GetPixelsPerPoint()
        {
            return GetPixelsPerPointFromDpi();
        }
#endif

        private static float GetPixelsPerPointFromDpi()
        {
            return Screen.dpi > 0 ? Screen.dpi / REFERENCE_DPI : 1;
        }
    }
}