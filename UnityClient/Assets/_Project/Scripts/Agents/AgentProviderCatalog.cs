using System;
using System.Collections.Generic;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Agents
{
    public enum AgentDetectionStrategy
    {
        AutoDetect,
        ManualFolder,
        AutoDetectOrManual
    }

    public enum AgentSupportedStatus
    {
        AutoDetectSupported,
        ManualFolderRequired,
        PartiallySupported,
        UnsupportedOnThisMachine
    }

    [Serializable]
    public sealed class AgentProviderDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AgentProviderType Type { get; set; } = AgentProviderType.Unknown;
        public AgentDetectionStrategy DetectionStrategy { get; set; } = AgentDetectionStrategy.ManualFolder;
        public AgentSupportedStatus SupportedStatus { get; set; } = AgentSupportedStatus.ManualFolderRequired;
        public List<string> DefaultCandidatePathLabels { get; set; } = new List<string>();
    }

    public static class AgentProviderCatalog
    {
        public static IReadOnlyList<AgentProviderDefinition> DefaultProviders { get; } = new List<AgentProviderDefinition>
        {
            Definition(AgentProviderType.Codex, "Codex", AgentDetectionStrategy.AutoDetectOrManual, AgentSupportedStatus.AutoDetectSupported, "Codex local data", "Codex project data"),
            Definition(AgentProviderType.ClaudeCode, "Claude Code", AgentDetectionStrategy.AutoDetectOrManual, AgentSupportedStatus.AutoDetectSupported, "Claude Code local data", "Claude Code project sessions"),
            Definition(AgentProviderType.Cursor, "Cursor", AgentDetectionStrategy.AutoDetectOrManual, AgentSupportedStatus.AutoDetectSupported, "Cursor local data", "Cursor workspace storage"),
            Definition(AgentProviderType.GitHubCopilot, "GitHub Copilot", AgentDetectionStrategy.AutoDetectOrManual, AgentSupportedStatus.PartiallySupported, "VS Code storage", "Cursor storage"),
            Definition(AgentProviderType.GeminiCli, "Gemini CLI", AgentDetectionStrategy.AutoDetectOrManual, AgentSupportedStatus.AutoDetectSupported, "Gemini CLI local data", "Gemini CLI project data"),
            Definition(AgentProviderType.Manual, "Other / Manual Log", AgentDetectionStrategy.ManualFolder, AgentSupportedStatus.ManualFolderRequired, "User-selected folder")
        };

        private static AgentProviderDefinition Definition(
            AgentProviderType type,
            string displayName,
            AgentDetectionStrategy strategy,
            AgentSupportedStatus status,
            params string[] safeCandidatePathLabels)
        {
            return new AgentProviderDefinition
            {
                Id = type.ToString(),
                DisplayName = displayName,
                Type = type,
                DetectionStrategy = strategy,
                SupportedStatus = status,
                DefaultCandidatePathLabels = new List<string>(safeCandidatePathLabels ?? new string[0])
            };
        }
    }
}
