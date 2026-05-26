using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TokenForge.Client.Git;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
                ErrorCode = string.IsNullOrWhiteSpace(repositoryRootPath) ? "RepositoryPathMissing" : string.Empty,
                ErrorMessage = string.IsNullOrWhiteSpace(repositoryRootPath) ? "Repository path is missing. Reconnect required." : string.Empty
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
                ErrorCode = "NotAGitRepository",
                ErrorMessage = "This folder is not a Git repository."
            };
        }

        public static RepositoryPickerResult Failure(string errorCode, string errorMessage)
        {
            return new RepositoryPickerResult
            {
                ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "Unknown" : errorCode,
                ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Repository selection failed safely." : errorMessage
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
        public async Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

#if UNITY_EDITOR
            var selectedPath = EditorUtility.OpenFolderPanel("Select Git Repository", string.Empty, string.Empty);
            return await GitRepositoryPathValidator.ToPickerResultAsync(selectedPath, cancellationToken);
#elif UNITY_STANDALONE_OSX
            var selectedPath = await MacOSFolderDialog.PickFolderAsync("Select Git Repository", cancellationToken);
            return await GitRepositoryPathValidator.ToPickerResultAsync(selectedPath, cancellationToken);
#else
            return RepositoryPickerResult.Unavailable();
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

            if (!Directory.Exists(selectedPath))
            {
                return RepositoryPickerResult.InvalidGitRepository();
            }

            return Directory.Exists(Path.Combine(selectedPath, ".git")) || File.Exists(Path.Combine(selectedPath, ".git"))
                ? RepositoryPickerResult.Selected(selectedPath)
                : RepositoryPickerResult.InvalidGitRepository();
        }

        public static async Task<RepositoryPickerResult> ToPickerResultAsync(string selectedPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return RepositoryPickerResult.Cancelled();
            }

            var validation = await ResolveRepositoryRootResultAsync(selectedPath, cancellationToken);
            return validation.IsSuccess
                ? RepositoryPickerResult.Selected(validation.Output.Trim())
                : RepositoryPickerResult.Failure(validation.ErrorCode, validation.ErrorMessage);
        }

        public static string ResolveRepositoryRoot(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
            {
                return string.Empty;
            }

            return Directory.Exists(Path.Combine(selectedPath, ".git")) || File.Exists(Path.Combine(selectedPath, ".git"))
                ? selectedPath
                : string.Empty;
        }

        public static async Task<string> ResolveRepositoryRootAsync(string selectedPath, CancellationToken cancellationToken = default)
        {
            var result = await ResolveRepositoryRootResultAsync(selectedPath, cancellationToken);
            return result.IsSuccess ? result.Output.Trim() : string.Empty;
        }

        public static async Task<GitCommandResult> ResolveRepositoryRootResultAsync(string selectedPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return GitCommandResult.Failure("RepositoryPathMissing", "Repository path is missing. Reconnect required.");
            }

            if (!Directory.Exists(selectedPath))
            {
                return GitCommandResult.Failure("RepositoryFolderNotFound", "Repository folder was not found. Reconnect required.");
            }

            try
            {
                Debug.Log("INFO [Repository] validate begin");
                var result = await new SystemGitCommandRunner(TimeSpan.FromSeconds(5))
                    .RunAsync(selectedPath, "rev-parse --show-toplevel", cancellationToken)
                    .ConfigureAwait(false);
                var root = (result.Output ?? string.Empty).Trim();
                if (result.IsSuccess && Directory.Exists(root))
                {
                    Debug.Log("INFO [Repository] validate success");
                    return GitCommandResult.Success(root + "\n", result.ExitCode);
                }

                if (result.IsSuccess)
                {
                    return GitCommandResult.Failure("RepositoryFolderNotFound", "Repository folder was not found. Reconnect required.");
                }

                Debug.LogWarning("WARN [Repository] validate failed reason=" + result.ErrorCode);
                return result;
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("WARN [Repository] validate failed reason=cancelled");
                return GitCommandResult.Failure("ProcessTimeout", "Repository validation timed out.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN [Repository] validate failed reason=" + exception.GetType().Name);
                return GitCommandResult.Failure("Unknown", "Repository validation failed safely.");
            }
        }
    }

    public sealed class MacOSAgentLogLocationPicker : IAgentLogLocationPicker
    {
        public async Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

#if UNITY_EDITOR
            var choice = EditorUtility.DisplayDialogComplex(
                "Select AI Agent Log Location",
                "Select an approved Codex activity log folder or a single supported local log file for this analysis.",
                "Folder",
                "Cancel",
                "File");

            if (choice == 1)
            {
                return AgentLogLocationPickerResult.Cancelled();
            }

            var selectedPath = choice == 2
                ? EditorUtility.OpenFilePanelWithFilters(
                    "Select Codex Log File",
                    string.Empty,
                    new[] { "Agent logs", "json,jsonl,log,txt,ndjson", "All files", "*" })
                : EditorUtility.OpenFolderPanel("Select Codex Log Folder", string.Empty, string.Empty);

            return string.IsNullOrWhiteSpace(selectedPath)
                ? AgentLogLocationPickerResult.Cancelled()
                : AgentLogLocationPickerResult.Selected(selectedPath);
#elif UNITY_STANDALONE_OSX
            var selectedPath = await MacOSFolderDialog.PickFolderAsync("Select AI Agent Log Folder", cancellationToken);
            return string.IsNullOrWhiteSpace(selectedPath)
                ? AgentLogLocationPickerResult.Cancelled()
                : AgentLogLocationPickerResult.Selected(selectedPath);
#else
            return AgentLogLocationPickerResult.Unavailable();
#endif
        }
    }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    internal static class MacOSFolderDialog
    {
        public static Task<string> PickFolderAsync(string prompt, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var buffer = new StringBuilder(4096);
                var selected = NativePickFolder(prompt ?? "Select Folder", buffer, buffer.Capacity);
                var path = selected ? buffer.ToString() : string.Empty;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    Debug.Log("INFO [Repository] path selected=" + SafePathForLog(path));
                }

                return Task.FromResult(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN [Repository] path selection failed reason=" + exception.GetType().Name);
                return Task.FromResult(string.Empty);
            }
        }

        private static string SafePathForLog(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            return value.Length <= 160 ? value : value.Substring(0, 160);
        }

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_PickFolder")]
        private static extern bool NativePickFolder(string prompt, StringBuilder selectedPath, int selectedPathCapacity);
    }
#endif
}
