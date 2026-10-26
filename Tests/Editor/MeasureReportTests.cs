using System;
using System.Linq;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class MeasureReportTests
    {
        [Test]
        public void GetRows_Measure_GivesEachMilestoneWithItsFirstLaunchAndStep()
        {
            var result = CreateResult(CreateLaunch(("first_frame", 735), ("first_scene_frame", 1677)), CreateLaunch(("first_frame", 450), ("first_scene_frame", 720)), CreateLaunch(("first_frame", 454), ("first_scene_frame", 733)), CreateLaunch(("first_frame", 453), ("first_scene_frame", 670)));

            var rows = MeasureReport.GetRows(result, null);

            Assert.That(rows.Select(row => row.Name), Is.EqualTo(new[] { "first_frame", "first_scene_frame" }));
            Assert.That(rows.Select(row => row.FirstLaunch), Is.EqualTo(new[] { 735, 1677 }));
            Assert.That(rows.Select(row => row.Cold.Median), Is.EqualTo(new[] { 453.0, 720.0 }));
            Assert.That(rows.Select(row => row.Step), Is.EqualTo(new[] { 453.0, 270.0 }));
            Assert.That(rows.All(row => row.Previous == null && double.IsNaN(row.Change) && !row.IsSlower), Is.True);
        }

        [Test]
        public void GetRows_OsFirstFrame_HasNoStep()
        {
            var cold = CreateLaunch(("first_frame", 900));
            cold.OsFirstFrame = 130;
            var first = CreateLaunch(("first_frame", 1800));
            first.OsFirstFrame = 900;

            var rows = MeasureReport.GetRows(CreateResult(first, cold), null);

            Assert.That(rows.Select(row => row.Name), Is.EqualTo(new[] { "first_frame", MilestoneSummary.OS_FIRST_FRAME }));
            Assert.That(rows[1].FirstLaunch, Is.EqualTo(900));
            Assert.That(rows[1].Cold.Median, Is.EqualTo(130));
            Assert.That(rows[1].Step, Is.NaN);
        }

        [Test]
        public void GetRows_BeforeTheFirstColdLaunch_GivesTheMilestonesOfTheFirstLaunch()
        {
            var rows = MeasureReport.GetRows(CreateResult(CreateLaunch(("first_frame", 735), ("first_scene_frame", 1677))), null);

            Assert.That(rows.Select(row => row.Name), Is.EqualTo(new[] { "first_frame", "first_scene_frame" }));
            Assert.That(rows.Select(row => row.FirstLaunch), Is.EqualTo(new[] { 735, 1677 }));
            Assert.That(rows.All(row => row.Cold == null), Is.True);
        }

        [Test]
        public void GetRows_Previous_FlagsTheMilestonesMoreThanTenPercentSlower()
        {
            var previous = CreateResult(null, CreateLaunch(("scripts_start", 400), ("first_frame", 500), ("first_scene_frame", 1000)));
            var result = CreateResult(null, CreateLaunch(("scripts_start", 440), ("first_frame", 551), ("first_scene_frame", 850)));

            var rows = MeasureReport.GetRows(result, previous);

            Assert.That(rows.Select(row => row.Previous.Median), Is.EqualTo(new[] { 400.0, 500.0, 1000.0 }));
            Assert.That(rows.Select(row => row.Change), Is.EqualTo(new[] { 0.1, 0.102, -0.15 }).Within(1e-9));
            Assert.That(rows.Select(row => row.IsSlower), Is.EqualTo(new[] { false, true, false }));
        }

        [Test]
        public void GetRows_MilestoneNewSinceThePrevious_HasNoChange()
        {
            var previous = CreateResult(null, CreateLaunch(("first_frame", 500)));
            var result = CreateResult(null, CreateLaunch(("first_frame", 500), ("game_frame", 900)));

            var rows = MeasureReport.GetRows(result, previous);

            Assert.That(rows[1].Previous, Is.Null);
            Assert.That(rows[1].Change, Is.NaN);
        }

        [Test]
        public void GetComparison_SlowerMilestones_WarnsWithEachOne()
        {
            var rows = new[] { new MilestoneRow { Name = "first_frame", Change = 0.12, IsSlower = true }, new MilestoneRow { Name = "first_scene_frame", Change = 0.05 } };

            var comparison = MeasureReport.GetComparison(rows, "2026-10-25 21:14:00");

            Assert.That(comparison.Message, Is.EqualTo("Slower by more than 10% than in the measure of 2026-10-25 21:14:00: first_frame +12%."));
            Assert.That(comparison.IsWarning, Is.True);
        }

        [Test]
        public void GetComparison_NoSlowerMilestone_SaysSo()
        {
            var comparison = MeasureReport.GetComparison(new[] { new MilestoneRow { Name = "first_frame", Change = 0.05 } }, "2026-10-25 21:14:00");

            Assert.That(comparison.Message, Is.EqualTo("No milestone is more than 10% slower than in the measure of 2026-10-25 21:14:00."));
            Assert.That(comparison.IsWarning, Is.False);
        }

        [Test]
        public void GetComparison_NoPrevious_SaysThereIsNone()
        {
            var comparison = MeasureReport.GetComparison(new MilestoneRow[0], null);

            Assert.That(comparison.Message, Does.StartWith("No earlier measure"));
            Assert.That(comparison.IsWarning, Is.False);
        }

        [Test]
        public void GetShare_Step_IsItsShareOfTheLastMilestoneOfTheTimeline()
        {
            var rows = new[] { new MilestoneRow { Name = "first_frame", Step = 450, Cold = new MilestoneSummary { Median = 450 } }, new MilestoneRow { Name = "first_scene_frame", Step = 270, Cold = new MilestoneSummary { Median = 720 } }, new MilestoneRow { Name = MilestoneSummary.OS_FIRST_FRAME, Cold = new MilestoneSummary { Median = 130 } } };

            Assert.That(MeasureReport.GetShare(rows[0], rows), Is.EqualTo(0.625f).Within(1e-6));
            Assert.That(MeasureReport.GetShare(rows[1], rows), Is.EqualTo(0.375f).Within(1e-6));
            Assert.That(MeasureReport.GetShare(rows[2], rows), Is.Zero);
        }

        [TestCase(722, "722")]
        [TestCase(713.5, "713.5")]
        [TestCase(double.NaN, "")]
        public void FormatMilliseconds_Value_KeepsTheHalfOfAMedian(double milliseconds, string expected)
        {
            Assert.That(MeasureReport.FormatMilliseconds(milliseconds), Is.EqualTo(expected));
        }

        [TestCase(0.1234, "+12%")]
        [TestCase(-0.031, "-3%")]
        [TestCase(-0.004, "0%")]
        [TestCase(double.NaN, "")]
        public void FormatChange_Change_IsASignedPercentage(double change, string expected)
        {
            Assert.That(MeasureReport.FormatChange(change), Is.EqualTo(expected));
        }

        [TestCase(42, "42 s")]
        [TestCase(383.3, "6 min 23 s")]
        public void FormatDuration_Duration_GivesMinutesAndSeconds(double seconds, string expected)
        {
            Assert.That(MeasureReport.FormatDuration(TimeSpan.FromSeconds(seconds)), Is.EqualTo(expected));
        }

        private static MeasureResult CreateResult(Launch firstLaunch, params Launch[] coldLaunches)
        {
            var result = new MeasureResult { FirstLaunch = firstLaunch, Status = MeasureStatus.SUCCEEDED };
            result.ColdLaunches.AddRange(coldLaunches);
            result.ColdSummary = LaunchStatistics.Summarize(result.ColdLaunches);
            return result;
        }

        private static Launch CreateLaunch(params (string Name, int Milliseconds)[] milestones)
        {
            var launch = new Launch { Origin = StartupTimeline.ORIGIN_PROCESS };
            launch.Milestones.AddRange(milestones.Select(milestone => new Milestone { Name = milestone.Name, Milliseconds = milestone.Milliseconds }));
            return launch;
        }
    }
}