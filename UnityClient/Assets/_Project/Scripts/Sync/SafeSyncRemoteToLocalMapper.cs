using System;
using System.Collections.Generic;
using System.Linq;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Sync
{
    public sealed class SafeSyncRemoteToLocalMapper
    {
        public AgentWorkSession ToLocalSession(RemoteSafeActivitySessionDto remote)
        {
            if (remote == null)
            {
                throw new ArgumentNullException(nameof(remote));
            }

            var day = ParseDay(remote.DayBucket);
            var start = day.AddHours(ParseHour(remote.TimeBucket));
            var end = start.AddMinutes(1);
            var sourceProvider = SafeSourceProvider(remote.SourceProvider);
            return new AgentWorkSession
            {
                SessionId = remote.ClientSessionId ?? string.Empty,
                AgentType = ToAgentType(sourceProvider),
                WorkType = ToWorkType(remote.ActivityCategory),
                StartedAt = start,
                EndedAt = end,
                TokenUsageBucket = TokenUsageBucket.Unknown,
                ActionSummary = new AgentActionSummary
                {
                    DurationBucket = ToDurationBucket(remote.DurationBucket)
                },
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCountBucket = ToCountBucket(remote.ChangeCountBucket),
                    AddedLineBucket = ToLineChangeBucket(remote.LineCountBucket),
                    DeletedLineBucket = LineChangeBucket.Unknown,
                    CommitCountBucket = ToCountBucket(remote.CommitCountBucket),
                    ProjectPathHash = SafeHash(remote.HashedRepositoryId),
                    AnalysisTimeBucket = remote.DayBucket ?? string.Empty,
                    AnalyzerVersion = remote.AnalyzerVersion ?? string.Empty,
                    PrivacyWarnings = SafeWarningIds(remote.WarningIds)
                },
                AgentActivitySummary = new AgentActivitySummary
                {
                    ProviderType = ToAgentProviderType(sourceProvider),
                    SourceIdentifierHash = SafeHash(remote.HashedRepositoryId),
                    DayBucket = remote.DayBucket ?? string.Empty,
                    SessionCountBucket = ToCountBucket(remote.SessionCountBucket),
                    InteractionCountBucket = ToCountBucket(remote.InteractionCountBucket),
                    EstimatedCodingActivityBucket = CountBucket.Unknown,
                    ToolUsageCategoryBuckets = ToToolBuckets(remote.ToolBuckets),
                    LanguageCategoryBuckets = ToLanguageBuckets(remote.LanguageBuckets),
                    ConfidenceLevel = ToConfidenceLevel(remote.Confidence),
                    WarningIds = SafeWarningIds(remote.WarningIds),
                    AnalyzerVersion = remote.AnalyzerVersion ?? string.Empty
                },
                ResultStatus = ResultStatus.Unknown,
                SourceProvider = sourceProvider,
                SourceProviders = new List<string> { sourceProvider },
                ParserVersion = remote.ParserVersion ?? string.Empty,
                Confidence = ToProviderConfidence(remote.Confidence),
                Warnings = SafeWarningIds(remote.WarningIds),
                DeduplicationKey = string.Empty,
                SourceSessionIds = new List<string>(),
                UserReviewed = true
            };
        }

        private static DateTimeOffset ParseDay(string value)
        {
            if (DateTimeOffset.TryParse((value ?? string.Empty) + "T00:00:00Z", out var parsed))
            {
                return parsed;
            }

            return DateTimeOffset.UtcNow.Date;
        }

        private static int ParseHour(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                value.StartsWith("HOUR_", StringComparison.Ordinal) &&
                int.TryParse(value.Substring(5), out var hour))
            {
                return Math.Max(0, Math.Min(23, hour));
            }

            return 0;
        }

        private static string SafeSourceProvider(string provider)
        {
            if (string.Equals(provider, "CODEX", StringComparison.OrdinalIgnoreCase)) return "CODEX";
            if (string.Equals(provider, "CLAUDE", StringComparison.OrdinalIgnoreCase)) return "CLAUDE";
            if (string.Equals(provider, "GIT", StringComparison.OrdinalIgnoreCase)) return "GIT";
            if (string.Equals(provider, "MANUAL", StringComparison.OrdinalIgnoreCase)) return "MANUAL";
            return "UNKNOWN_AGENT";
        }

        private static AgentType ToAgentType(string sourceProvider)
        {
            if (string.Equals(sourceProvider, "CODEX", StringComparison.OrdinalIgnoreCase)) return AgentType.Codex;
            if (string.Equals(sourceProvider, "CLAUDE", StringComparison.OrdinalIgnoreCase)) return AgentType.ClaudeCode;
            if (string.Equals(sourceProvider, "GIT", StringComparison.OrdinalIgnoreCase)) return AgentType.GitOnly;
            if (string.Equals(sourceProvider, "MANUAL", StringComparison.OrdinalIgnoreCase)) return AgentType.ManualFallback;
            return AgentType.Unknown;
        }

        private static AgentProviderType ToAgentProviderType(string sourceProvider)
        {
            if (string.Equals(sourceProvider, "CODEX", StringComparison.OrdinalIgnoreCase)) return AgentProviderType.Codex;
            if (string.Equals(sourceProvider, "CLAUDE", StringComparison.OrdinalIgnoreCase)) return AgentProviderType.ClaudeCode;
            if (string.Equals(sourceProvider, "CURSOR", StringComparison.OrdinalIgnoreCase)) return AgentProviderType.Cursor;
            if (string.Equals(sourceProvider, "GITHUB_COPILOT", StringComparison.OrdinalIgnoreCase)) return AgentProviderType.GitHubCopilot;
            return AgentProviderType.Unknown;
        }

        private static WorkType ToWorkType(string value)
        {
            switch (value)
            {
                case "WORK_FEATURE": return WorkType.Feature;
                case "WORK_BUGFIX": return WorkType.Bugfix;
                case "WORK_REFACTOR": return WorkType.Refactor;
                case "WORK_TEST": return WorkType.Test;
                case "WORK_UIUX": return WorkType.UIUX;
                case "WORK_DOCS": return WorkType.Docs;
                case "WORK_BUILD": return WorkType.Build;
                case "WORK_CHORE": return WorkType.Chore;
                case "WORK_RESEARCH": return WorkType.Research;
                case "WORK_MIXED": return WorkType.Mixed;
                default: return WorkType.Unknown;
            }
        }

        private static CountBucket ToCountBucket(string value)
        {
            switch (value)
            {
                case "NONE": return CountBucket.None;
                case "ONE": return CountBucket.One;
                case "FEW": return CountBucket.Small;
                case "MANY": return CountBucket.Medium;
                case "MASSIVE": return CountBucket.Huge;
                default: return CountBucket.Unknown;
            }
        }

        private static LineChangeBucket ToLineChangeBucket(string value)
        {
            switch (value)
            {
                case "NONE": return LineChangeBucket.None;
                case "FEW": return LineChangeBucket.Small;
                case "MANY": return LineChangeBucket.Medium;
                case "MASSIVE": return LineChangeBucket.Huge;
                default: return LineChangeBucket.Unknown;
            }
        }

        private static DurationBucket ToDurationBucket(string value)
        {
            switch (value)
            {
                case "UNDER_5M": return DurationBucket.Under5Minutes;
                case "M_5_15": return DurationBucket.FiveTo15Minutes;
                case "M_30_60": return DurationBucket.FifteenTo60Minutes;
                case "H_1_2": return DurationBucket.OneTo3Hours;
                case "H_2_PLUS": return DurationBucket.Over3Hours;
                default: return DurationBucket.Unknown;
            }
        }

        private static ProviderConfidence ToProviderConfidence(string value)
        {
            if (string.Equals(value, "HIGH", StringComparison.OrdinalIgnoreCase)) return ProviderConfidence.High;
            if (string.Equals(value, "MEDIUM", StringComparison.OrdinalIgnoreCase)) return ProviderConfidence.Medium;
            if (string.Equals(value, "LOW", StringComparison.OrdinalIgnoreCase)) return ProviderConfidence.Low;
            return ProviderConfidence.Unknown;
        }

        private static ConfidenceLevel ToConfidenceLevel(string value)
        {
            if (string.Equals(value, "HIGH", StringComparison.OrdinalIgnoreCase)) return ConfidenceLevel.High;
            if (string.Equals(value, "MEDIUM", StringComparison.OrdinalIgnoreCase)) return ConfidenceLevel.Medium;
            if (string.Equals(value, "LOW", StringComparison.OrdinalIgnoreCase)) return ConfidenceLevel.Low;
            return ConfidenceLevel.Unknown;
        }

        private static List<AgentToolUsageCategoryBucket> ToToolBuckets(List<SafeBucketContractDto> buckets)
        {
            return (buckets ?? new List<SafeBucketContractDto>())
                .Take(12)
                .Select(bucket => new AgentToolUsageCategoryBucket
                {
                    Category = ToToolCategory(bucket.Key),
                    CountBucket = ToCountBucket(bucket.CountBucket)
                })
                .ToList();
        }

        private static List<AgentLanguageCategoryBucket> ToLanguageBuckets(List<SafeBucketContractDto> buckets)
        {
            return (buckets ?? new List<SafeBucketContractDto>())
                .Take(12)
                .Select(bucket => new AgentLanguageCategoryBucket
                {
                    Category = ToLanguageCategory(bucket.Key),
                    CountBucket = ToCountBucket(bucket.CountBucket)
                })
                .ToList();
        }

        private static AgentToolUsageCategory ToToolCategory(string value)
        {
            switch (value)
            {
                case "TOOL_EDIT": return AgentToolUsageCategory.CodeEditing;
                case "TOOL_SHELL": return AgentToolUsageCategory.ShellCommand;
                case "TOOL_TEST": return AgentToolUsageCategory.TestRun;
                case "TOOL_BUILD": return AgentToolUsageCategory.BuildRun;
                case "TOOL_NAVIGATION": return AgentToolUsageCategory.FileNavigation;
                case "TOOL_SEARCH": return AgentToolUsageCategory.Search;
                default: return AgentToolUsageCategory.Unknown;
            }
        }

        private static AgentLanguageCategory ToLanguageCategory(string value)
        {
            switch (value)
            {
                case "LANG_CSHARP": return AgentLanguageCategory.CSharp;
                case "LANG_JAVASCRIPT": return AgentLanguageCategory.JavaScript;
                case "LANG_TYPESCRIPT": return AgentLanguageCategory.TypeScript;
                case "LANG_PYTHON": return AgentLanguageCategory.Python;
                case "LANG_WEB": return AgentLanguageCategory.Web;
                case "LANG_CONFIG": return AgentLanguageCategory.Config;
                case "LANG_DOCS": return AgentLanguageCategory.Docs;
                case "LANG_TEST": return AgentLanguageCategory.Test;
                case "LANG_SHELL": return AgentLanguageCategory.Shell;
                default: return AgentLanguageCategory.Unknown;
            }
        }

        private static string SafeHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var filtered = new string(value.Where(Uri.IsHexDigit).ToArray());
            return filtered.Length > 128 ? filtered.Substring(0, 128) : filtered;
        }

        private static List<string> SafeWarningIds(List<string> warningIds)
        {
            return (warningIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .Take(25)
                .ToList();
        }
    }
}
