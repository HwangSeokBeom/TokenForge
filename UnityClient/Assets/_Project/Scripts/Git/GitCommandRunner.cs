using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;

namespace TokenForge.Client.Git
{
    public sealed class GitCommandRunner
    {
        private readonly TimeSpan timeout;

        public GitCommandRunner(TimeSpan? timeout = null)
        {
            this.timeout = timeout ?? TimeSpan.FromSeconds(5);
        }

        public async Task<Result<string>> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                return Result<string>.Failure("missing_working_directory", "A project folder is required.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = new Process { StartInfo = startInfo })
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(timeout);

                try
                {
                    if (!process.Start())
                    {
                        return Result<string>.Failure("git_start_failed", "Git process could not be started.");
                    }

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    var exitTask = Task.Run(() =>
                    {
                        process.WaitForExit();
                        return process.ExitCode;
                    }, timeoutCts.Token);

                    var completed = await Task.WhenAny(exitTask, Task.Delay(timeout, timeoutCts.Token));
                    if (completed != exitTask)
                    {
                        TryKill(process);
                        return Result<string>.Failure("git_timeout", "Git command timed out.");
                    }

                    var exitCode = await exitTask;
                    var output = await outputTask;
                    var error = await errorTask;

                    if (exitCode != 0)
                    {
                        return Result<string>.Failure("git_command_failed", string.IsNullOrWhiteSpace(error) ? "Git command failed." : "Git command failed with stderr.");
                    }

                    return Result<string>.Success(output ?? string.Empty);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    return Result<string>.Failure("git_cancelled", "Git command was cancelled.");
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
                {
                    return Result<string>.Failure("git_unavailable", "Git executable is unavailable or cannot be launched.");
                }
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // Nothing useful can be logged here without risking raw command output leakage.
            }
        }
    }
}
