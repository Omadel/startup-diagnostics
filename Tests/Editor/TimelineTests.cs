using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class TimelineTests
    {
        [Test]
        public void MarkFirstFrame_EndNotDeferred_EndsTheStartAtTheFirstFrame()
        {
            var timeline = new Timeline();
            timeline.Mark(StartupTimeline.SCRIPTS_START, 0.1);
            timeline.Mark(StartupTimeline.FIRST_SCENE_LOADED, 0.2);

            var isEnded = timeline.MarkFirstFrame(0.3);

            Assert.That(isEnded, Is.True);
            Assert.That(timeline.IsEnded, Is.True);
            Assert.That(Names(timeline), Is.EqualTo(new[] { StartupTimeline.SCRIPTS_START, StartupTimeline.FIRST_SCENE_LOADED, StartupTimeline.FIRST_FRAME }));
        }

        [Test]
        public void MarkFirstFrame_EndDeferred_RecordsTheFrameAndGoesOn()
        {
            var timeline = new Timeline();
            timeline.DeferEnd();

            var isEnded = timeline.MarkFirstFrame(0.3);

            Assert.That(isEnded, Is.False);
            Assert.That(timeline.IsEnded, Is.False);
            Assert.That(Names(timeline), Is.EqualTo(new[] { StartupTimeline.FIRST_FRAME }));
        }

        [Test]
        public void End_EndDeferred_EndsTheStartWithItsMilestoneLast()
        {
            var timeline = new Timeline();
            timeline.DeferEnd();
            timeline.MarkFirstFrame(0.3);
            timeline.Mark("scene_loaded", 1.9);

            var isEnded = timeline.End("first_scene_frame", 1.925);

            Assert.That(isEnded, Is.True);
            Assert.That(timeline.IsEnded, Is.True);
            Assert.That(timeline.Milestones.Last(), Is.EqualTo(new KeyValuePair<string, double>("first_scene_frame", 1.925)));
        }

        [Test]
        public void End_AlreadyEnded_ReturnsFalseAndRecordsNothing()
        {
            var timeline = new Timeline();
            timeline.MarkFirstFrame(0.3);

            var isEnded = timeline.End("first_scene_frame", 1.925);

            Assert.That(isEnded, Is.False);
            Assert.That(Names(timeline), Is.EqualTo(new[] { StartupTimeline.FIRST_FRAME }));
        }

        [Test]
        public void Mark_AfterTheEnd_IsLeftOut()
        {
            var timeline = new Timeline();
            timeline.End("menu", 0.5);

            timeline.Mark("later", 0.6);

            Assert.That(Names(timeline), Is.EqualTo(new[] { "menu" }));
        }

        [Test]
        public void MarkFirstFrame_EndedBeforeTheFirstFrame_RecordsNothing()
        {
            var timeline = new Timeline();
            timeline.DeferEnd();
            timeline.End("menu", 0.2);

            var isEnded = timeline.MarkFirstFrame(0.3);

            Assert.That(isEnded, Is.False);
            Assert.That(Names(timeline), Is.EqualTo(new[] { "menu" }));
        }

        [Test]
        public void DeferEnd_AfterTheEnd_HasNoEffect()
        {
            var timeline = new Timeline();
            timeline.MarkFirstFrame(0.3);

            timeline.DeferEnd();

            Assert.That(timeline.IsEndDeferred, Is.False);
        }

        private static IEnumerable<string> Names(Timeline timeline)
        {
            return timeline.Milestones.Select(milestone => milestone.Key);
        }
    }
}