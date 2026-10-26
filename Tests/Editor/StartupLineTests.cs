using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class StartupLineTests
    {
        [Test]
        public void TryParseLast_Line_ReadsTheOriginAndTheMilestonesInOrder()
        {
            var parsed = StartupLine.TryParseLast("[Startup] origin=process scripts_start=504 first_scene_loaded=686 first_frame=926 first_scene_frame=1449", out var launch);

            Assert.That(parsed, Is.True);
            Assert.That(launch.Origin, Is.EqualTo(StartupTimeline.ORIGIN_PROCESS));
            Assert.That(launch.Milestones.Select(milestone => milestone.Name), Is.EqualTo(new[] { "scripts_start", "first_scene_loaded", "first_frame", "first_scene_frame" }));
            Assert.That(launch.Milestones.Select(milestone => milestone.Milliseconds), Is.EqualTo(new[] { 504, 686, 926, 1449 }));
        }

        [Test]
        public void TryParseLast_LineOfTheTimeline_ReadsWhatFormatTimelineWrites()
        {
            var milestones = new[] { new KeyValuePair<string, double>(StartupTimeline.SCRIPTS_START, 0.5), new KeyValuePair<string, double>(StartupTimeline.FIRST_FRAME, 0.925) };
            var line = StartupTimeline.FormatTimeline(StartupTimeline.ORIGIN_PROCESS, 0, milestones);

            StartupLine.TryParseLast(line, out var launch);

            Assert.That(launch.Milestones.Select(milestone => milestone.Milliseconds), Is.EqualTo(new[] { 500, 925 }));
        }

        [Test]
        public void TryParseLast_Logcat_ReadsTheLastLineAfterItsPrefix()
        {
            var log = "--------- beginning of main\n" + "10-26 00:01:02.345  1234  1250 I Unity   : [Startup] origin=process scripts_start=500 first_frame=900\n" + "10-26 00:01:03.000  1234  1250 I Unity   : [Remote content] Up to date\n" + "10-26 00:01:09.123  2345  2360 I Unity   : [Startup] origin=process scripts_start=480 first_frame=880\n";

            StartupLine.TryParseLast(log, out var launch);

            Assert.That(launch.Milestones.Select(milestone => milestone.Milliseconds), Is.EqualTo(new[] { 480, 880 }));
        }

        [Test]
        public void TryParseLast_NegativeTime_KeepsItsSign()
        {
            StartupLine.TryParseLast("[Startup] origin=engine scripts_start=-12", out var launch);

            Assert.That(launch.Milestones.Single().Milliseconds, Is.EqualTo(-12));
        }

        [Test]
        public void TryParseLast_OriginOnly_GivesNoMilestone()
        {
            var parsed = StartupLine.TryParseLast("[Startup] origin=play_mode", out var launch);

            Assert.That(parsed, Is.True);
            Assert.That(launch.Milestones, Is.Empty);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("[Startup] The start has already ended, scene_loaded is left out.")]
        [TestCase("Startup origin=process first_frame=900")]
        public void TryParseLast_NoTimeline_ReturnsFalse(string text)
        {
            var parsed = StartupLine.TryParseLast(text, out var launch);

            Assert.That(parsed, Is.False);
            Assert.That(launch, Is.Null);
        }
    }
}