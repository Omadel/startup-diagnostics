using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class StepTimesTests
    {
        [Test]
        public void FromMeasure_MilestonesOfThePackageAndOfTheProject_GivesEachStep()
        {
            var times = StepTimes.FromMeasure(CreateMeasure("Android"));

            Assert.That(times.GetMilliseconds(StartupStep.EngineInitialization), Is.EqualTo(537));
            Assert.That(times.GetMilliseconds(StartupStep.FirstScene), Is.EqualTo(184));
            Assert.That(times.GetMilliseconds(StartupStep.FirstFrame), Is.EqualTo(199.5));
            Assert.That(times.GetMilliseconds(StartupStep.ProjectMilestones), Is.EqualTo(536.5));
            Assert.That(times.Total, Is.EqualTo(1457));
            Assert.That(times.Longest, Is.EqualTo(StartupStep.EngineInitialization));
            Assert.That(times.GetShare(StartupStep.FirstScene), Is.EqualTo(184 / 1457.0).Within(1e-9));
        }

        [Test]
        public void FromMeasure_PerceivedWait_IsNotTimed()
        {
            var times = StepTimes.FromMeasure(CreateMeasure("Android"));

            Assert.That(times.GetMilliseconds(StartupStep.PerceivedWait), Is.Null);
            Assert.That(times.GetShare(StartupStep.PerceivedWait), Is.Null);
        }

        [Test]
        public void FromMeasure_LongProjectMilestones_AreTheLongestStep()
        {
            var measure = CreateMeasure("iOS");
            measure.ColdSummary.Single(milestone => milestone.Name == "first_scene_frame").Median = 3000;

            Assert.That(StepTimes.FromMeasure(measure).Longest, Is.EqualTo(StartupStep.ProjectMilestones));
        }

        [Test]
        public void FromMeasure_FailedCaptureOrIncomplete_IsNull()
        {
            var failed = CreateMeasure("Android");
            failed.Status = MeasureStatus.FAILED;
            var capture = CreateMeasure("Android");
            capture.ProfilerCapture = true;
            var incomplete = CreateMeasure("Android");
            incomplete.ColdSummary.RemoveAll(milestone => milestone.Name == StartupTimeline.FIRST_FRAME);

            Assert.That(StepTimes.FromMeasure(failed), Is.Null);
            Assert.That(StepTimes.FromMeasure(capture), Is.Null);
            Assert.That(StepTimes.FromMeasure(incomplete), Is.Null);
            Assert.That(StepTimes.FromMeasure(null), Is.Null);
        }

        [Test]
        public void FromNewest_Measures_PrefersThePlatform()
        {
            var newestFirst = new List<MeasureFile> { new("a.json", CreateMeasure("iOS")), new("b.json", CreateMeasure("Android")) };

            Assert.That(StepTimes.FromNewest(newestFirst, "Android").Measure.Platform, Is.EqualTo("Android"));
            Assert.That(StepTimes.FromNewest(newestFirst, "WebGL").Measure.Platform, Is.EqualTo("iOS"));
            Assert.That(StepTimes.FromNewest(new List<MeasureFile>(), "Android"), Is.Null);
        }

        [Test]
        public void Sort_LongestStep_PutsItsWarningsAndInfosFirstAndThePassedLast()
        {
            var results = new List<DiagnosisResult>
            {
                new() { Check = "passed", Level = DiagnosisLevel.Passed, Step = StartupStep.ProjectMilestones },
                new() { Check = "early warning", Level = DiagnosisLevel.Warning, Step = StartupStep.FirstScene },
                new() { Check = "late info", Level = DiagnosisLevel.Info, Step = StartupStep.ProjectMilestones },
                new() { Check = "late warning", Level = DiagnosisLevel.Warning, Step = StartupStep.ProjectMilestones }
            };

            var sorted = DiagnosisReport.Sort(results, StartupStep.ProjectMilestones).Select(result => result.Check);

            Assert.That(sorted, Is.EqualTo(new[] { "late warning", "late info", "early warning", "passed" }));
        }

        [Test]
        public void GetStepLabel_TimedAndNotTimedSteps_ShowTheShareWhenKnown()
        {
            var times = StepTimes.FromMeasure(CreateMeasure("Android"));

            Assert.That(DiagnosisReport.GetStepLabel(StartupStep.EngineInitialization, times), Is.EqualTo("Engine initialization · 37%"));
            Assert.That(DiagnosisReport.GetStepLabel(StartupStep.PerceivedWait, times), Is.EqualTo("Perceived wait"));
            Assert.That(DiagnosisReport.GetStepLabel(StartupStep.FirstScene, null), Is.EqualTo("First scene"));
        }

        [Test]
        public void DescribeLongestStep_Measure_GivesTheStepItsTimeAndShare()
        {
            var times = StepTimes.FromMeasure(CreateMeasure("Android"));

            var description = DiagnosisReport.DescribeLongestStep(times, "2026-10-27 19:35:21");

            Assert.That(description, Is.EqualTo("Longest step of the last measure, Pixel 6 on 2026-10-27 19:35:21: engine initialization, 0.54 s of 1.46 s (37%). Its checks come first."));
            Assert.That(DiagnosisReport.DescribeLongestStep(null, null), Does.StartWith("No measure of the start yet"));
        }

        private static MeasureResult CreateMeasure(string platform)
        {
            return new MeasureResult
            {
                Status = MeasureStatus.SUCCEEDED,
                Platform = platform,
                DeviceModel = "Pixel 6",
                ColdSummary = new List<MilestoneSummary>
                {
                    new() { Name = StartupTimeline.SCRIPTS_START, Median = 537 },
                    new() { Name = StartupTimeline.FIRST_SCENE_LOADED, Median = 721 },
                    new() { Name = StartupTimeline.FIRST_FRAME, Median = 920.5 },
                    new() { Name = "addressables_ready", Median = 1000 },
                    new() { Name = "scene_loaded", Median = 1374.5 },
                    new() { Name = "first_scene_frame", Median = 1457 },
                    new() { Name = MilestoneSummary.OS_FIRST_FRAME, Median = 132.5 }
                }
            };
        }
    }
}