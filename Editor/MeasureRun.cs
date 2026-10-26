using System;
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

        internal MeasureRun(MeasureSettings settings, MeasureResult result, string resultPath, DateTime startedAt)
        {
            Settings = settings;
            Result = result;
            ResultPath = resultPath;
            StartedAt = startedAt;
            StepStartedAt = startedAt;
            StepCount = MeasureProgress.CountSteps(settings);
        }

        public MeasureSettings Settings { get; }
        public MeasureResult Result { get; }

        /// <summary>
        ///     The JSON file of the result, relative to the project.
        /// </summary>
        public string ResultPath { get; }

        /// <summary>
        ///     The start of the measure, in UTC.
        /// </summary>
        public DateTime StartedAt { get; }

        /// <summary>
        ///     The steps of the measure: the device check, the build, the install, the first launch, then each cold launch.
        /// </summary>
        public int StepCount { get; }

        /// <summary>
        ///     The step under way, <see cref="MeasureResult.Step" />, from 0: the number of steps done.
        /// </summary>
        public int StepIndex { get; private set; } = -1;

        /// <summary>
        ///     The start of the step under way, in UTC.
        /// </summary>
        public DateTime StepStartedAt { get; private set; }

        /// <summary>
        ///     The share of the steps done, from 0 to 1.
        /// </summary>
        public float Fraction => StepCount == 0 ? 0 : (float)Math.Max(0, StepIndex) / StepCount;

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

        internal void StartStep(string step)
        {
            Result.Step = step;
            StepIndex++;
            StepStartedAt = DateTime.UtcNow;
        }

        internal void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ResultPath)));
            File.WriteAllText(ResultPath, JsonUtility.ToJson(Result, true));
        }
    }
}