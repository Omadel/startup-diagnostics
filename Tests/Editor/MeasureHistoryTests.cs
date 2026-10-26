using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Javienne.StartupDiagnostics.Tests
{
    public class MeasureHistoryTests
    {
        private const string PIXEL = "25271FDF6004NP";
        private const string IPAD = "00008120-000924542E200032";
        private const string GAME = "com.javienne.rouflac";

        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "StartupDiagnosticsTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [Test]
        public void Load_Folder_ReadsTheMeasuresNewestFirstAndLeavesOtherFilesOut()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, "a.json"), JsonUtility.ToJson(CreateResult("2026-10-25T20:14:00.0000000Z", PIXEL)));
            File.WriteAllText(Path.Combine(_folder, "b.json"), JsonUtility.ToJson(CreateResult("2026-10-26T06:56:24.5374430Z", IPAD)));
            File.WriteAllText(Path.Combine(_folder, "broken.json"), "{ not json");
            File.WriteAllText(Path.Combine(_folder, "other.json"), "{ \"Name\" : \"other\" }");

            var files = MeasureHistory.Load(_folder);

            Assert.That(files.Select(file => file.Result.DeviceId), Is.EqualTo(new[] { IPAD, PIXEL }));
            Assert.That(files[0].Path, Does.EndWith("/b.json"));
        }

        [Test]
        public void Load_NoFolder_GivesNothing()
        {
            Assert.That(MeasureHistory.Load(_folder), Is.Empty);
        }

        [Test]
        public void FindPrevious_Measures_IsTheNewestEarlierSuccessOfTheGameOnTheDevice()
        {
            var shown = CreateFile("2026-10-26T09:00:00Z", PIXEL);
            var failed = CreateFile("2026-10-26T08:00:00Z", PIXEL, MeasureStatus.FAILED);
            var otherDevice = CreateFile("2026-10-26T07:30:00Z", IPAD);
            var otherGame = CreateFile("2026-10-26T07:20:00Z", PIXEL);
            otherGame.Result.ApplicationIdentifier = "com.other.game";
            var previous = CreateFile("2026-10-26T07:00:00Z", PIXEL);
            var older = CreateFile("2026-10-25T07:00:00Z", PIXEL);
            var later = CreateFile("2026-10-26T10:00:00Z", PIXEL);
            var files = MeasureHistory.SortNewestFirst(new[] { older, shown, otherDevice, previous, later, failed, otherGame });

            Assert.That(MeasureHistory.FindPrevious(files, shown), Is.SameAs(previous));
        }

        [Test]
        public void FindPrevious_ProfilerCaptureBefore_IsLeftOut()
        {
            var shown = CreateFile("2026-10-26T09:00:00Z", PIXEL);
            var capture = CreateFile("2026-10-26T08:00:00Z", PIXEL);
            capture.Result.ProfilerCapture = true;
            var previous = CreateFile("2026-10-26T07:00:00Z", PIXEL);
            var files = new[] { shown, capture, previous };

            Assert.That(MeasureHistory.FindPrevious(files, shown), Is.SameAs(previous));
        }

        [Test]
        public void FindPrevious_ProfilerCaptureShown_IsNull()
        {
            var shown = CreateFile("2026-10-26T09:00:00Z", PIXEL);
            shown.Result.ProfilerCapture = true;
            var files = new[] { shown, CreateFile("2026-10-26T08:00:00Z", PIXEL) };

            Assert.That(MeasureHistory.FindPrevious(files, shown), Is.Null);
        }

        [Test]
        public void FindPrevious_FirstMeasureOfTheDevice_IsNull()
        {
            var shown = CreateFile("2026-10-26T09:00:00Z", PIXEL);
            var files = new[] { shown, CreateFile("2026-10-26T08:00:00Z", IPAD) };

            Assert.That(MeasureHistory.FindPrevious(files, shown), Is.Null);
        }

        [Test]
        public void FindPrevious_MeasureThatFailedBeforeTheDevice_IsNull()
        {
            var shown = CreateFile("2026-10-26T09:00:00Z", null, MeasureStatus.FAILED);
            var files = new[] { shown, CreateFile("2026-10-26T08:00:00Z", PIXEL) };

            Assert.That(MeasureHistory.FindPrevious(files, shown), Is.Null);
        }

        [Test]
        public void FormatStartedAt_Utc_GivesTheDateAndTimeToTheSecond()
        {
            Assert.That(MeasureHistory.FormatStartedAt(CreateResult("2026-10-26T06:56:24.5374430Z", IPAD), TimeZoneInfo.Utc), Is.EqualTo("2026-10-26 06:56:24"));
        }

        [Test]
        public void GetDuration_Finished_IsTheTimeFromTheStartToTheEnd()
        {
            var result = CreateResult("2026-10-26T06:56:24.5374430Z", IPAD);
            result.FinishedAt = "2026-10-26T07:02:47.8593900Z";

            Assert.That(MeasureHistory.GetDuration(result), Is.EqualTo(TimeSpan.FromTicks(3833219470)));
        }

        [Test]
        public void GetDuration_Running_IsNull()
        {
            Assert.That(MeasureHistory.GetDuration(CreateResult("2026-10-26T06:56:24Z", IPAD)), Is.Null);
        }

        private static MeasureFile CreateFile(string startedAt, string device, string status = MeasureStatus.SUCCEEDED)
        {
            var result = CreateResult(startedAt, device);
            result.Status = status;
            return new MeasureFile(startedAt + ".json", result);
        }

        private static MeasureResult CreateResult(string startedAt, string device)
        {
            return new MeasureResult { Platform = device == IPAD ? "iOS" : "Android", StartedAt = startedAt, DeviceId = device, ApplicationIdentifier = GAME };
        }
    }
}