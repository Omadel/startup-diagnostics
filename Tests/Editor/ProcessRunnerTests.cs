using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class ProcessRunnerTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void JoinArguments_PlainArguments_LeavesThemAsTheyAre(bool windows)
        {
            Assert.That(ProcessRunner.JoinArguments(new[] { "-s", "25271FDF6004NP", "--bundle=/build/Game.aab" }, windows), Is.EqualTo("-s 25271FDF6004NP --bundle=/build/Game.aab"));
        }

        [TestCase("/Users/me/My Game/Game.aab", "\"/Users/me/My Game/Game.aab\"")]
        [TestCase("", "\"\"")]
        [TestCase("say \"hi\"", "\"say \\\"hi\\\"\"")]
        [TestCase("it's", "\"it's\"")]
        [TestCase("a\\b", "\"a\\\\b\"")]
        public void JoinArguments_Unix_QuotesAndEscapesWhatTheShellRulesRead(string argument, string expected)
        {
            Assert.That(ProcessRunner.JoinArguments(new[] { argument }, false), Is.EqualTo(expected));
        }

        [TestCase("C:\\My Game\\Game.aab", "\"C:\\My Game\\Game.aab\"")]
        [TestCase("", "\"\"")]
        [TestCase("say \"hi\"", "\"say \\\"hi\\\"\"")]
        [TestCase("C:\\My Game\\", "\"C:\\My Game\\\\\"")]
        [TestCase("a\\\"b", "\"a\\\\\\\"b\"")]
        [TestCase("C:\\Game\\it's", "C:\\Game\\it's")]
        public void JoinArguments_Windows_QuotesAndEscapesWhatTheWindowsRulesRead(string argument, string expected)
        {
            Assert.That(ProcessRunner.JoinArguments(new[] { argument }, true), Is.EqualTo(expected));
        }

        [Test]
        public void GetLastLines_LongText_KeepsTheLastLines()
        {
            Assert.That(ProcessRunner.GetLastLines("a\nb\nc\nd\n", 2), Is.EqualTo("c\nd"));
        }

        [Test]
        public void GetLastLines_ShortText_KeepsItAll()
        {
            Assert.That(ProcessRunner.GetLastLines("  a\nb  ", 5), Is.EqualTo("a\nb"));
        }
    }
}