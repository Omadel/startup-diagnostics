using System;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A startup measure that cannot go on, with a message for the user: what went wrong and what to do about it.
    /// </summary>
    internal sealed class MeasureException : Exception
    {
        public MeasureException(string message) : base(message)
        {
        }
    }
}