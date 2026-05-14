using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;

namespace TokenForge.Client.Git
{
    public interface IGitCommandRunner
    {
        Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken);
    }

    public sealed class GitCommandResult
    {
        public bool IsSuccess { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public static GitCommandResult Success(string output, int exitCode = 0)
        {
            return new GitCommandResult
            {
                IsSuccess = true,
                ExitCode = exitCode,
                Output = output ?? string.Empty
            };
        }

        public static GitCommandResult Failure(string errorCode, string errorMessage, int exitCode = -1)
        {
            return new GitCommandResult
            {
                IsSuccess = false,
                ExitCode = exitCode,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }

    public sealed class SystemGitCommandRunner : IGitCommandRunner
    {
        private readonly TimeSpan timeout;

        public SystemGitCommandRunner(TimeSpan? timeout = null)
        {
            this.timeout = timeout ?? TimeSpan.FromSeconds(5);
        }

        public async Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                return GitCommandResult.Failure("missing_working_directory", "A project folder is required.");
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
                        return GitCommandResult.Failure("git_start_failed", "Git process could not be started.");
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
                        return GitCommandResult.Failure("git_timeout", "Git command timed out.");
                    }

                    var exitCode = await exitTask;
                    var output = await outputTask;
                    await errorTask;

                    if (exitCode != 0)
                    {
                        return GitCommandResult.Failure("git_command_failed", "Git command failed with stderr.", exitCode);
                    }

                    return GitCommandResult.Success(output, exitCode);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    return GitCommandResult.Failure("git_cancelled", "Git command was cancelled.");
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
                {
                    return GitCommandResult.Failure("git_unavailable", "Git executable is unavailable or cannot be launched.");
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

    public sealed class GitCommandRunner
    {
        private readonly IGitCommandRunner inner;

        public GitCommandRunner(TimeSpan? timeout = null)
        {
            inner = new SystemGitCommandRunner(timeout);
        }

        public async Task<Result<string>> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
        {
            var result = await inner.RunAsync(workingDirectory, arguments, cancellationToken);
            if (!result.IsSuccess)
            {
                return Result<string>.Failure(result.ErrorCode, result.ErrorMessage);
            }

            return Result<string>.Success(result.Output ?? string.Empty);
        }
    }
}
