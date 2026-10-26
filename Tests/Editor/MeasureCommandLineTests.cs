using NUnit.Framework;
using UnityEditor;

namespace Javienne.StartupDiagnostics.Tests
{
    public class MeasureCommandLineTests
    {
        [Test]
        public void Parse_NoOption_MeasuresTheActiveTargetWithTheDefaults()
        {
            var settings = MeasureCommandLine.Parse(new[] { "Unity", "-batchmode", "-projectPath", "/project" }, BuildTarget.iOS);

            Assert.That(settings.Platform, Is.EqualTo(BuildTarget.iOS));
            Assert.That(settings.Runs, Is.EqualTo(MeasureSettings.DEFAULT_RUNS));
            Assert.That(settings.Rebuild, Is.True);
            Assert.That(settings.CleanInstall, Is.True);
            Assert.That(settings.Device, Is.Null);
            Assert.That(settings.BuildPath, Is.Null);
        }

        [Test]
        public void Parse_Options_SetsEachOne()
        {
            var arguments = new[] { "-startupPlatform", "Android", "-startupDevice", "25271FDF6004NP", "-startupRuns", "3", "-startupBuild", "Build/Game.aab", "-startupNoBuild", "-startupNoInstall" };

            var settings = MeasureCommandLine.Parse(arguments, BuildTarget.iOS);

            Assert.That(settings.Platform, Is.EqualTo(BuildTarget.Android));
            Assert.That(settings.Device, Is.EqualTo("25271FDF6004NP"));
            Assert.That(settings.Runs, Is.EqualTo(3));
            Assert.That(settings.BuildPath, Is.EqualTo("Build/Game.aab"));
            Assert.That(settings.Rebuild, Is.False);
            Assert.That(settings.CleanInstall, Is.False);
        }

        [Test]
        public void Parse_ProfilerCapture_CapturesForTheSeconds()
        {
            var settings = MeasureCommandLine.Parse(new[] { "-startupProfilerCapture", "-startupProfilerSeconds", "4.5" }, BuildTarget.Android);

            Assert.That(settings.ProfilerCapture, Is.True);
            Assert.That(settings.ProfilerSeconds, Is.EqualTo(4.5f));
        }

        [Test]
        public void Parse_NoProfilerCapture_TimesColdLaunchesWithTheDefaultSeconds()
        {
            var settings = MeasureCommandLine.Parse(new string[0], BuildTarget.Android);

            Assert.That(settings.ProfilerCapture, Is.False);
            Assert.That(settings.ProfilerSeconds, Is.EqualTo(MeasureSettings.DEFAULT_PROFILER_SECONDS));
        }

        [Test]
        public void Parse_NoInstall_MeasuresTheGameOnTheDeviceWithoutBuilding()
        {
            var settings = MeasureCommandLine.Parse(new[] { "-startupNoInstall" }, BuildTarget.Android);

            Assert.That(settings.CleanInstall, Is.False);
            Assert.That(settings.Rebuild, Is.False);
        }

        [TestCase("ios", BuildTarget.iOS)]
        [TestCase("IOS", BuildTarget.iOS)]
        [TestCase("android", BuildTarget.Android)]
        public void Parse_Platform_IgnoresTheCase(string value, BuildTarget expected)
        {
            Assert.That(MeasureCommandLine.Parse(new[] { "-startupPlatform", value }, BuildTarget.StandaloneOSX).Platform, Is.EqualTo(expected));
        }

        [TestCase("-startupPlatform", "windows")]
        [TestCase("-startupRuns", "0")]
        [TestCase("-startupRuns", "ten")]
        [TestCase("-startupProfilerSeconds", "0")]
        [TestCase("-startupProfilerSeconds", "6s")]
        public void Parse_InvalidValue_Throws(string option, string value)
        {
            Assert.That(() => MeasureCommandLine.Parse(new[] { option, value }, BuildTarget.Android), Throws.TypeOf<MeasureException>().With.Message.StartsWith(option));
        }

        [Test]
        public void Parse_LastOptionWithoutValue_Throws()
        {
            Assert.That(() => MeasureCommandLine.Parse(new[] { "-startupRuns" }, BuildTarget.Android), Throws.TypeOf<MeasureException>().With.Message.Contains("needs a value"));
        }

        [Test]
        public void Parse_OptionFollowedByAnother_Throws()
        {
            Assert.That(() => MeasureCommandLine.Parse(new[] { "-startupDevice", "-startupNoInstall" }, BuildTarget.Android), Throws.TypeOf<MeasureException>().With.Message.Contains("needs a value"));
        }
    }
}