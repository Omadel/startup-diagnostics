using System;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A milestone of one launch, in milliseconds since the origin of its timeline.
    /// </summary>
    [Serializable]
    public sealed class Milestone
    {
        public string Name;
        public int Milliseconds;
    }
}