using System.Threading;
using System.Threading.Tasks;
using System.IO;

using System.Diagnostics;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TokenForge.Client.Platform
{
    public interface IRepositoryPicker
    {
        Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default);
    }

    public interface IAgentLogLocationPicker
    {
        Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default);
    }

    public sealed class RepositoryPickerResult
    {
        public bool IsSuccess { get; private set; }
        public bool IsCancelled { get; private set; }
        public string RepositoryRootPath { get; private set; } = string.Empty;
        public string ErrorCode { get; private set; } = string.Empty;
        public string ErrorMessage { get; private set; } = string.Empty;

        public static RepositoryPickerResult Selected(string repositoryRootPath)
        {
            return new RepositoryPickerResult
            {
                IsSuccess = !string.IsNullOrWhiteSpace(repositoryRootPath),
                RepositoryRootPath = repositoryRootPath ?? string.Empty,
                ErrorCode = string.IsNullOrWhiteSpace(repositoryRootPath) ? "missing_repository_path" : string.Empty,
                ErrorMessage = string.IsNullOrWhiteSpace(repositoryRootPath) ? "Repository folder is required." : string.Empty
            };
        }

        public static RepositoryPickerResult Cancelled()
        {
            return new RepositoryPickerResult
            {
                IsCancelled = true,
                ErrorCode = "repository_selection_cancelled",
                ErrorMessage = "Repository selection was cancelled."
            };
        }

        public static RepositoryPickerResult Unavailable()
        {
            return new RepositoryPickerResult
            {
                ErrorCode = "repository_picker_unavailable",
                ErrorMessage = "Repository picker is not available on this platform."
            };
        }

        public static RepositoryPickerResult InvalidGitRepository()
        {
            return new RepositoryPickerResult
            {
                ErrorCode = "not_git_repository",
                ErrorMessage = "This folder is not a Git repository."
            };
        }
    }

    public sealed class AgentLogLocationPickerResult
    {
        public bool IsSuccess { get; private set; }
        public bool IsCancelled { get; private set; }
        public string AgentLogLocationPath { get; private set; } = string.Empty;
        public string ErrorCode { get; private set; } = string.Empty;
        public string ErrorMessage { get; private set; } = string.Empty;

        public static AgentLogLocationPickerResult Selected(string agentLogLocationPath)
        {
            return new AgentLogLocationPickerResult
            {
                IsSuccess = !string.IsNullOrWhiteSpace(agentLogLocationPath),
                AgentLogLocationPath = agentLogLocationPath ?? string.Empty,
                ErrorCode = string.IsNullOrWhiteSpace(agentLogLocationPath) ? "missing_agent_log_location" : string.Empty,
                ErrorMessage = string.IsNullOrWhiteSpace(agentLogLocationPath) ? "Agent log location is required." : string.Empty
            };
        }

        public static AgentLogLocationPickerResult Cancelled()
        {
            return new AgentLogLocationPickerResult
            {
                IsCancelled = true,
                ErrorCode = "agent_log_selection_cancelled",
                ErrorMessage = "Agent log selection was cancelled."
            };
        }

        public static AgentLogLocationPickerResult Unavailable()
        {
            return new AgentLogLocationPickerResult
            {
                ErrorCode = "agent_log_picker_unavailable",
                ErrorMessage = "Agent log picker is not available on this platform."
            };
        }
    }

    public sealed class MacOSRepositoryPicker : IRepositoryPicker
    {
        public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

#if UNITY_EDITOR
            var selectedPath = EditorUtility.OpenFolderPanel("Select Git Repository", string.Empty, string.Empty);
            return Task.FromResult(GitRepositoryPathValidator.ToPickerResult(selectedPath));
#elif UNITY_STANDALONE_OSX
            var selectedPath = MacOSFolderDialog.PickFolder("Select Git Repository");
            return Task.FromResult(GitRepositoryPathValidator.ToPickerResult(selectedPath));
#else
            return Task.FromResult(RepositoryPickerResult.Unavailable());
#endif
        }
    }

    public static class GitRepositoryPathValidator
    {
        public static RepositoryPickerResult ToPickerResult(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return RepositoryPickerResult.Cancelled();
            }

            var root = ResolveRepositoryRoot(selectedPath);
            return string.IsNullOrWhiteSpace(root)
                ? RepositoryPickerResult.InvalidGitRepository()
                : RepositoryPickerResult.Selected(root);
        }

        public static string ResolveRepositoryRoot(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
            {
                return string.Empty;
            }

            if (!Directory.Exists(Path.Combine(selectedPath, ".git")) && !File.Exists(Path.Combine(selectedPath, ".git")))
            {
                return string.Empty;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --show-toplevel",
                    WorkingDirectory = selectedPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return string.Empty;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(5000) || process.ExitCode != 0)
                    {
                        return string.Empty;
                    }

                    var root = (output ?? string.Empty).Trim();
                    return Directory.Exists(root) ? root : string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public sealed class MacOSAgentLogLocationPicker : IAgentLogLocationPicker
    {
        public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

#if UNITY_EDITOR
            var choice = EditorUtility.DisplayDialogComplex(
                "Select Codex Agent Log Location",
                "Select an approved Codex activity log folder or a single supported local log file for this analysis.",
                "Folder",
                "Cancel",
                "File");

            if (choice == 1)
            {
                return Task.FromResult(AgentLogLocationPickerResult.Cancelled());
            }

            var selectedPath = choice == 2
                ? EditorUtility.OpenFilePanelWithFilters(
                    "Select Codex Log File",
                    string.Empty,
                    new[] { "Agent logs", "json,jsonl,log,txt,ndjson", "All files", "*" })
                : EditorUtility.OpenFolderPanel("Select Codex Log Folder", string.Empty, string.Empty);

            return Task.FromResult(string.IsNullOrWhiteSpace(selectedPath)
                ? AgentLogLocationPickerResult.Cancelled()
                : AgentLogLocationPickerResult.Selected(selectedPath));
#elif UNITY_STANDALONE_OSX
            var selectedPath = MacOSFolderDialog.PickFolder("Select Codex log folder");
            return Task.FromResult(string.IsNullOrWhiteSpace(selectedPath)
                ? AgentLogLocationPickerResult.Cancelled()
                : AgentLogLocationPickerResult.Selected(selectedPath));
#else
            return Task.FromResult(AgentLogLocationPickerResult.Unavailable());
#endif
        }
    }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    internal static class MacOSFolderDialog
    {
        public static string PickFolder(string prompt)
        {
            try
            {
                var script = "POSIX path of (choose folder with prompt " + Quote(prompt) + ")";
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/osascript",
                    Arguments = "-e " + Quote(script),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return string.Empty;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(120000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // Best effort cleanup; selection simply fails closed.
                        }

                        return string.Empty;
                    }

                    if (process.ExitCode != 0)
                    {
                        return string.Empty;
                    }

                    return (output ?? string.Empty).Trim();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
#endif
}
