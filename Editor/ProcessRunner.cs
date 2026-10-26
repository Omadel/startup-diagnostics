using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Runs the command-line tools of the measure (adb, bundletool, git) without blocking the editor.
    /// </summary>
    internal static class ProcessRunner
    {
        private const int ERROR_LINES = 20;

        private static readonly char[] _windowsQuoted = { ' ', '\t', '\n', '"' };
        private static readonly char[] _unixQuoted = { ' ', '\t', '\n', '"', '\'', '\\' };

        private static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

        /// <summary>
        ///     Runs a tool to its end and returns its exit code and output, with environment variables added to those of the
        ///     editor if given. Past the timeout, kills it and throws a <see cref="MeasureException" />; on cancellation,
        ///     kills it and throws an <see cref="OperationCanceledException" />.
        /// </summary>
        public static async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellation, IReadOnlyDictionary<string, string> environment = null)
        {
            cancellation.ThrowIfCancellationRequested();
            var info = new ProcessStartInfo(fileName, JoinArguments(arguments, IsWindows)) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            if (environment != null)
            {
                foreach (var variable in environment)
                {
                    info.EnvironmentVariables[variable.Key] = variable.Value;
                }
            }

            using var process = new Process { StartInfo = info, EnableRaisingEvents = true };
            var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            process.Exited += (_, _) => exited.TrySetResult(true);
            try
            {
                process.Start();
            }
            catch (Win32Exception exception)
            {
                throw new MeasureException($"Unable to run {fileName}: {exception.Message}");
            }

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var completion = Task.WhenAll(exited.Task, output, error);
            var finished = await Task.WhenAny(completion, Task.Delay(timeout, waiting.Token));
            waiting.Cancel();
            if (finished != completion)
            {
                Kill(process);
                cancellation.ThrowIfCancellationRequested();
                throw new MeasureException($"{Describe(fileName, arguments)} did not end within {timeout.TotalSeconds:0} s.");
            }

            return new ProcessResult(process.ExitCode, output.Result, error.Result);
        }

        /// <summary>
        ///     Runs a tool to its end and returns its output, or throws a <see cref="MeasureException" /> with the end of
        ///     what it wrote if it fails.
        /// </summary>
        public static async Task<string> RunCheckedAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellation)
        {
            var result = await RunAsync(fileName, arguments, timeout, cancellation);
            if (result.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                throw new MeasureException($"{Describe(fileName, arguments)} failed ({result.ExitCode}):\n{GetLastLines(message, ERROR_LINES)}");
            }

            return result.Output;
        }

        /// <summary>
        ///     The command line of the arguments, each one quoted when needed so that the tool reads it back as one argument:
        ///     under the rules of Windows, or under the shell-like rules that Mono follows on macOS and Linux, where a
        ///     backslash escapes the next character.
        /// </summary>
        internal static string JoinArguments(IReadOnlyList<string> arguments, bool windows)
        {
            var line = new StringBuilder();
            foreach (var argument in arguments)
            {
                if (line.Length > 0)
                {
                    line.Append(' ');
                }

                if (argument.Length > 0 && argument.IndexOfAny(windows ? _windowsQuoted : _unixQuoted) < 0)
                {
                    line.Append(argument);
                    continue;
                }

                line.Append('"');
                if (windows)
                {
                    AppendWindowsQuoted(line, argument);
                }
                else
                {
                    AppendUnixQuoted(line, argument);
                }

                line.Append('"');
            }

            return line.ToString();
        }

        /// <summary>
        ///     The last lines of the text, trimmed.
        /// </summary>
        internal static string GetLastLines(string text, int count)
        {
            var lines = text.Trim().Split('\n');
            return string.Join("\n", lines.Skip(Math.Max(0, lines.Length - count))).Trim();
        }

        /// <summary>
        ///     Backslashes are literal, except before a quote: those are doubled, and the quote escaped.
        /// </summary>
        private static void AppendWindowsQuoted(StringBuilder line, string argument)
        {
            var backslashes = 0;
            foreach (var character in argument)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                line.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes).Append(character);
                backslashes = 0;
            }

            line.Append('\\', backslashes * 2);
        }

        private static void AppendUnixQuoted(StringBuilder line, string argument)
        {
            foreach (var character in argument)
            {
                if (character == '\\' || character == '"')
                {
                    line.Append('\\');
                }

                line.Append(character);
            }
        }

        private static string Describe(string fileName, IReadOnlyList<string> arguments)
        {
            return string.Join(" ", new[] { Path.GetFileName(fileName) }.Concat(arguments.Take(3)));
        }

        private static void Kill(Process process)
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
                // Already ended.
            }
        }
    }
}