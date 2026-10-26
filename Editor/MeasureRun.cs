using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A startup measure started by <see cref="StartupMeasure.Start" />: its result, updated as it goes and written to
    ///     <see cref="ResultPath" /> at each step.
    /// </summary>
    public sealed class MeasureRun
    {
        private readonly CancellationTokenSource _cancellation = new();

        internal MeasureRun(MeasureSettings settings, MeasureResult result, string resultPath)
        {
            Settings = settings;
            Result = result;
            ResultPath = resultPath;
        }

        public MeasureSettings Settings { get; }
        public MeasureResult Result { get; }

        /// <summary>
        ///     The JSON file of the result, relative to the project.
        /// </summary>
        public string ResultPath { get; }

        /// <summary>
        ///     Ends with the result once the measure has succeeded, failed or been cancelled. It does not throw: a failure is
        ///     in <see cref="MeasureResult.Status" /> and <see cref="MeasureResult.Error" />.
        /// </summary>
        public Task<MeasureResult> Completion { get; internal set; }

        public bool IsDone => Completion != null && Completion.IsCompleted;

        internal CancellationToken Cancellation => _cancellation.Token;

        /// <summary>
        ///     Stops the measure at its next step; the game may be left running on the device.
        /// </summary>
        public void Cancel()
        {
            _cancellation.Cancel();
        }

        internal void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ResultPath)));
            File.WriteAllText(ResultPath, JsonUtility.ToJson(Result, true));
        }
    }
}