using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Reads the timeline that <see cref="StartupTimeline" /> logs: <c>[Startup] origin=process scripts_start=640 …</c>.
    /// </summary>
    internal static class StartupLine
    {
        private static readonly Regex _line = new(Regex.Escape(StartupTimeline.LOG_PREFIX) + @" origin=(\w+)((?: \w+=-?\d+)*)");

        /// <summary>
        ///     The timeline of the last <c>[Startup]</c> line of the text, which may be a whole log. False without such a
        ///     line.
        /// </summary>
        public static bool TryParseLast(string text, out Launch launch)
        {
            launch = null;
            var matches = _line.Matches(text ?? string.Empty);
            if (matches.Count == 0) return false;

            var match = matches[matches.Count - 1];
            launch = new Launch { Origin = match.Groups[1].Value };
            foreach (var pair in match.Groups[2].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                if (!int.TryParse(pair.Substring(separator + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var milliseconds)) continue;

                launch.Milestones.Add(new Milestone { Name = pair.Substring(0, separator), Milliseconds = milliseconds });
            }

            return true;
        }
    }
}