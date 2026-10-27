using System.Threading.Tasks;
using UnityEditor;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Prepares the project for the build of a startup measure, for example by publishing its remote content. Each
    ///     implementation with a parameterless constructor is created and called just before the build, on the main thread
    ///     and out of the player loop, where it may build, in the order of their full type names; the build waits for the
    ///     returned task. An exception fails the measure, with its message.
    /// </summary>
    public interface IBeforeMeasureBuild
    {
        Task OnBeforeMeasureBuildAsync(BuildTarget target);
    }
}