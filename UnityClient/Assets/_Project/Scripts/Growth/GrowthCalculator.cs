using System;
using System.Collections.Generic;
using System.Linq;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Growth
{
    public sealed class GrowthCalculator
    {
        private readonly GrowthCalculationConfig config;
        private readonly Dictionary<WorkType, GrowthRule> rules;

        public GrowthCalculator(GrowthCalculationConfig config = null, IEnumerable<GrowthRule> rules = null)
        {
            this.config = config ?? new GrowthCalculationConfig();
            this.rules = (rules ?? DefaultGrowthRules.Create()).GroupBy(rule => rule.TargetWorkType)
                .ToDictionary(group => group.Key, group => group.First());
        }

        public CharacterGrowthResult Calculate(AgentWorkSession session, CharacterProfile profile, int expAlreadyGainedToday = 0, float miniGameBonusRatio = 0f)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var rule = rules.TryGetValue(session.WorkType, out var selectedRule)
                ? selectedRule
                : rules[WorkType.Unknown];

            var tokenMultiplier = GetTokenMultiplier(session.TokenUsageBucket);
            var fileMultiplier = GetFileChangeMultiplier(session.GitChangeSummary?.ChangedFileCount ?? session.ActionSummary?.FileEditCount ?? 0);
            var lineMultiplier = GetLineChangeMultiplier(session.GitChangeSummary);
            var gitAggregateMultiplier = GetGitAggregateConfidenceMultiplier(session);
            var antiGrindingMultiplier = GetAntiGrindingMultiplier(session);
            var resultMultiplier = GetResultMultiplier(session.ResultStatus, rule);
            var baseExp = rule.BaseExp * tokenMultiplier * fileMultiplier * lineMultiplier * gitAggregateMultiplier * antiGrindingMultiplier * resultMultiplier;

            // Mini-game rewards are optional side rewards and should stay within 5-15% of the final work-session reward.
            var clampedMiniGameRatio = Math.Max(0f, Math.Min(config.MiniGameBonusMaxRatio, miniGameBonusRatio));
            if (clampedMiniGameRatio > 0f)
            {
                clampedMiniGameRatio = Math.Max(config.MiniGameBonusMinRatio, clampedMiniGameRatio);
            }

            var exp = (int)Math.Round(baseExp * (1f + clampedMiniGameRatio));
            var capApplied = false;
            if (config.EnableDailyCap && expAlreadyGainedToday + exp > config.DailyExpCap)
            {
                exp = Math.Max(0, config.DailyExpCap - expAlreadyGainedToday);
                capApplied = true;
            }

            var levelBefore = profile.Level;
            var levelAfter = CalculateLevel(profile.TotalExp + exp);
            var statDeltas = CalculateStatDeltas(rule.StatWeights, tokenMultiplier, fileMultiplier * lineMultiplier * gitAggregateMultiplier, session);
            var stressDelta = CalculateStressDelta(session, rule);
            statDeltas.Stress += stressDelta;

            return new CharacterGrowthResult
            {
                SessionId = session.SessionId,
                ExpGained = exp,
                LevelBefore = levelBefore,
                LevelAfter = levelAfter,
                StatDeltas = statDeltas,
                StressDelta = stressDelta,
                EvolutionProgressDelta = InferEvolutionDelta(session.WorkType, statDeltas),
                EvolutionProgressDeltaAmount = Math.Max(1, exp / 100),
                RewardTags = BuildRewardTags(session, capApplied),
                Warnings = BuildWarnings(session, antiGrindingMultiplier, capApplied),
                DailyCapApplied = capApplied
            };
        }

        public CharacterGrowthResult Calculate(CharacterProfile profile, AgentWorkSession session, float miniGameBonusRatio = 0f)
        {
            return Calculate(session, profile, 0, miniGameBonusRatio);
        }

        private int CalculateLevel(int totalExp)
        {
            return Math.Max(1, totalExp / Math.Max(1, config.ExpPerLevel) + 1);
        }

        private static float GetTokenMultiplier(TokenUsageBucket bucket)
        {
            switch (bucket)
            {
                case TokenUsageBucket.None: return 0.85f;
                case TokenUsageBucket.Small: return 1.0f;
                case TokenUsageBucket.Medium: return 1.15f;
                case TokenUsageBucket.Large: return 1.35f;
                case TokenUsageBucket.Huge: return 1.55f;
                default: return 1.0f;
            }
        }

        private static float GetFileChangeMultiplier(int fileCount)
        {
            if (fileCount <= 0) return 0.9f;
            if (fileCount <= 3) return 1.0f;
            if (fileCount <= 8) return 1.15f;
            if (fileCount <= 20) return 1.35f;
            return 1.5f;
        }

        private static float GetLineChangeMultiplier(GitChangeSummary summary)
        {
            if (summary == null) return 1f;

            var added = BucketWeight(summary.AddedLineBucket);
            var deleted = BucketWeight(summary.DeletedLineBucket);
            return Math.Min(1.35f, 1f + ((added + deleted) * 0.075f));
        }

        private static float GetGitAggregateConfidenceMultiplier(AgentWorkSession session)
        {
            if (!IsGitAggregateSession(session))
            {
                return 1f;
            }

            var multiplier = 1f;
            switch (session.GitChangeSummary?.ConfidenceLevel ?? ConfidenceLevel.Unknown)
            {
                case ConfidenceLevel.High:
                    multiplier = 1f;
                    break;
                case ConfidenceLevel.Medium:
                    multiplier = 0.9f;
                    break;
                case ConfidenceLevel.Low:
                    multiplier = 0.7f;
                    break;
                default:
                    multiplier = 0.8f;
                    break;
            }

            if ((session.GitChangeSummary?.PrivacyWarnings?.Count ?? 0) > 0)
            {
                multiplier *= 0.85f;
            }

            return Math.Max(0.55f, multiplier);
        }

        private static int BucketWeight(LineChangeBucket bucket)
        {
            switch (bucket)
            {
                case LineChangeBucket.Small: return 1;
                case LineChangeBucket.Medium: return 2;
                case LineChangeBucket.Large: return 3;
                case LineChangeBucket.Huge: return 4;
                default: return 0;
            }
        }

        private static float GetAntiGrindingMultiplier(AgentWorkSession session)
        {
            var changedFiles = Math.Max(session.GitChangeSummary?.ChangedFileCount ?? 0, session.ActionSummary?.FileEditCount ?? 0);
            var actionCount = (session.ActionSummary?.ToolCallCount ?? 0)
                + (session.ActionSummary?.CommandRunCount ?? 0)
                + (session.ActionSummary?.TestRunCount ?? 0)
                + (session.ActionSummary?.BuildRunCount ?? 0);
            var addedBucket = session.GitChangeSummary?.AddedLineBucket ?? LineChangeBucket.Unknown;
            var deletedBucket = session.GitChangeSummary?.DeletedLineBucket ?? LineChangeBucket.Unknown;
            var noLineSignal = (addedBucket == LineChangeBucket.Unknown || addedBucket == LineChangeBucket.None)
                && (deletedBucket == LineChangeBucket.Unknown || deletedBucket == LineChangeBucket.None);

            if (changedFiles <= 0 && actionCount <= 0)
            {
                return 0.25f;
            }

            if (changedFiles <= 1 && noLineSignal && session.TokenUsageBucket <= TokenUsageBucket.Small)
            {
                return 0.45f;
            }

            return 1f;
        }

        private static float GetResultMultiplier(ResultStatus status, GrowthRule rule)
        {
            switch (status)
            {
                case ResultStatus.Succeeded:
                    return 1f + rule.SuccessBonus;
                case ResultStatus.Failed:
                    return rule.FailureCompensation;
                case ResultStatus.PartiallySucceeded:
                    return 0.8f;
                default:
                    return 0.9f;
            }
        }

        private static CharacterStats CalculateStatDeltas(CharacterStats weights, float tokenMultiplier, float fileMultiplier, AgentWorkSession session)
        {
            var scale = Math.Max(1f, (tokenMultiplier + fileMultiplier) * 0.5f);
            var deltas = new CharacterStats
            {
                Logic = Scale(weights.Logic, scale),
                Debug = Scale(weights.Debug, scale) + (session.ActionSummary?.FailedCommandCount > 0 ? 1 : 0),
                Architecture = Scale(weights.Architecture, scale),
                Design = Scale(weights.Design, scale),
                Stability = Scale(weights.Stability, scale) + (session.ActionSummary?.TestRunCount > 0 ? 1 : 0),
                Velocity = Scale(weights.Velocity, scale),
                Creativity = Scale(weights.Creativity, scale),
                Efficiency = Scale(weights.Efficiency, scale) + (session.ResultStatus == ResultStatus.Succeeded && session.TokenUsageBucket == TokenUsageBucket.Small ? 1 : 0)
            };

            ApplyGitAggregateStatSignals(deltas, session);
            ApplyGitHistoryAxisScores(deltas, session);
            return deltas;
        }

        private static void ApplyGitHistoryAxisScores(CharacterStats deltas, AgentWorkSession session)
        {
            var summary = session?.GitChangeSummary;
            if (summary == null || !string.Equals(summary.GrowthScoringVersion, "git-growth-axes-v2", StringComparison.Ordinal))
            {
                return;
            }

            // A full repository analysis owns the five displayed axes. Do not blend these scores
            // with the generic WorkType rule weights: that blend was the source of identical,
            // fixed-looking summaries across unrelated repositories.
            deltas.Logic = Math.Max(0, summary.GrowthCodeScore);
            deltas.Architecture = 0;
            deltas.Velocity = 0;
            deltas.Efficiency = Math.Max(0, summary.GrowthFocusScore);
            deltas.Stability = 0;
            deltas.Debug = Math.Max(0, summary.GrowthDebugScore);
            deltas.Design = Math.Max(0, summary.GrowthDesignScore);
            deltas.Creativity = 0;
            deltas.Sync = Math.Max(0, summary.GrowthSyncScore);
        }

        private static void ApplyGitAggregateStatSignals(CharacterStats deltas, AgentWorkSession session)
        {
            if (!IsGitAggregateSession(session) || session.GitChangeSummary?.ExtensionCategoryBuckets == null)
            {
                return;
            }

            if (HasExtensionCategory(session.GitChangeSummary, "test"))
            {
                deltas.Stability += 1;
                deltas.Debug += 1;
            }

            if (HasExtensionCategory(session.GitChangeSummary, "markdown"))
            {
                deltas.Architecture += 1;
                deltas.Efficiency += 1;
            }

            if (HasExtensionCategory(session.GitChangeSummary, "config") || HasExtensionCategory(session.GitChangeSummary, "json"))
            {
                deltas.Stability += 1;
                deltas.Efficiency += 1;
            }

            if (session.GitChangeSummary.ChangedFileCountBucket == CountBucket.Huge ||
                session.GitChangeSummary.AddedLineBucket == LineChangeBucket.Huge ||
                session.GitChangeSummary.DeletedLineBucket == LineChangeBucket.Huge)
            {
                CapStats(deltas, 8);
            }

            if ((session.GitChangeSummary.PrivacyWarnings?.Count ?? 0) > 0 ||
                session.GitChangeSummary.ConfidenceLevel == ConfidenceLevel.Low)
            {
                CapStats(deltas, 5);
            }
        }

        private static int CalculateStressDelta(AgentWorkSession session, GrowthRule rule)
        {
            var stress = 0;
            if (session.ResultStatus == ResultStatus.Failed) stress += 4;
            if (session.ActionSummary?.FailedCommandCount > 0) stress += session.ActionSummary.FailedCommandCount;
            if (session.TokenUsageBucket == TokenUsageBucket.Large || session.TokenUsageBucket == TokenUsageBucket.Huge) stress += rule.StressPenalty;
            if (session.ResultStatus == ResultStatus.Succeeded && stress == 0) stress -= 1;
            return stress;
        }

        private static int Scale(int value, float scale)
        {
            return value <= 0 ? 0 : Math.Max(1, (int)Math.Round(value * scale));
        }

        private static bool IsGitAggregateSession(AgentWorkSession session)
        {
            return string.Equals(session?.GitChangeSummary?.AnalyzerVersion, "git-aggregate-v1", StringComparison.Ordinal) ||
                   string.Equals(session?.GitChangeSummary?.AnalyzerVersion, "git-aggregate-v2", StringComparison.Ordinal) ||
                   string.Equals(session?.SourceProvider, "GIT", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasExtensionCategory(GitChangeSummary summary, string category)
        {
            return (summary.ExtensionCategoryBuckets ?? new List<ExtensionCategoryCount>())
                .Any(item => string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase) && item.Count > 0);
        }

        private static void CapStats(CharacterStats stats, int max)
        {
            stats.Logic = Math.Min(stats.Logic, max);
            stats.Debug = Math.Min(stats.Debug, max);
            stats.Architecture = Math.Min(stats.Architecture, max);
            stats.Design = Math.Min(stats.Design, max);
            stats.Stability = Math.Min(stats.Stability, max);
            stats.Velocity = Math.Min(stats.Velocity, max);
            stats.Creativity = Math.Min(stats.Creativity, max);
            stats.Efficiency = Math.Min(stats.Efficiency, max);
        }

        private static EvolutionType InferEvolutionDelta(WorkType workType, CharacterStats delta)
        {
            switch (workType)
            {
                case WorkType.Bugfix:
                    return EvolutionType.Debugger;
                case WorkType.Test:
                    return EvolutionType.Guardian;
                case WorkType.Refactor:
                case WorkType.Docs:
                    return EvolutionType.CleanCodeArchitect;
                case WorkType.UIUX:
                    return EvolutionType.ProductBard;
                default:
                    if (delta.Efficiency >= delta.Logic && delta.Efficiency > 0) return EvolutionType.EfficiencyNinja;
                    return EvolutionType.Unknown;
            }
        }

        private static List<string> BuildRewardTags(AgentWorkSession session, bool capApplied)
        {
            var tags = new List<string> { session.WorkType.ToString() };
            if (session.ResultStatus == ResultStatus.Succeeded) tags.Add("Success");
            if (session.ActionSummary?.TestRunCount > 0) tags.Add("Tests");
            if (capApplied) tags.Add("DailyCap");
            return tags;
        }

        private static List<PrivacyWarning> BuildWarnings(AgentWorkSession session, float antiGrindingMultiplier, bool capApplied)
        {
            var warnings = new List<PrivacyWarning>();
            if (antiGrindingMultiplier < 1f)
            {
                warnings.Add(new PrivacyWarning
                {
                    Code = "growth_tiny_session",
                    Message = "Tiny session reward was reduced.",
                    Location = nameof(AgentWorkSession)
                });
            }

            if (capApplied)
            {
                warnings.Add(new PrivacyWarning
                {
                    Code = "growth_daily_cap",
                    Message = "Daily experience cap was applied.",
                    Location = nameof(GrowthCalculator)
                });
            }

            if (session.TokenUsageBucket == TokenUsageBucket.Huge)
            {
                warnings.Add(new PrivacyWarning
                {
                    Code = "growth_high_token_stress",
                    Message = "High token usage increased stress.",
                    Location = nameof(AgentWorkSession.TokenUsageBucket)
                });
            }

            return warnings;
        }
    }
}
