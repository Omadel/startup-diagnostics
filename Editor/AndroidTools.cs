using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The Android tools of Unity's Android module: adb from its SDK, java from its JDK, and bundletool.
    /// </summary>
    internal sealed class AndroidTools
    {
        private const string EXTERNAL_TOOLS_SETTINGS = "UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions";
        private const string BUNDLETOOL_PREFIX = "bundletool-all-";
        private const string BUNDLETOOL_EXTENSION = ".jar";

        private AndroidTools(string adb, string java, string bundletool)
        {
            Adb = adb;
            Java = java;
            Bundletool = bundletool;
        }

        public string Adb { get; }
        public string Java { get; }

        /// <summary>
        ///     The bundletool jar, run by <see cref="Java" />, or null if the module has none.
        /// </summary>
        public string Bundletool { get; }

        /// <summary>
        ///     The tools set in <i>Preferences &gt; External Tools</i>, Unity's own by default. Throws a
        ///     <see cref="MeasureException" /> without the Android module or without adb.
        /// </summary>
        public static AndroidTools Find()
        {
            string engine;
            try
            {
                engine = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
            }
            catch (Exception)
            {
                engine = null;
            }

            if (string.IsNullOrEmpty(engine) || !Directory.Exists(engine)) throw new MeasureException("Android Build Support is not installed for this Unity version: add it from the Unity Hub.");

            var settings = Type.GetType(EXTERNAL_TOOLS_SETTINGS);
            var sdk = GetSetting(settings, "sdkRootPath") ?? Path.Combine(engine, "SDK");
            var jdk = GetSetting(settings, "jdkRootPath") ?? Path.Combine(engine, "OpenJDK");
            var toolsFolder = Path.Combine(engine, "Tools");
            var bundletools = Directory.Exists(toolsFolder) ? Directory.GetFiles(toolsFolder, BUNDLETOOL_PREFIX + "*" + BUNDLETOOL_EXTENSION) : Array.Empty<string>();
            var tools = FromFolders(sdk, jdk, bundletools, Environment.OSVersion.Platform == PlatformID.Win32NT);
            if (!File.Exists(tools.Adb)) throw new MeasureException($"No adb at {tools.Adb}: set the Android SDK in Preferences > External Tools.");

            return tools;
        }

        /// <summary>
        ///     The tools in an SDK and a JDK, with the latest of the bundletool jars.
        /// </summary>
        internal static AndroidTools FromFolders(string sdkRoot, string jdkRoot, IEnumerable<string> bundletools, bool windows)
        {
            var executable = windows ? ".exe" : string.Empty;
            var adb = Path.Combine(sdkRoot, "platform-tools", "adb" + executable);
            var java = Path.Combine(jdkRoot, "bin", "java" + executable);
            return new AndroidTools(adb, java, PickLatestBundletool(bundletools));
        }

        /// <summary>
        ///     The jar of the highest version, by number and not by text (1.17.2 after 1.9.0), or null without one.
        /// </summary>
        internal static string PickLatestBundletool(IEnumerable<string> paths)
        {
            string latest = null;
            Version latestVersion = null;
            foreach (var path in paths)
            {
                var name = Path.GetFileName(path);
                if (!name.StartsWith(BUNDLETOOL_PREFIX, StringComparison.Ordinal) || !name.EndsWith(BUNDLETOOL_EXTENSION, StringComparison.Ordinal)) continue;

                var number = name.Substring(BUNDLETOOL_PREFIX.Length, name.Length - BUNDLETOOL_PREFIX.Length - BUNDLETOOL_EXTENSION.Length);
                if (!Version.TryParse(number, out var version) || (latestVersion != null && version <= latestVersion)) continue;

                latest = path;
                latestVersion = version;
            }

            return latest;
        }

        private static string GetSetting(Type settings, string name)
        {
            try
            {
                var value = settings?.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                return string.IsNullOrEmpty(value) ? null : value;
            }
            catch (TargetInvocationException)
            {
                return null;
            }
        }
    }
}