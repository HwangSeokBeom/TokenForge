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
            var resultMultiplier = GetResultMultiplier(session.ResultStatus, rule);
            var baseExp = rule.BaseExp * tokenMultiplier * fileMultiplier * resultMultiplier;

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
            var statDeltas = CalculateStatDeltas(rule.StatWeights, tokenMultiplier, fileMultiplier, session);
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
                RewardTags = BuildRewardTags(session, capApplied),
                DailyCapApplied = capApplied
            };
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
            return new CharacterStats
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
    }
}
