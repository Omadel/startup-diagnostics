using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class DiagnosisReportTests
    {
        [Test]
        public void Sort_Results_PutsWarningsThenInfosThenPassedInTheOrderOfTheSteps()
        {
            var results = new List<DiagnosisResult>
            {
                new() { Check = "passed", Level = DiagnosisLevel.Passed, Step = StartupStep.EngineInitialization },
                new() { Check = "late warning", Level = DiagnosisLevel.Warning, Step = StartupStep.ProjectMilestones },
                new() { Check = "info", Level = DiagnosisLevel.Info, Step = StartupStep.FirstScene },
                new() { Check = "early warning", Level = DiagnosisLevel.Warning, Step = StartupStep.FirstScene },
                new() { Check = "second early warning", Level = DiagnosisLevel.Warning, Step = StartupStep.FirstScene }
            };

            var sorted = DiagnosisReport.Sort(results).Select(result => result.Check);

            Assert.That(sorted, Is.EqualTo(new[] { "early warning", "second early warning", "late warning", "info", "passed" }));
        }

        [Test]
        public void GetSummary_Results_CountsEachLevel()
        {
            var results = new List<DiagnosisResult> { new() { Level = DiagnosisLevel.Warning }, new() { Level = DiagnosisLevel.Info }, new() { Level = DiagnosisLevel.Passed }, new() { Level = DiagnosisLevel.Passed } };

            Assert.That(DiagnosisReport.GetSummary(results), Is.EqualTo("1 warning, 1 info, 2 passed"));
        }

        [Test]
        public void GetSummary_NoWarning_SaysWarningsInThePlural()
        {
            Assert.That(DiagnosisReport.GetSummary(new List<DiagnosisResult>()), Is.EqualTo("0 warnings, 0 info, 0 passed"));
        }

        [Test]
        public void FormatLog_ResultWithItems_ListsThemThenTheGuide()
        {
            var result = new DiagnosisResult { Title = "Splash screen of Unity", Level = DiagnosisLevel.Warning, Message = "Turn it off." };
            result.Items.Add(new DiagnosisItem { Label = "Show Splash Screen", Detail = "On" });
            result.Items.Add(new DiagnosisItem { Label = "Assets/Resources/Big.asset" });

            var log = DiagnosisReport.FormatLog(result, "https://example.com/guide#splash");

            Assert.That(log, Is.EqualTo("[Startup Diagnostics] Splash screen of Unity (warning): Turn it off.\n- Show Splash Screen: On\n- Assets/Resources/Big.asset\nGuide: https://example.com/guide#splash"));
        }

        [Test]
        public void GetStepName_EveryStep_HasANameAndARange()
        {
            foreach (StartupStep step in System.Enum.GetValues(typeof(StartupStep)))
            {
                Assert.That(DiagnosisReport.GetStepName(step), Is.Not.Empty);
                Assert.That(DiagnosisReport.GetStepRange(step), Is.Not.Empty);
            }

            Assert.That(DiagnosisReport.GetStepName(StartupStep.FirstScene), Is.EqualTo("First scene"));
        }

        [Test]
        public void Create_SettingsInMegabytes_GivesBytes()
        {
            var thresholds = DiagnosisThresholds.Create(1.5f, 0.25f, 2048, 5);

            Assert.That(thresholds.PreloadedAndResourcesSize, Is.EqualTo(1572864));
            Assert.That(thresholds.FirstSceneSize, Is.EqualTo(262144));
            Assert.That(thresholds.TextureSize, Is.EqualTo(2048));
            Assert.That(thresholds.ListedAssets, Is.EqualTo(5));
        }

        [Test]
        public void Create_ValuesOutOfRange_AreClamped()
        {
            var thresholds = DiagnosisThresholds.Create(-1, 0, 0, 0);

            Assert.That(thresholds.PreloadedAndResourcesSize, Is.Zero);
            Assert.That(thresholds.FirstSceneSize, Is.Zero);
            Assert.That(thresholds.TextureSize, Is.EqualTo(1));
            Assert.That(thresholds.ListedAssets, Is.EqualTo(1));
        }
    }
}