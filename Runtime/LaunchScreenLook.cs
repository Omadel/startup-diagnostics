using System;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     What the launch screen of the first scene shows, from the settings of the project: written into the first scene
    ///     of the build, or given in Play Mode.
    /// </summary>
    [Serializable]
    internal sealed class LaunchScreenLook
    {
        /// <summary>
        ///     The image at the centre of the screen, drawn as it is, fitted in a square of <see cref="LaunchScreen.ICON_SIZE" />.
        /// </summary>
        public Texture2D Icon;

        /// <summary>
        ///     The colour of the whole screen, the splash background colour of the Player Settings.
        /// </summary>
        public Color Background;

        /// <summary>
        ///     Whether a ring turns under the icon, from the second frame.
        /// </summary>
        public bool LoadingRing;

        public Color LoadingRingColor;

        /// <summary>
        ///     The diameter of the ring, in points on iOS and dp on Android.
        /// </summary>
        public float LoadingRingSize;

        /// <summary>
        ///     The space between the icon and the ring, in points on iOS and dp on Android.
        /// </summary>
        public float LoadingRingGap;

        public Shader IconShader;
        public Shader LoadingRingShader;
    }
}