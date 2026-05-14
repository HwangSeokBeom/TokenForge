using System.Threading;
using System.Threading.Tasks;

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
            return Task.FromResult(string.IsNullOrWhiteSpace(selectedPath)
                ? RepositoryPickerResult.Cancelled()
                : RepositoryPickerResult.Selected(selectedPath));
#else
            return Task.FromResult(RepositoryPickerResult.Unavailable());
#endif
        }
    }

    public sealed class MacOSAgentLogLocationPicker : IAgentLogLocationPicker
    {
        public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

#if UNITY_EDITOR
            var choice = EditorUtility.DisplayDialogComplex(
                "Select AI Agent Log Location",
                "Select an approved AI agent log folder or a single log file for this analysis.",
                "Folder",
                "Cancel",
                "File");

            if (choice == 1)
            {
                return Task.FromResult(AgentLogLocationPickerResult.Cancelled());
            }

            var selectedPath = choice == 2
                ? EditorUtility.OpenFilePanelWithFilters(
                    "Select AI Agent Log File",
                    string.Empty,
                    new[] { "Agent logs", "json,jsonl,log,txt,ndjson", "All files", "*" })
                : EditorUtility.OpenFolderPanel("Select AI Agent Log Folder", string.Empty, string.Empty);

            return Task.FromResult(string.IsNullOrWhiteSpace(selectedPath)
                ? AgentLogLocationPickerResult.Cancelled()
                : AgentLogLocationPickerResult.Selected(selectedPath));
#else
            return Task.FromResult(AgentLogLocationPickerResult.Unavailable());
#endif
        }
    }
}
