using UnityEngine;

namespace Javienne.StartupDiagnostics.Tests
{
    /// <summary>
    ///     The properties of the launch screen of iOS in the Player Settings, under their serialized names, for the tests
    ///     of <see cref="IosLaunchScreen" />: they must not change the settings of the project.
    /// </summary>
    public class FakeIosPlayerSettings : ScriptableObject
    {
        // The serialized names of the Player Settings.
        // ReSharper disable InconsistentNaming
        [SerializeField] private int iOSLaunchScreenType;
        [SerializeField] private int iOSLaunchScreeniPadType;
        [SerializeField] private Texture2D iOSLaunchScreenPortrait;
        [SerializeField] private Texture2D iOSLaunchScreenLandscape;
        [SerializeField] private Texture2D iOSLaunchScreeniPadImage;
        [SerializeField] private Color iOSLaunchScreenBackgroundColor;
        [SerializeField] private Color iOSLaunchScreeniPadBackgroundColor;
        [SerializeField] private float iOSLaunchScreenSize;
        [SerializeField] private float iOSLaunchScreeniPadSize;
        // ReSharper restore InconsistentNaming

        public Texture2D PortraitImage => iOSLaunchScreenPortrait;
        public Color IpadBackground => iOSLaunchScreeniPadBackgroundColor;
        public float IpadSize => iOSLaunchScreeniPadSize;
        public int IphoneType => iOSLaunchScreenType;
    }
}