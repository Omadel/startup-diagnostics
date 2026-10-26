using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.Scripting;

namespace Javienne.StartupDiagnostics.Tests
{
    public class StartupTimelineTests
    {
        private const double TOLERANCE = 1e-9;

        [Test]
        public void FormatTimeline_Milestones_ListsThemInOrderInMillisecondsSinceTheOrigin()
        {
            var milestones = new List<KeyValuePair<string, double>>
            {
                new(StartupTimeline.SCRIPTS_START, 0.1),
                new(StartupTimeline.FIRST_SCENE_LOADED, 0.25),
                new(StartupTimeline.FIRST_FRAME, 0.3),
                new("addressables_ready", 0.45),
                new("scene_loaded", 1.9),
                new("first_scene_frame", 1.925)
            };

            var line = StartupTimeline.FormatTimeline(StartupTimeline.ORIGIN_PROCESS, -0.5, milestones);

            Assert.That(line, Is.EqualTo("[Startup] origin=process scripts_start=600 first_scene_loaded=750 first_frame=800 addressables_ready=950 scene_loaded=2400 first_scene_frame=2425"));
        }

        [TestCase(0.0004, "0")]
        [TestCase(0.0005, "1")]
        [TestCase(1.2346, "1235")]
        [TestCase(12.0, "12000")]
        public void FormatTimeline_Time_RoundsToTheMillisecond(double time, string expected)
        {
            var milestones = new List<KeyValuePair<string, double>> { new(StartupTimeline.FIRST_FRAME, time) };

            var line = StartupTimeline.FormatTimeline(StartupTimeline.ORIGIN_ENGINE, 0, milestones);

            Assert.That(line, Is.EqualTo($"[Startup] origin=engine first_frame={expected}"));
        }

        [Test]
        public void FormatTimeline_NoMilestone_GivesTheOriginOnly()
        {
            var line = StartupTimeline.FormatTimeline(StartupTimeline.ORIGIN_PLAY_MODE, 3, new List<KeyValuePair<string, double>>());

            Assert.That(line, Is.EqualTo("[Startup] origin=play_mode"));
        }

        [TestCase(0.2, 0.7, -0.5)]
        [TestCase(10, 0.5, 9.5)]
        [TestCase(1, 0, 1)]
        public void GetProcessStart_NowAndProcessAge_ReturnsTheStartOnTheSameClock(double now, double processAge, double expected)
        {
            Assert.That(StartupTimeline.GetProcessStart(now, processAge), Is.EqualTo(expected).Within(TOLERANCE));
        }

        [Test]
        public void RuntimeAssembly_IsAlwaysLinked()
        {
            var assembly = typeof(StartupTimeline).Assembly;

            Assert.That(assembly.IsDefined(typeof(AlwaysLinkAssemblyAttribute), false), Is.True);
        }
    }
}