using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class LaunchStatisticsTests
    {
        [TestCase(new[] { 7 }, 7)]
        [TestCase(new[] { 3, 1, 2 }, 2)]
        [TestCase(new[] { 4, 1, 3, 2 }, 2.5)]
        [TestCase(new[] { 10, 10, 30, 20 }, 15)]
        public void Median_Values_IsTheMiddleOrTheMeanOfTheTwoMiddleOnes(int[] values, double expected)
        {
            Assert.That(LaunchStatistics.Median(values), Is.EqualTo(expected));
        }

        [Test]
        public void Median_Values_LeavesThemInTheirOrder()
        {
            var values = new List<int> { 3, 1, 2 };

            LaunchStatistics.Median(values);

            Assert.That(values, Is.EqualTo(new[] { 3, 1, 2 }));
        }

        [Test]
        public void Summarize_Launches_GivesTheMedianMinAndMaxOfEachMilestone()
        {
            var launches = new[] { CreateLaunch(("first_frame", 900), ("first_scene_frame", 1400)), CreateLaunch(("first_frame", 950), ("first_scene_frame", 1500)), CreateLaunch(("first_frame", 870), ("first_scene_frame", 1450)) };

            var summaries = LaunchStatistics.Summarize(launches);

            Assert.That(summaries.Select(summary => summary.Name), Is.EqualTo(new[] { "first_frame", "first_scene_frame" }));
            Assert.That(summaries.Select(summary => summary.Median), Is.EqualTo(new[] { 900.0, 1450.0 }));
            Assert.That(summaries.Select(summary => summary.Min), Is.EqualTo(new[] { 870, 1400 }));
            Assert.That(summaries.Select(summary => summary.Max), Is.EqualTo(new[] { 950, 1500 }));
        }

        [Test]
        public void Summarize_MilestoneOfSomeLaunches_SumsUpTheLaunchesThatHaveIt()
        {
            var launches = new[] { CreateLaunch(("scripts_start", 500), ("first_frame", 900)), CreateLaunch(("scripts_start", 520), ("addressables_ready", 1000), ("first_frame", 940)) };

            var summaries = LaunchStatistics.Summarize(launches);

            Assert.That(summaries.Select(summary => summary.Name), Is.EqualTo(new[] { "scripts_start", "first_frame", "addressables_ready" }));
            Assert.That(summaries[2].Median, Is.EqualTo(1000));
        }

        [Test]
        public void Summarize_OsFirstFrameOfEveryLaunch_AddsItLast()
        {
            var launches = new[] { CreateLaunch(("first_frame", 900)), CreateLaunch(("first_frame", 950)) };
            launches[0].OsFirstFrame = 130;
            launches[1].OsFirstFrame = 140;

            var summaries = LaunchStatistics.Summarize(launches);

            Assert.That(summaries.Select(summary => summary.Name), Is.EqualTo(new[] { "first_frame", MilestoneSummary.OS_FIRST_FRAME }));
            Assert.That(summaries[1].Median, Is.EqualTo(135));
        }

        [Test]
        public void Summarize_OsFirstFrameOfSomeLaunches_LeavesItOut()
        {
            var launches = new[] { CreateLaunch(("first_frame", 900)), CreateLaunch(("first_frame", 950)) };
            launches[0].OsFirstFrame = 130;

            var summaries = LaunchStatistics.Summarize(launches);

            Assert.That(summaries.Select(summary => summary.Name), Is.EqualTo(new[] { "first_frame" }));
        }

        [Test]
        public void Summarize_NoLaunch_GivesNothing()
        {
            Assert.That(LaunchStatistics.Summarize(new Launch[0]), Is.Empty);
        }

        [Test]
        public void SummarizeSteps_Launches_GivesTheMedianTimeSinceTheMilestoneBefore()
        {
            var launches = new[] { CreateLaunch(("scripts_start", 360), ("first_frame", 450), ("first_scene_frame", 720)), CreateLaunch(("scripts_start", 364), ("first_frame", 454), ("first_scene_frame", 670)), CreateLaunch(("scripts_start", 358), ("first_frame", 450), ("first_scene_frame", 733)) };

            var steps = LaunchStatistics.SummarizeSteps(launches);

            Assert.That(steps["scripts_start"], Is.EqualTo(360));
            Assert.That(steps["first_frame"], Is.EqualTo(90));
            Assert.That(steps["first_scene_frame"], Is.EqualTo(270));
        }

        [Test]
        public void SummarizeSteps_MilestoneOfSomeLaunches_TakesTheMilestoneBeforeItInEachLaunch()
        {
            var launches = new[] { CreateLaunch(("first_frame", 900), ("first_scene_frame", 1400)), CreateLaunch(("first_frame", 940), ("addressables_ready", 1000), ("first_scene_frame", 1500)) };

            var steps = LaunchStatistics.SummarizeSteps(launches);

            Assert.That(steps["addressables_ready"], Is.EqualTo(60));
            Assert.That(steps["first_scene_frame"], Is.EqualTo(500));
        }

        private static Launch CreateLaunch(params (string Name, int Milliseconds)[] milestones)
        {
            var launch = new Launch { Origin = StartupTimeline.ORIGIN_PROCESS };
            launch.Milestones.AddRange(milestones.Select(milestone => new Milestone { Name = milestone.Name, Milliseconds = milestone.Milliseconds }));
            return launch;
        }
    }
}