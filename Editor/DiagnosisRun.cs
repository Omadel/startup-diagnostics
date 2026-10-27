using System;
using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     One run of the diagnosis: when it ran, and its results in the order to show them.
    /// </summary>
    [Serializable]
    internal sealed class DiagnosisRun
    {
        /// <summary>
        ///     When the diagnosis ran, in local time, round-trip format; empty when it never ran.
        /// </summary>
        public string RunAt;

        public List<DiagnosisResult> Results = new();
    }
}