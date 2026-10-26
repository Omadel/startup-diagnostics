using System;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class MeasureProgressTests
    {
        [Test]
        public void CountSteps_NewBuild_CountsEachStep()
        {
            // The device check, the build, the install, the first launch and 10 cold launches.
            Assert.That(MeasureProgress.CountSteps(new MeasureSettings()), Is.EqualTo(14));
        }

        [Test]
        public void CountSteps_LastBuild_LeavesTheBuildOut()
        {
            Assert.That(MeasureProgress.CountSteps(new MeasureSettings { Rebuild = false }), Is.EqualTo(13));
        }

        [Test]
        public void CountSteps_GameOnTheDevice_CountsTheCheckAndTheColdLaunches()
        {
            Assert.That(MeasureProgress.CountSteps(new MeasureSettings { Rebuild = false, CleanInstall = false, Runs = 3 }), Is.EqualTo(4));
        }

        [Test]
        public void CountSteps_NegativeRuns_CountsNoColdLaunch()
        {
            Assert.That(MeasureProgress.CountSteps(new MeasureSettings { Rebuild = false, CleanInstall = false, Runs = -2 }), Is.EqualTo(1));
        }

        [Test]
        public void CountSteps_ProfilerCapture_CountsOneCaptureInsteadOfTheColdLaunches()
        {
            // The device check, the build, the install, the first launch and the capture.
            Assert.That(MeasureProgress.CountSteps(new MeasureSettings { ProfilerCapture = true }), Is.EqualTo(5));
        }

        [Test]
        public void GetTitle_GivesTheStepAndTheTimes()
        {
            var title = MeasureProgress.GetTitle("Building", 1, 14, TimeSpan.FromSeconds(83), TimeSpan.FromSeconds(95));

            Assert.That(title, Is.EqualTo("Step 2 of 14: Building, 1 min 23 s · 1 min 35 s in all"));
        }

        [Test]
        public void StartStep_CountsTheStepsDone()
        {
            var startedAt = DateTime.UtcNow.AddMinutes(-1);
            var run = new MeasureRun(new MeasureSettings { Rebuild = false, CleanInstall = false, Runs = 3 }, new MeasureResult(), "Library/StartupDiagnostics/test.json", startedAt);

            Assert.That(run.StepIndex, Is.EqualTo(-1));
            Assert.That(run.Fraction, Is.Zero);

            run.StartStep("Checking the device");
            run.StartStep("Cold launch 1/3");

            Assert.That(run.StepIndex, Is.EqualTo(1));
            Assert.That(run.Fraction, Is.EqualTo(0.25f));
            Assert.That(run.Result.Step, Is.EqualTo("Cold launch 1/3"));
            Assert.That(run.StepStartedAt, Is.GreaterThan(startedAt));
        }
    }
}