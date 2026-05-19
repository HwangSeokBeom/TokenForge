using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Agents
{
    public sealed class AgentLogActivityProvider
    {
        public const string SourceProviderId = "AI_AGENT";

        private readonly IAgentActivityAnalyzer analyzer;
        private readonly PrivacySanitizer privacySanitizer;

        public AgentLogActivityProvider(IAgentActivityAnalyzer analyzer = null, PrivacySanitizer privacySanitizer = null)
        {
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.analyzer = analyzer ?? new AgentActivityAnalyzer(null, null, this.privacySanitizer);
        }

        public async Task<Result<AgentWorkSession>> AnalyzeSessionAsync(AgentAnalysisInput input, CancellationToken cancellationToken)
        {
            var result = await analyzer.AnalyzeAsync(input, cancellationToken);
            if (!result.IsSuccess)
            {
                return Result<AgentWorkSession>.Failure(result.ErrorCode, result.ErrorMessage);
            }

            var session = CreateSession(result.Value);
            var validation = privacySanitizer.ValidateSafeSession(session);
            if (!validation.IsSuccess)
            {
                return Result<AgentWorkSession>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            return Result<AgentWorkSession>.Success(session);
        }

        public static AgentWorkSession CreateSession(AgentActivitySummary summary)
        {
            summary = summary ?? AgentActivitySummary.Empty();
            var endedAt = DateTimeOffset.UtcNow;
            var startedAt = endedAt.AddHours(-1);
            if (DateTimeOffset.TryParse(summary.DayBucket, out var day))
            {
                startedAt = day;
                endedAt = day.AddDays(1).AddTicks(-1);
            }

            var session = new AgentWorkSession
            {
                AgentType = ToAgentType(summary.ProviderType),
                WorkType = InferWorkType(summary),
                StartedAt = startedAt,
                EndedAt = endedAt >= startedAt ? endedAt : startedAt,
                TokenUsageBucket = TokenUsageBucket.Unknown,
                ActionSummary = ToActionSummary(summary),
                GitChangeSummary = GitChangeSummary.Empty(),
                AgentActivitySummary = summary,
                ResultStatus = summary.ConfidenceLevel == ConfidenceLevel.Low ? ResultStatus.PartiallySucceeded : ResultStatus.Succeeded,
                SourceProvider = SourceProviderId,
                SourceProviders = { SourceProviderId },
                ParserVersion = summary.AnalyzerVersion,
                Confidence = ToProviderConfidence(summary.ConfidenceLevel),
                Warnings = (summary.WarningIds ?? Enumerable.Empty<string>()).Take(12).ToList(),
                UserReviewed = true
            };

            session.DeduplicationKey = $"{summary.SourceIdentifierHash}:{summary.DayBucket}:provider{(int)summary.ProviderType}:{summary.SessionCountBucket}:{summary.InteractionCountBucket}";
            return session;
        }

        private static AgentActionSummary ToActionSummary(AgentActivitySummary summary)
        {
            return new AgentActionSummary
            {
                PromptCount = EstimateCount(summary.InteractionCountBucket),
                ToolCallCount = EstimateCount(summary.EstimatedCodingActivityBucket),
                FileEditCount = EstimateCategory(summary, AgentToolUsageCategory.CodeEditing),
                CommandRunCount = EstimateCategory(summary, AgentToolUsageCategory.ShellCommand),
                TestRunCount = EstimateCategory(summary, AgentToolUsageCategory.TestRun),
                BuildRunCount = EstimateCategory(summary, AgentToolUsageCategory.BuildRun),
                DurationBucket = DurationBucket.Unknown
            };
        }

        private static int EstimateCategory(AgentActivitySummary summary, AgentToolUsageCategory category)
        {
            var bucket = (summary.ToolUsageCategoryBuckets ?? Enumerable.Empty<AgentToolUsageCategoryBucket>())
                .FirstOrDefault(item => item.Category == category)?.CountBucket ?? CountBucket.None;
            return EstimateCount(bucket);
        }

        private static int EstimateCount(CountBucket bucket)
        {
            switch (bucket)
            {
                case CountBucket.One: return 1;
                case CountBucket.Small: return 3;
                case CountBucket.Medium: return 10;
                case CountBucket.Large: return 50;
                case CountBucket.Huge: return 100;
                default: return 0;
            }
        }

        private static AgentType ToAgentType(AgentProviderType providerType)
        {
            switch (providerType)
            {
                case AgentProviderType.Cursor: return AgentType.Cursor;
                case AgentProviderType.Claude: return AgentType.ClaudeCode;
                case AgentProviderType.ClaudeCode: return AgentType.ClaudeCode;
                case AgentProviderType.Codex: return AgentType.Codex;
                case AgentProviderType.GitHubCopilot: return AgentType.GitHubCopilot;
                case AgentProviderType.Manual: return AgentType.ManualFallback;
                default: return AgentType.Unknown;
            }
        }

        private static WorkType InferWorkType(AgentActivitySummary summary)
        {
            if (HasCategory(summary, AgentToolUsageCategory.TestRun)) return WorkType.Test;
            if (HasCategory(summary, AgentToolUsageCategory.BuildRun)) return WorkType.Build;
            if (HasLanguage(summary, AgentLanguageCategory.Docs)) return WorkType.Docs;
            if (HasCategory(summary, AgentToolUsageCategory.CodeEditing)) return WorkType.Mixed;
            if (HasCategory(summary, AgentToolUsageCategory.Search) || HasCategory(summary, AgentToolUsageCategory.FileNavigation)) return WorkType.Research;
            return WorkType.Unknown;
        }

        private static bool HasCategory(AgentActivitySummary summary, AgentToolUsageCategory category)
        {
            return (summary.ToolUsageCategoryBuckets ?? Enumerable.Empty<AgentToolUsageCategoryBucket>())
                .Any(item => item.Category == category && item.CountBucket != CountBucket.None && item.CountBucket != CountBucket.Unknown);
        }

        private static bool HasLanguage(AgentActivitySummary summary, AgentLanguageCategory category)
        {
            return (summary.LanguageCategoryBuckets ?? Enumerable.Empty<AgentLanguageCategoryBucket>())
                .Any(item => item.Category == category && item.CountBucket != CountBucket.None && item.CountBucket != CountBucket.Unknown);
        }

        private static ProviderConfidence ToProviderConfidence(ConfidenceLevel confidence)
        {
            switch (confidence)
            {
                case ConfidenceLevel.High: return ProviderConfidence.High;
                case ConfidenceLevel.Medium: return ProviderConfidence.Medium;
                case ConfidenceLevel.Low: return ProviderConfidence.Low;
                default: return ProviderConfidence.Unknown;
            }
        }
    }
}
