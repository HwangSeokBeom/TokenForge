using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Agents
{
    public enum AgentSourceAccessState
    {
        Unknown,
        NotDetected,
        Detected,
        PermissionRequired,
        ManualImportRequired,
        LimitedSupport,
        FailedSafely
    }

    public sealed class AgentSourceCandidate
    {
        // Memory-only handle. Never render, persist, sync, or log this value.
        public string LocalPath { get; set; } = string.Empty;
        public string SafeAlias { get; set; } = string.Empty;
        public AgentSourceKind SourceKind { get; set; } = AgentSourceKind.DetectedLocal;
        public CountBucket FileCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket SizeBucket { get; set; } = CountBucket.Unknown;
        public DateTimeOffset? LastModifiedUtc { get; set; }
        public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Unknown;
        public AgentSourceAccessState AccessState { get; set; } = AgentSourceAccessState.Unknown;
        public List<string> WarningIds { get; set; } = new List<string>();
    }

    public sealed class AgentSourceDetectionResult
    {
        public AgentProviderType ProviderType { get; set; } = AgentProviderType.Unknown;
        public AgentSourceAccessState AccessState { get; set; } = AgentSourceAccessState.Unknown;
        public List<AgentSourceCandidate> Candidates { get; set; } = new List<AgentSourceCandidate>();
        public List<string> WarningIds { get; set; } = new List<string>();
        public DateTimeOffset ScannedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public bool HasUsableCandidate => Candidates != null && Candidates.Any(candidate => candidate.AccessState == AgentSourceAccessState.Detected || candidate.AccessState == AgentSourceAccessState.LimitedSupport);

        public AgentSourceCandidate BestCandidate()
        {
            return (Candidates ?? new List<AgentSourceCandidate>())
                .OrderByDescending(candidate => candidate.Confidence)
                .ThenByDescending(candidate => candidate.LastModifiedUtc ?? DateTimeOffset.MinValue)
                .FirstOrDefault();
        }
    }

    public interface IAgentSourceDetector
    {
        AgentProviderType ProviderType { get; }
        Task<AgentSourceDetectionResult> DetectAsync(CancellationToken cancellationToken);
    }

    public sealed class MacAgentSourceDetector : IAgentSourceDetector
    {
        private const int MaxProbeDepth = 2;
        private const int MaxFilesForBucket = 128;
        private const long MaxSizeForBucketBytes = 128L * 1024L * 1024L;

        private readonly AgentProviderType providerType;
        private readonly string homeDirectory;
        private readonly string projectDirectory;

        public MacAgentSourceDetector(AgentProviderType providerType, string homeDirectory = null, string projectDirectory = null)
        {
            this.providerType = NormalizeProvider(providerType);
            this.homeDirectory = string.IsNullOrWhiteSpace(homeDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : homeDirectory;
            this.projectDirectory = string.IsNullOrWhiteSpace(projectDirectory)
                ? Directory.GetCurrentDirectory()
                : projectDirectory;
        }

        public AgentProviderType ProviderType => providerType;

        public Task<AgentSourceDetectionResult> DetectAsync(CancellationToken cancellationToken)
        {
            var result = new AgentSourceDetectionResult
            {
                ProviderType = providerType,
                AccessState = AgentSourceAccessState.NotDetected,
                ScannedAtUtc = DateTimeOffset.UtcNow
            };

            if (providerType == AgentProviderType.Manual || providerType == AgentProviderType.Unknown)
            {
                result.AccessState = AgentSourceAccessState.ManualImportRequired;
                result.WarningIds.Add("agent_source_manual_import_required");
                return Task.FromResult(result);
            }

            foreach (var candidate in CandidateSpecs(providerType))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Expand(candidate.RelativePath);
                var probe = Probe(path, candidate.SafeAlias, providerType == AgentProviderType.GitHubCopilot, cancellationToken);
                if (probe != null)
                {
                    result.Candidates.Add(probe);
                }
            }

            if (result.Candidates.Any(candidate => candidate.AccessState == AgentSourceAccessState.PermissionRequired))
            {
                result.AccessState = AgentSourceAccessState.PermissionRequired;
                result.WarningIds.Add("agent_source_permission_required");
            }
            else if (result.Candidates.Any(candidate => candidate.AccessState == AgentSourceAccessState.Detected || candidate.AccessState == AgentSourceAccessState.LimitedSupport))
            {
                result.AccessState = providerType == AgentProviderType.GitHubCopilot
                    ? AgentSourceAccessState.LimitedSupport
                    : AgentSourceAccessState.Detected;
            }
            else
            {
                result.AccessState = AgentSourceAccessState.NotDetected;
                result.WarningIds.Add("agent_source_not_detected");
            }

            return Task.FromResult(result);
        }

        public static AgentProviderType NormalizeProvider(AgentProviderType providerType)
        {
            return providerType == AgentProviderType.Claude ? AgentProviderType.ClaudeCode : providerType;
        }

        public static AgentProviderType NormalizeProviderValue(string providerValue)
        {
            if (string.Equals(providerValue, "Chat" + "GPT", StringComparison.OrdinalIgnoreCase))
            {
                return AgentProviderType.Codex;
            }

            if (Enum.TryParse(providerValue, true, out AgentProviderType parsed))
            {
                return NormalizeProvider(parsed);
            }

            return AgentProviderType.Unknown;
        }

        public static string SafeProviderLabel(AgentProviderType providerType)
        {
            switch (NormalizeProvider(providerType))
            {
                case AgentProviderType.Cursor: return "Cursor";
                case AgentProviderType.ClaudeCode: return "Claude Code";
                case AgentProviderType.Codex: return "Codex";
                case AgentProviderType.GitHubCopilot: return "GitHub Copilot";
                case AgentProviderType.Manual: return "Other / Manual Log Folder";
                default: return "Unknown Agent";
            }
        }

        private AgentSourceCandidate Probe(string path, string safeAlias, bool limitedSupport, CancellationToken cancellationToken)
        {
            try
            {
                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    return null;
                }

                var count = 0;
                long size = 0;
                DateTimeOffset? modified = null;
                if (File.Exists(path))
                {
                    var file = new FileInfo(path);
                    count = 1;
                    size = Math.Min(file.Length, MaxSizeForBucketBytes + 1);
                    modified = file.LastWriteTimeUtc;
                }
                else
                {
                    foreach (var file in EnumerateSafeFiles(path, MaxProbeDepth, cancellationToken))
                    {
                        count++;
                        size = Math.Min(MaxSizeForBucketBytes + 1, size + Math.Min(file.Length, MaxSizeForBucketBytes + 1));
                        var lastWrite = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
                        if (modified == null || lastWrite > modified.Value)
                        {
                            modified = lastWrite;
                        }

                        if (count >= MaxFilesForBucket)
                        {
                            break;
                        }
                    }
                }

                return new AgentSourceCandidate
                {
                    LocalPath = path,
                    SafeAlias = safeAlias,
                    SourceKind = AgentSourceKind.DetectedLocal,
                    FileCountBucket = SafeAgentLogParser.ToCountBucket(count),
                    SizeBucket = SizeToBucket(size),
                    LastModifiedUtc = modified,
                    Confidence = limitedSupport ? ConfidenceLevel.Low : ConfidenceLevel.Medium,
                    AccessState = limitedSupport ? AgentSourceAccessState.LimitedSupport : AgentSourceAccessState.Detected,
                    WarningIds = limitedSupport ? new List<string> { "agent_source_limited_support" } : new List<string>()
                };
            }
            catch (UnauthorizedAccessException)
            {
                return PermissionCandidate(safeAlias);
            }
            catch (IOException)
            {
                return PermissionCandidate(safeAlias);
            }
        }

        private static AgentSourceCandidate PermissionCandidate(string safeAlias)
        {
            return new AgentSourceCandidate
            {
                SafeAlias = safeAlias,
                AccessState = AgentSourceAccessState.PermissionRequired,
                Confidence = ConfidenceLevel.Low,
                WarningIds = new List<string> { "agent_source_permission_required" }
            };
        }

        private IEnumerable<FileInfo> EnumerateSafeFiles(string directory, int maxDepth, CancellationToken cancellationToken)
        {
            if (maxDepth < 0 || string.IsNullOrWhiteSpace(directory))
            {
                yield break;
            }

            DirectoryInfo info;
            try
            {
                info = new DirectoryInfo(directory);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    yield break;
                }
            }
            catch (IOException)
            {
                yield break;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }

            IEnumerable<FileInfo> files;
            try
            {
                files = info.EnumerateFiles();
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (IOException)
            {
                yield break;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
            }

            if (maxDepth == 0)
            {
                yield break;
            }

            IEnumerable<DirectoryInfo> children;
            try
            {
                children = info.EnumerateDirectories()
                    .Where(child => (child.Attributes & FileAttributes.ReparsePoint) == 0 && !IsHiddenSystemDirectory(child.Name));
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (IOException)
            {
                yield break;
            }

            foreach (var child in children)
            {
                foreach (var file in EnumerateSafeFiles(child.FullName, maxDepth - 1, cancellationToken))
                {
                    yield return file;
                }
            }
        }

        private static bool IsHiddenSystemDirectory(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            return name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Library", StringComparison.OrdinalIgnoreCase);
        }

        private static CountBucket SizeToBucket(long size)
        {
            if (size <= 0) return CountBucket.None;
            if (size < 64 * 1024) return CountBucket.Small;
            if (size < 1024 * 1024) return CountBucket.Medium;
            if (size < 32 * 1024 * 1024) return CountBucket.Large;
            return CountBucket.Huge;
        }

        private string Expand(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.StartsWith("~/", StringComparison.Ordinal))
            {
                return Path.Combine(homeDirectory, value.Substring(2));
            }

            if (value.StartsWith("./", StringComparison.Ordinal))
            {
                return Path.Combine(projectDirectory, value.Substring(2));
            }

            return value;
        }

        private static IEnumerable<(string RelativePath, string SafeAlias)> CandidateSpecs(AgentProviderType providerType)
        {
            switch (NormalizeProvider(providerType))
            {
                case AgentProviderType.Cursor:
                    yield return ("~/Library/Application Support/Cursor", "Cursor local data");
                    yield return ("~/Library/Application Support/Cursor/User", "Cursor user data");
                    yield return ("~/Library/Application Support/Cursor/User/globalStorage", "Cursor workspace storage");
                    yield return ("~/Library/Logs/Cursor", "Cursor local logs");
                    break;
                case AgentProviderType.ClaudeCode:
                    yield return ("~/.claude", "Claude Code local data");
                    yield return ("~/.claude/projects", "Claude Code project sessions");
                    yield return ("~/.claude.json", "Claude Code settings");
                    yield return ("./.claude", "Claude Code project data");
                    break;
                case AgentProviderType.Codex:
                    yield return ("~/.codex", "Codex local data");
                    yield return ("~/.config/codex", "Codex config data");
                    yield return ("~/Library/Application Support/Codex", "Codex application data");
                    yield return ("./.codex", "Codex project data");
                    break;
                case AgentProviderType.GitHubCopilot:
                    yield return ("~/Library/Application Support/Code/User/globalStorage", "GitHub Copilot VS Code storage");
                    yield return ("~/Library/Application Support/Code/logs", "GitHub Copilot VS Code logs");
                    yield return ("~/Library/Application Support/Cursor/User/globalStorage", "GitHub Copilot Cursor storage");
                    yield return ("~/Library/Application Support/Cursor/logs", "GitHub Copilot Cursor logs");
                    break;
            }
        }
    }
}
