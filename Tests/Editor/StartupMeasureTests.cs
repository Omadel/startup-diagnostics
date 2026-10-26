using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class StartupMeasureTests
    {
        [Test]
        public void OpenProfilerCapture_ThisUnity_LoadsAsTheLoadButtonOfTheProfilerWindow()
        {
            var load = typeof(StartupMeasure).GetField("_loadProfilingData", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(load.GetValue(null), Is.Not.Null);
        }

        [Test]
        public void OpenProfilerCapture_NoFile_IsFalse()
        {
            Assert.That(StartupMeasure.OpenProfilerCapture(Path.Combine(Path.GetTempPath(), "missing-startup-profiler.raw")), Is.False);
            Assert.That(StartupMeasure.OpenProfilerCapture(null), Is.False);
        }
    }
}