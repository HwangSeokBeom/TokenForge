using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
        public string OutputSummary { get; set; } = string.Empty;
        public string ErrorSummary { get; set; } = string.Empty;
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

        public static GitCommandResult Failure(string errorCode, string errorMessage, int exitCode = -1, string outputSummary = "", string errorSummary = "")
        {
            return new GitCommandResult
            {
                IsSuccess = false,
                ExitCode = exitCode,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                OutputSummary = outputSummary ?? string.Empty,
                ErrorSummary = errorSummary ?? string.Empty
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
                return GitCommandResult.Failure("RepositoryPathMissing", "Repository path is missing. Reconnect required.");
            }

            if (!Directory.Exists(workingDirectory))
            {
                return GitCommandResult.Failure("RepositoryFolderNotFound", "Repository folder was not found. Reconnect required.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveGitExecutable(),
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
            {
                try
                {
                    Debug.Log("INFO [Repository] git command begin command=git " + SafeCommandForLog(arguments));
                    if (!process.Start())
                    {
                        return GitCommandResult.Failure("git_start_failed", "Git process could not be started.");
                    }

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    var exitTask = WaitForExitAsync(process, cancellationToken);
                    var timeoutTask = Task.Delay(timeout);

                    var completed = await Task.WhenAny(exitTask, timeoutTask);
                    if (completed == timeoutTask)
                    {
                        TryKill(process);
                        Debug.LogWarning("WARN [Repository] git command timeout command=git " + SafeCommandForLog(arguments));
                        return GitCommandResult.Failure("ProcessTimeout", "Git command timed out.");
                    }

                    var exitCode = await exitTask;
                    var output = await outputTask;
                    var error = await errorTask;
                    Debug.Log("INFO [Repository] git command end exitCode=" + exitCode);

                    if (exitCode != 0)
                    {
                        var category = CategoryForFailedGitCommand(arguments, error);
                        Debug.LogWarning("WARN [Repository] git command failed exitCode=" + exitCode + " category=" + category);
                        return GitCommandResult.Failure(
                            category,
                            MessageForCategory(category),
                            exitCode,
                            SafeCommandForLog(output),
                            SafeCommandForLog(error));
                    }

                    return GitCommandResult.Success(output, exitCode);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    Debug.LogWarning("WARN [Repository] git command timeout command=git " + SafeCommandForLog(arguments));
                    return GitCommandResult.Failure("ProcessTimeout", "Git command timed out.");
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.LogWarning("WARN [Repository] git command failed reason=permission_denied");
                    return GitCommandResult.Failure("PermissionDenied", "Permission denied while reading repository folder.");
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
                {
                    Debug.LogWarning("WARN [Repository] git command failed reason=" + ex.GetType().Name);
                    return GitCommandResult.Failure("GitExecutableNotFound", "Git executable was not found. Install Xcode Command Line Tools or Git.");
                }
            }
        }

        private static string ResolveGitExecutable()
        {
            var candidates = new[]
            {
                "/usr/bin/git",
                "/opt/homebrew/bin/git",
                "/usr/local/bin/git"
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return "git";
        }

        private static Task<int> WaitForExitAsync(Process process, CancellationToken cancellationToken)
        {
            if (process.HasExited)
            {
                return Task.FromResult(process.ExitCode);
            }

            var completion = new TaskCompletionSource<int>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) =>
            {
                try
                {
                    completion.TrySetResult(process.ExitCode);
                }
                catch (InvalidOperationException exception)
                {
                    completion.TrySetException(exception);
                }
            };

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => completion.TrySetCanceled());
            }

            if (process.HasExited)
            {
                completion.TrySetResult(process.ExitCode);
            }

            return completion.Task;
        }

        private static string SafeCommandForLog(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();
            value = value.Replace('\n', ' ').Replace('\r', ' ');
            return value.Length <= 120 ? value : value.Substring(0, 120);
        }

        private static string CategoryForFailedGitCommand(string arguments, string error)
        {
            var command = arguments ?? string.Empty;
            var stderr = (error ?? string.Empty).ToLowerInvariant();
            if (command.IndexOf("rev-parse", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (stderr.Contains("not a git repository") || stderr.Contains("not a git repo") || stderr.Contains("fatal: not a git")))
            {
                return "NotAGitRepository";
            }

            if (stderr.Contains("permission denied") || stderr.Contains("operation not permitted"))
            {
                return "PermissionDenied";
            }

            if (stderr.Contains("xcrun") || stderr.Contains("command line tools") || stderr.Contains("unable to find utility"))
            {
                return "GitExecutableNotFound";
            }

            return "GitCommandFailed";
        }

        private static string MessageForCategory(string category)
        {
            switch (category)
            {
                case "NotAGitRepository": return "This folder is not a Git repository.";
                case "GitExecutableNotFound": return "Git executable was not found. Install Xcode Command Line Tools or Git.";
                case "PermissionDenied": return "Permission denied while reading repository folder.";
                case "ProcessTimeout": return "Git command timed out.";
                case "RepositoryFolderNotFound": return "Repository folder was not found. Reconnect required.";
                case "RepositoryPathMissing": return "Repository path is missing. Reconnect required.";
                default: return "Git command failed. Check repository state and try again.";
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
