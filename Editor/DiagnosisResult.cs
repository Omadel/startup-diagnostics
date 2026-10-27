using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     What one check of the diagnosis found, with the section of the optimization guide that explains it.
    /// </summary>
    internal sealed class DiagnosisResult
    {
        /// <summary>
        ///     Identifier of the check, such as <c>unity-splash-screen</c>.
        /// </summary>
        public string Check;

        /// <summary>
        ///     Name of the check, shown in the diagnosis.
        /// </summary>
        public string Title;

        public DiagnosisLevel Level;

        /// <summary>
        ///     The step of the start that what the check found lengthens.
        /// </summary>
        public StartupStep Step;

        /// <summary>
        ///     What the check found, why it matters to the start, and what to do.
        /// </summary>
        public string Message;

        /// <summary>
        ///     Anchor of the section of the optimization guide that explains the check: see <see cref="DiagnosisGuide" />.
        /// </summary>
        public string GuideSection;

        /// <summary>
        ///     What is in cause, largest first when the items have a size.
        /// </summary>
        public List<DiagnosisItem> Items = new();
    }
}