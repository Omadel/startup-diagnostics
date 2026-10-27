using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Sets the launch screen of iOS in the Player Settings to the icon of the launch screen, at 192 points at the centre
    ///     of the splash background colour, on iPhone and iPad: the storyboard <i>Image and background (constant size)</i>
    ///     of Unity, where <see cref="LaunchScreen" /> draws the icon too.
    /// </summary>
    internal static class IosLaunchScreen
    {
        public const string IPHONE_TYPE = "iOSLaunchScreenType";
        public const string IPAD_TYPE = "iOSLaunchScreeniPadType";
        public const string PORTRAIT_IMAGE = "iOSLaunchScreenPortrait";
        public const string LANDSCAPE_IMAGE = "iOSLaunchScreenLandscape";
        public const string IPAD_IMAGE = "iOSLaunchScreeniPadImage";
        public const string IPHONE_BACKGROUND = "iOSLaunchScreenBackgroundColor";
        public const string IPAD_BACKGROUND = "iOSLaunchScreeniPadBackgroundColor";
        public const string IPHONE_SIZE = "iOSLaunchScreenSize";
        public const string IPAD_SIZE = "iOSLaunchScreeniPadSize";

        /// <summary>
        ///     Sets the launch screen of iOS in the Player Settings of the project, and logs what changed.
        /// </summary>
        internal static void ApplyToPlayerSettings(Texture2D icon, Color background)
        {
            var playerSettings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton(nameof(PlayerSettings)));
            var changed = Apply(playerSettings, icon, background, out var missing);
            if (missing.Count > 0)
            {
                Debug.LogWarning($"{StartupMeasure.LOG_PREFIX} The Player Settings of this version of Unity have no {string.Join(", ", missing)}: the launch screen of iOS may differ from the one of the package.");
            }

            if (changed.Count > 0)
            {
                Debug.Log($"{StartupMeasure.LOG_PREFIX} The launch screen of iOS in the Player Settings now shows the icon of the launch screen: {string.Join(", ", changed)} changed.");
            }
        }

        /// <summary>
        ///     Sets the properties of the launch screen of iOS that differ, in serialized Player Settings, and returns their
        ///     names. The names of the properties not found go to <paramref name="missing" />.
        /// </summary>
        internal static List<string> Apply(SerializedObject playerSettings, Texture2D icon, Color background, out List<string> missing)
        {
            var changed = new List<string>();
            missing = new List<string>();
            playerSettings.Update();
            foreach (var type in new[] { IPHONE_TYPE, IPAD_TYPE })
            {
                Set(playerSettings, type, changed, missing, property => property.intValue == (int)iOSLaunchScreenType.ImageAndBackgroundConstant, property => property.intValue = (int)iOSLaunchScreenType.ImageAndBackgroundConstant);
            }

            foreach (var image in new[] { PORTRAIT_IMAGE, LANDSCAPE_IMAGE, IPAD_IMAGE })
            {
                Set(playerSettings, image, changed, missing, property => property.objectReferenceValue == icon, property => property.objectReferenceValue = icon);
            }

            foreach (var color in new[] { IPHONE_BACKGROUND, IPAD_BACKGROUND })
            {
                Set(playerSettings, color, changed, missing, property => IsSameColor(property.colorValue, background), property => property.colorValue = background);
            }

            foreach (var size in new[] { IPHONE_SIZE, IPAD_SIZE })
            {
                Set(playerSettings, size, changed, missing, property => Mathf.Approximately(property.floatValue, LaunchScreen.ICON_SIZE), property => property.floatValue = LaunchScreen.ICON_SIZE);
            }

            if (changed.Count > 0)
            {
                playerSettings.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        /// <summary>
        ///     Whether two colours are the same once stored in 8 bits per channel, as the Player Settings store them.
        /// </summary>
        internal static bool IsSameColor(Color a, Color b)
        {
            Color32 first = a;
            Color32 second = b;
            return first.r == second.r && first.g == second.g && first.b == second.b && first.a == second.a;
        }

        private static void Set(SerializedObject playerSettings, string name, List<string> changed, List<string> missing, Func<SerializedProperty, bool> isSet, Action<SerializedProperty> set)
        {
            var property = playerSettings.FindProperty(name);
            if (property == null)
            {
                missing.Add(name);
                return;
            }

            if (isSet(property)) return;

            set(property);
            changed.Add(name);
        }
    }
}