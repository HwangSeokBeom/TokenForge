using System;
using System.Collections.Generic;
using System.Linq;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Agents
{
    public sealed class DeduplicationCandidate
    {
        public DeduplicationDecision Decision { get; set; } = DeduplicationDecision.KeepSeparate;
        public ProviderConfidence Confidence { get; set; } = ProviderConfidence.Low;
        public AgentWorkSession MergedSession { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class SessionDeduplicationService
    {
        public DeduplicationCandidate Evaluate(AgentWorkSession first, AgentWorkSession second, bool userConfirmedReview = false)
        {
            if (first == null || second == null)
            {
                return KeepSeparate("missing_session");
            }

            var sameProject = SameProject(first, second);
            var timeOverlap = TimeRangesOverlap(first, second);
            var similarFileCount = SimilarFileCount(first, second);
            var compatibleWorkType = WorkTypesCompatible(first.WorkType, second.WorkType);

            if (!sameProject || !timeOverlap)
            {
                return KeepSeparate("different_project_or_time_range");
            }

            if (compatibleWorkType && similarFileCount && BothHighConfidence(first, second))
            {
                return new DeduplicationCandidate
                {
                    Decision = DeduplicationDecision.MergeAutomatically,
                    Confidence = ProviderConfidence.High,
                    MergedSession = Merge(first, second, true),
                    Reason = "high_confidence_same_project_time_and_file_count"
                };
            }

            if (userConfirmedReview && compatibleWorkType)
            {
                return new DeduplicationCandidate
                {
                    Decision = DeduplicationDecision.MergeAutomatically,
                    Confidence = ProviderConfidence.Medium,
                    MergedSession = Merge(first, second, true),
                    Reason = "user_review_confirmed"
                };
            }

            return new DeduplicationCandidate
            {
                Decision = DeduplicationDecision.RequiresUserReview,
                Confidence = ProviderConfidence.Medium,
                MergedSession = Merge(first, second, false),
                Reason = "possible_duplicate_requires_review"
            };
        }

        private static DeduplicationCandidate KeepSeparate(string reason)
        {
            return new DeduplicationCandidate
            {
                Decision = DeduplicationDecision.KeepSeparate,
                Confidence = ProviderConfidence.Low,
                Reason = reason
            };
        }

        private static bool SameProject(AgentWorkSession first, AgentWorkSession second)
        {
            var firstHash = first.GitChangeSummary?.ProjectPathHash ?? string.Empty;
            var secondHash = second.GitChangeSummary?.ProjectPathHash ?? string.Empty;
            return !string.IsNullOrWhiteSpace(firstHash) && firstHash == secondHash;
        }

        private static bool TimeRangesOverlap(AgentWorkSession first, AgentWorkSession second)
        {
            return first.StartedAt <= second.EndedAt && second.StartedAt <= first.EndedAt;
        }

        private static bool SimilarFileCount(AgentWorkSession first, AgentWorkSession second)
        {
            var firstCount = Math.Max(first.GitChangeSummary?.ChangedFileCount ?? 0, first.ActionSummary?.FileEditCount ?? 0);
            var secondCount = Math.Max(second.GitChangeSummary?.ChangedFileCount ?? 0, second.ActionSummary?.FileEditCount ?? 0);
            if (firstCount == 0 || secondCount == 0)
            {
                return false;
            }

            var difference = Math.Abs(firstCount - secondCount);
            return difference <= Math.Max(2, Math.Min(firstCount, secondCount) / 3);
        }

        private static bool WorkTypesCompatible(WorkType first, WorkType second)
        {
            return first == second || first == WorkType.Unknown || second == WorkType.Unknown || first == WorkType.Mixed || second == WorkType.Mixed;
        }

        private static bool BothHighConfidence(AgentWorkSession first, AgentWorkSession second)
        {
            return first.Confidence == ProviderConfidence.High && second.Confidence == ProviderConfidence.High;
        }

        private static AgentWorkSession Merge(AgentWorkSession first, AgentWorkSession second, bool userReviewed)
        {
            var providers = new HashSet<string>(first.SourceProviders.Concat(second.SourceProviders).Where(p => !string.IsNullOrWhiteSpace(p)));
            if (!string.IsNullOrWhiteSpace(first.SourceProvider)) providers.Add(first.SourceProvider);
            if (!string.IsNullOrWhiteSpace(second.SourceProvider)) providers.Add(second.SourceProvider);

            var sourceSessionIds = new HashSet<string>(first.SourceSessionIds.Concat(second.SourceSessionIds));
            sourceSessionIds.Add(first.SessionId);
            sourceSessionIds.Add(second.SessionId);

            var selectedGit = SelectRicherGitSummary(first.GitChangeSummary, second.GitChangeSummary);
            var selectedActions = SelectRicherActionSummary(first.ActionSummary, second.ActionSummary);
            var selectedWorkType = first.WorkType != WorkType.Unknown && first.WorkType != WorkType.Mixed ? first.WorkType : second.WorkType;

            return new AgentWorkSession
            {
                AgentType = first.AgentType == AgentType.GitOnly ? second.AgentType : first.AgentType,
                WorkType = selectedWorkType,
                StartedAt = first.StartedAt < second.StartedAt ? first.StartedAt : second.StartedAt,
                EndedAt = first.EndedAt > second.EndedAt ? first.EndedAt : second.EndedAt,
                TokenUsageBucket = first.TokenUsageBucket != TokenUsageBucket.Unknown ? first.TokenUsageBucket : second.TokenUsageBucket,
                ActionSummary = selectedActions,
                GitChangeSummary = selectedGit,
                ResultStatus = first.ResultStatus != ResultStatus.Unknown ? first.ResultStatus : second.ResultStatus,
                SourceProvider = string.Join("+", providers.OrderBy(p => p)),
                SourceProviders = providers.OrderBy(p => p).ToList(),
                ParserVersion = "dedup-v1",
                Confidence = BothHighConfidence(first, second) ? ProviderConfidence.High : ProviderConfidence.Medium,
                SourceSessionIds = sourceSessionIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList(),
                UserReviewed = userReviewed,
                DeduplicationKey = BuildDeduplicationKey(first, second)
            };
        }

        private static GitChangeSummary SelectRicherGitSummary(GitChangeSummary first, GitChangeSummary second)
        {
            if (first == null) return second ?? GitChangeSummary.Empty();
            if (second == null) return first;
            return first.ChangedFileCount >= second.ChangedFileCount ? first : second;
        }

        private static AgentActionSummary SelectRicherActionSummary(AgentActionSummary first, AgentActionSummary second)
        {
            if (first == null) return second ?? AgentActionSummary.Empty();
            if (second == null) return first;
            var firstSignal = first.FileEditCount + first.CommandRunCount + first.ToolCallCount;
            var secondSignal = second.FileEditCount + second.CommandRunCount + second.ToolCallCount;
            return firstSignal >= secondSignal ? first : second;
        }

        private static string BuildDeduplicationKey(AgentWorkSession first, AgentWorkSession second)
        {
            var hash = first.GitChangeSummary?.ProjectPathHash ?? second.GitChangeSummary?.ProjectPathHash ?? string.Empty;
            var hour = (first.StartedAt < second.StartedAt ? first.StartedAt : second.StartedAt).ToString("yyyyMMddHH");
            return $"{hash}:{hour}:{first.SessionId}:{second.SessionId}";
        }
    }
}
