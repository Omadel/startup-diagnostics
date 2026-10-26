using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class StartupProfilerCaptureTests
    {
        [TestCase("6", 6f)]
        [TestCase(" 2.5\n", 2.5f)]
        public void ParseSeconds_Number_GivesIt(string request, float expected)
        {
            Assert.That(StartupProfilerCapture.ParseSeconds(request), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0")]
        [TestCase("-3")]
        [TestCase("six")]
        [TestCase("Infinity")]
        [TestCase("NaN")]
        public void ParseSeconds_NoValidNumber_GivesTheDefault(string request)
        {
            Assert.That(StartupProfilerCapture.ParseSeconds(request), Is.EqualTo(StartupProfilerCapture.DEFAULT_SECONDS));
        }

        [Test]
        public void CaptureFile_IsTheLogOfTheProfilerWithItsExtension()
        {
            Assert.That(StartupProfilerCapture.CAPTURE_FILE, Is.EqualTo("startup-profiler.raw"));
        }
    }
}