using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The settings of a measure launched by <see cref="StartupMeasure.RunFromCommandLine" />, from the arguments of
    ///     the editor.
    /// </summary>
    internal static class MeasureCommandLine
    {
        public const string PLATFORM = "-startupPlatform";
        public const string DEVICE = "-startupDevice";
        public const string RUNS = "-startupRuns";
        public const string BUILD = "-startupBuild";
        public const string NO_BUILD = "-startupNoBuild";
        public const string NO_INSTALL = "-startupNoInstall";

        /// <summary>
        ///     The settings from the arguments, on the active build target by default. Throws a
        ///     <see cref="MeasureException" /> for a missing or invalid value. Other arguments are left to the editor.
        /// </summary>
        public static MeasureSettings Parse(IReadOnlyList<string> arguments, BuildTarget activeTarget)
        {
            var settings = new MeasureSettings { Platform = activeTarget };
            for (var i = 0; i < arguments.Count; i++)
            {
                switch (arguments[i])
                {
                    case PLATFORM:
                        settings.Platform = ParsePlatform(GetValue(arguments, ref i));
                        break;
                    case DEVICE:
                        settings.Device = GetValue(arguments, ref i);
                        break;
                    case RUNS:
                        settings.Runs = ParseRuns(GetValue(arguments, ref i));
                        break;
                    case BUILD:
                        settings.BuildPath = GetValue(arguments, ref i);
                        break;
                    case NO_BUILD:
                        settings.Rebuild = false;
                        break;
                    case NO_INSTALL:
                        settings.CleanInstall = false;
                        settings.Rebuild = false;
                        break;
                }
            }

            return settings;
        }

        private static string GetValue(IReadOnlyList<string> arguments, ref int index)
        {
            if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("-", StringComparison.Ordinal)) throw new MeasureException($"{arguments[index]} needs a value.");

            index++;
            return arguments[index];
        }

        private static BuildTarget ParsePlatform(string value)
        {
            if (string.Equals(value, "android", StringComparison.OrdinalIgnoreCase)) return BuildTarget.Android;
            if (string.Equals(value, "ios", StringComparison.OrdinalIgnoreCase)) return BuildTarget.iOS;

            throw new MeasureException($"{PLATFORM} {value}: the platform is Android or iOS.");
        }

        private static int ParseRuns(string value)
        {
            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var runs) && runs > 0) return runs;

            throw new MeasureException($"{RUNS} {value}: the number of cold launches is a whole number above 0.");
        }
    }
}