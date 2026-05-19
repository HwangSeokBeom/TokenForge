using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenForge.Client.Domain
{
    public sealed class CompanionProgressionRules
    {
        public const int SchemaVersion = 1;
        public const int HatchingXp = 250;
        public const int BabyXp = 500;
        public const int JuniorXp = 1200;
        public const int AdultXp = 2600;
        private const int XpPerCompanionLevel = 500;

        public static CompanionState CreateDefaultState()
        {
            return CompanionState.CreateDefault();
        }

        public static CompanionState Normalize(CompanionState state)
        {
            if (state == null)
            {
                return CreateDefaultState();
            }

            state.SchemaVersion = SchemaVersion;
            state.Level = Math.Max(1, state.Level);
            state.TotalXp = Math.Max(0, state.TotalXp);
            state.GrowthProfile = state.GrowthProfile ?? new CompanionGrowthProfile();
            state.GrowthProfile.SchemaVersion = SchemaVersion;
            state.GrowthProfile.TokenUsageProfile = state.GrowthProfile.TokenUsageProfile ?? new CompanionTokenUsageProfile();
            state.LastGrowthReasonIds = state.LastGrowthReasonIds ?? new List<string>();
            if (state.LastGrowthReasonIds.Count == 0)
            {
                state.LastGrowthReasonIds.Add(state.TotalXp <= 0 ? CompanionGrowthReasonIds.NoApprovedGrowthYet : CompanionGrowthReasonIds.ApprovedAggregateGrowth);
            }

            state.Stage = StageForXp(state.TotalXp);
            state.XpToNextStage = XpToNextStage(state.TotalXp);
            if (state.TotalXp <= 0)
            {
                state.Archetype = CompanionArchetype.Unknown;
            }

            return state;
        }

        public static CompanionState CalculateState(IEnumerable<AgentWorkSession> sessions, IEnumerable<CharacterGrowthResult> growthHistory)
        {
            var safeSessions = (sessions ?? new List<AgentWorkSession>()).Where(session => session != null).ToList();
            var growthBySessionId = (growthHistory ?? new List<CharacterGrowthResult>())
                .Where(growth => growth != null && !string.IsNullOrWhiteSpace(growth.SessionId))
                .GroupBy(growth => growth.SessionId)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
            var profile = BuildProfile(safeSessions, growthBySessionId);
            var totalXp = CalculateCompanionXp(growthBySessionId.Values, profile);
            var archetype = totalXp <= 0
                ? CompanionArchetype.Unknown
                : CompanionArchetypeResolver.Resolve(profile);

            return Normalize(new CompanionState
            {
                SchemaVersion = SchemaVersion,
                Stage = StageForXp(totalXp),
                Archetype = archetype,
                Level = Math.Max(1, totalXp / XpPerCompanionLevel + 1),
                TotalXp = totalXp,
                XpToNextStage = XpToNextStage(totalXp),
                GrowthProfile = profile,
                LastGrowthReasonIds = BuildReasonIds(profile, archetype, totalXp)
            });
        }

        public static CompanionStage StageForXp(int totalXp)
        {
            if (totalXp >= AdultXp) return CompanionStage.Adult;
            if (totalXp >= JuniorXp) return CompanionStage.Junior;
            if (totalXp >= BabyXp) return CompanionStage.Baby;
            if (totalXp >= HatchingXp) return CompanionStage.Hatching;
            return CompanionStage.Egg;
        }

        public static int XpToNextStage(int totalXp)
        {
            totalXp = Math.Max(0, totalXp);
            if (totalXp < HatchingXp) return HatchingXp - totalXp;
            if (totalXp < BabyXp) return BabyXp - totalXp;
            if (totalXp < JuniorXp) return JuniorXp - totalXp;
            if (totalXp < AdultXp) return AdultXp - totalXp;
            return 0;
        }

        private static CompanionGrowthProfile BuildProfile(
            List<AgentWorkSession> sessions,
            Dictionary<string, CharacterGrowthResult> growthBySessionId)
        {
            var profile = new CompanionGrowthProfile
            {
                SchemaVersion = SchemaVersion,
                ApprovedSessionCount = sessions.Count,
                TokenUsageProfile = new CompanionTokenUsageProfile()
            };
            var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var workTypes = new HashSet<WorkType>();
            var tokenBuckets = new List<TokenUsageBucket>();

            foreach (var session in sessions)
            {
                AddProviderSignals(providers, session);
                AddProviderCounts(profile, session);
                tokenBuckets.Add(session.TokenUsageBucket);
                if (session.WorkType != WorkType.Unknown)
                {
                    workTypes.Add(session.WorkType);
                }

                growthBySessionId.TryGetValue(session.SessionId ?? string.Empty, out var growth);
                ApplySessionScores(profile, session, growth);
            }

            profile.ExplorationScore += Math.Max(0, providers.Count - 1) * 3 + Math.Max(0, workTypes.Count - 1) * 2;
            profile.ProviderDiversityBucket = ToCountBucket(providers.Count);
            profile.WorkTypeDiversityBucket = ToCountBucket(workTypes.Count);
            profile.TokenUsageProfile.TotalTokenBucket = DominantTokenBucket(tokenBuckets);
            profile.TokenUsageProfile.InputTokenBucket = profile.TokenUsageProfile.TotalTokenBucket;
            profile.TokenUsageProfile.OutputTokenBucket = profile.TokenUsageProfile.TotalTokenBucket;
            profile.TokenUsageProfile.TokenIntensityBucket = HighestTokenBucket(tokenBuckets);
            profile.TokenUsageProfile.TokenTrendBucket = TokenTrendBucket(tokenBuckets);
            ApplyHistoricalConsistency(profile);
            profile.DominantStats = DominantStats(profile);
            return profile;
        }

        private static void AddProviderSignals(HashSet<string> providers, AgentWorkSession session)
        {
            if (!string.IsNullOrWhiteSpace(session.SourceProvider))
            {
                providers.Add(session.SourceProvider.Trim().ToUpperInvariant());
            }

            foreach (var provider in session.SourceProviders ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    providers.Add(provider.Trim().ToUpperInvariant());
                }
            }

            var providerType = session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown;
            if (providerType != AgentProviderType.Unknown)
            {
                providers.Add(providerType.ToString());
            }
        }

        private static void AddProviderCounts(CompanionGrowthProfile profile, AgentWorkSession session)
        {
            var provider = NormalizeProvider(session);
            switch (provider)
            {
                case AgentProviderType.Cursor:
                    profile.CursorProviderCount += 1;
                    break;
                case AgentProviderType.ClaudeCode:
                case AgentProviderType.Claude:
                    profile.ClaudeCodeProviderCount += 1;
                    break;
                case AgentProviderType.Codex:
                    profile.CodexProviderCount += 1;
                    break;
                case AgentProviderType.GitHubCopilot:
                    profile.CopilotProviderCount += 1;
                    break;
                case AgentProviderType.Manual:
                    profile.ManualProviderCount += 1;
                    break;
            }
        }

        private static void ApplySessionScores(CompanionGrowthProfile profile, AgentWorkSession session, CharacterGrowthResult growth)
        {
            var action = session.ActionSummary ?? AgentActionSummary.Empty();
            var git = session.GitChangeSummary ?? GitChangeSummary.Empty();
            var stat = growth?.StatDeltas ?? CharacterStats.Zero();
            var changedFileScore = CountBucketScore(git.ChangedFileCountBucket);
            var lineScore = LineBucketScore(git.AddedLineBucket) + LineBucketScore(git.DeletedLineBucket);
            var sessionWeight = Math.Max(1, (growth?.ExpGained ?? 0) / 80);

            if (session.WorkType == WorkType.Feature || session.WorkType == WorkType.Build || session.WorkType == WorkType.Mixed)
            {
                profile.ImplementationScore += 4 + sessionWeight;
            }

            if (action.FileEditCount > 0 || git.ChangedFileCount > 0)
            {
                profile.ImplementationScore += 1 + Math.Max(changedFileScore, 1);
            }

            if (git.CommitCountBucket == CountBucket.Small || git.CommitCountBucket == CountBucket.Medium || git.CommitCountBucket == CountBucket.Large)
            {
                profile.ImplementationScore += 2;
            }

            ApplyProviderScores(profile, session, git, action);
            ApplyGitStyleScores(profile, session, git, action);
            ApplyTokenScores(profile, session, git, action);

            if (session.WorkType == WorkType.Bugfix || session.WorkType == WorkType.Test)
            {
                profile.DebugScore += 4 + sessionWeight;
            }

            if (action.FailedCommandCount > 0 || action.TestRunCount > 0 || git.TestFileChanged)
            {
                profile.DebugScore += 2 + action.FailedCommandCount + Math.Min(3, action.TestRunCount);
            }

            if (stat.Debug > 0 || stat.Stability > 0)
            {
                profile.DebugScore += Math.Min(5, stat.Debug + stat.Stability);
            }

            if (session.WorkType == WorkType.Refactor || session.WorkType == WorkType.Docs || git.ArchitectureFileChanged || git.DocsFileChanged)
            {
                profile.StructureScore += 4 + sessionWeight;
            }

            if (HasExtensionCategory(git, "markdown") || HasExtensionCategory(git, "config") || HasExtensionCategory(git, "json"))
            {
                profile.StructureScore += 2;
            }

            if (stat.Architecture > 0 || stat.Design > 0)
            {
                profile.StructureScore += Math.Min(5, stat.Architecture + stat.Design);
            }

            var smallChange = IsSmallChange(git, action);
            if (smallChange)
            {
                profile.SmallChangeSessionCount += 1;
                profile.CleanupScore += 3;
            }

            if (session.WorkType == WorkType.Refactor || session.WorkType == WorkType.Chore || session.WorkType == WorkType.Docs)
            {
                profile.CleanupScore += 2;
            }

            if (session.WorkType == WorkType.Research || session.WorkType == WorkType.Mixed)
            {
                profile.ExplorationScore += 3;
            }

            var largeBurst = IsLargeBurst(git, session.TokenUsageBucket);
            if (largeBurst)
            {
                profile.LargeBurstSessionCount += 1;
                profile.BurstScore += 5 + lineScore + changedFileScore;
            }

            if ((action.DurationBucket == DurationBucket.Under5Minutes || action.DurationBucket == DurationBucket.FiveTo15Minutes) && largeBurst)
            {
                profile.BurstScore += 4;
            }
        }

        private static void ApplyProviderScores(CompanionGrowthProfile profile, AgentWorkSession session, GitChangeSummary git, AgentActionSummary action)
        {
            var provider = NormalizeProvider(session);
            switch (provider)
            {
                case AgentProviderType.Cursor:
                    if (session.WorkType == WorkType.Feature || session.WorkType == WorkType.Build || HasMeaningfulLocalActivity(git, action))
                    {
                        profile.ImplementationScore += 3;
                    }

                    if (profile.WorkTypeDiversityBucket == CountBucket.Small || HasDiverseCategories(git))
                    {
                        profile.ExplorationScore += 2;
                    }

                    break;
                case AgentProviderType.ClaudeCode:
                case AgentProviderType.Claude:
                    if (session.WorkType == WorkType.Docs || session.WorkType == WorkType.Refactor || HasExtensionCategory(git, "markdown") || HasExtensionCategory(git, "config"))
                    {
                        profile.StructureScore += 4;
                    }
                    else if (action.TestRunCount > 0 || action.FileEditCount <= 2)
                    {
                        profile.CleanupScore += 2;
                    }

                    break;
                case AgentProviderType.Codex:
                    if (session.WorkType == WorkType.Bugfix || session.WorkType == WorkType.Test || action.FailedCommandCount > 0 || git.TestFileChanged)
                    {
                        profile.DebugScore += 4;
                    }
                    else if (session.WorkType == WorkType.Refactor || session.WorkType == WorkType.Docs)
                    {
                        profile.StructureScore += 3;
                    }
                    else if (session.WorkType == WorkType.Feature || HasMeaningfulLocalActivity(git, action))
                    {
                        profile.ImplementationScore += 4;
                    }

                    break;
                case AgentProviderType.GitHubCopilot:
                    if (IsSmallChange(git, action))
                    {
                        profile.CleanupScore += 3;
                    }

                    if (action.DurationBucket == DurationBucket.Under5Minutes || action.DurationBucket == DurationBucket.FiveTo15Minutes)
                    {
                        profile.BurstScore += 2;
                    }

                    break;
            }
        }

        private static void ApplyGitStyleScores(CompanionGrowthProfile profile, AgentWorkSession session, GitChangeSummary git, AgentActionSummary action)
        {
            var smallCommitCadence = (git.CommitCountBucket == CountBucket.Small || git.CommitCountBucket == CountBucket.Medium) && IsSmallChange(git, action);
            if (smallCommitCadence)
            {
                profile.CleanupScore += 4;
            }

            if (git.CommitCountBucket == CountBucket.Large || git.CommitCountBucket == CountBucket.Huge)
            {
                profile.BurstScore += 4 + CountBucketScore(git.CommitCountBucket);
            }

            if (git.TestFileChanged || HasExtensionCategory(git, "test") || session.WorkType == WorkType.Test || session.WorkType == WorkType.Bugfix)
            {
                profile.DebugScore += 4;
            }

            if (git.DocsFileChanged || git.ArchitectureFileChanged || HasExtensionCategory(git, "markdown") || session.WorkType == WorkType.Docs || session.WorkType == WorkType.Refactor)
            {
                profile.StructureScore += 4;
            }

            if (HasDiverseCategories(git))
            {
                profile.ExplorationScore += 4;
            }
        }

        private static void ApplyTokenScores(CompanionGrowthProfile profile, AgentWorkSession session, GitChangeSummary git, AgentActionSummary action)
        {
            var tokenScore = TokenBucketScore(session.TokenUsageBucket);
            if (tokenScore <= 0)
            {
                return;
            }

            var hasLocalActivity = HasMeaningfulLocalActivity(git, action);
            if (!hasLocalActivity && tokenScore >= 3)
            {
                profile.HighTokenLowLocalActivityCount += 1;
                profile.CleanupScore += 1;
                return;
            }

            if (hasLocalActivity && tokenScore >= 2)
            {
                profile.BalancedTokenGitActivityCount += 1;
                profile.ImplementationScore += 1;
            }

            if (tokenScore >= 3 && (session.WorkType == WorkType.Refactor || session.WorkType == WorkType.Docs || git.DocsFileChanged || git.ArchitectureFileChanged))
            {
                profile.StructureScore += 3;
            }

            if (tokenScore >= 3 && (session.WorkType == WorkType.Test || session.WorkType == WorkType.Bugfix || git.TestFileChanged || action.FailedCommandCount > 0))
            {
                profile.DebugScore += 3;
            }

            if (tokenScore >= 3 && (session.WorkType == WorkType.Feature || session.WorkType == WorkType.Build || session.WorkType == WorkType.Mixed))
            {
                profile.ImplementationScore += 3;
            }
        }

        private static List<string> BuildReasonIds(CompanionGrowthProfile profile, CompanionArchetype archetype, int totalXp)
        {
            if (totalXp <= 0)
            {
                return new List<string> { CompanionGrowthReasonIds.NoApprovedGrowthYet };
            }

            var reasons = new List<string> { CompanionGrowthReasonIds.ApprovedAggregateGrowth };
            switch (archetype)
            {
                case CompanionArchetype.Explorer: reasons.Add(CompanionGrowthReasonIds.ProviderDiversityExplorerSignal); break;
                case CompanionArchetype.Builder: reasons.Add("implementation_commit_activity"); break;
                case CompanionArchetype.Debugger: reasons.Add(CompanionGrowthReasonIds.GitTestDebugDebuggerSignal); break;
                case CompanionArchetype.Refiner: reasons.Add(CompanionGrowthReasonIds.GitFrequentSmallCommitsRefinerSignal); break;
                case CompanionArchetype.Architect: reasons.Add(CompanionGrowthReasonIds.GitDocsRefactorArchitectSignal); break;
                case CompanionArchetype.Sprinter: reasons.Add(CompanionGrowthReasonIds.GitLargeBurstSprinterSignal); break;
            }

            if (profile.CursorProviderCount > 0 && profile.ImplementationScore >= profile.ExplorationScore) reasons.Add(CompanionGrowthReasonIds.ProviderCursorBuilderSignal);
            if (profile.ClaudeCodeProviderCount > 0 && profile.StructureScore > 0) reasons.Add(CompanionGrowthReasonIds.ProviderClaudeArchitectSignal);
            if (profile.CodexProviderCount > 0 && profile.ImplementationScore > 0) reasons.Add(CompanionGrowthReasonIds.ProviderCodexBuilderSignal);
            if (profile.CodexProviderCount > 0 && profile.DebugScore > 0) reasons.Add(CompanionGrowthReasonIds.ProviderCodexDebuggerSignal);
            if (profile.CopilotProviderCount > 0 && profile.CleanupScore > 0) reasons.Add(CompanionGrowthReasonIds.ProviderCopilotRefinerSignal);
            if (profile.SmallChangeSessionCount > 0) reasons.Add(CompanionGrowthReasonIds.GitFrequentSmallCommitsRefinerSignal);
            if (profile.LargeBurstSessionCount > 0) reasons.Add(CompanionGrowthReasonIds.GitLargeBurstSprinterSignal);
            if (profile.DebugScore > 0) reasons.Add(CompanionGrowthReasonIds.GitTestDebugDebuggerSignal);
            if (profile.StructureScore > 0) reasons.Add(CompanionGrowthReasonIds.GitDocsRefactorArchitectSignal);
            if (profile.BalancedTokenGitActivityCount > 0) reasons.Add(CompanionGrowthReasonIds.BalancedTokenGitGrowthBoost);
            if (profile.HighTokenLowLocalActivityCount > 0) reasons.Add(CompanionGrowthReasonIds.HighTokenLowLocalActivityCautiousGrowth);
            if (profile.ProviderDiversityBucket != CountBucket.None && profile.ProviderDiversityBucket != CountBucket.One && profile.ProviderDiversityBucket != CountBucket.Unknown) reasons.Add(CompanionGrowthReasonIds.ProviderDiversityExplorerSignal);
            if (profile.SteadyReviewSaveCount > 0) reasons.Add(CompanionGrowthReasonIds.SteadyReviewSaveConsistencyBonus);

            foreach (var stat in profile.DominantStats ?? new List<string>())
            {
                reasons.Add("dominant_" + stat.ToLowerInvariant());
            }

            return reasons.Distinct(StringComparer.Ordinal).Take(10).ToList();
        }

        private static List<string> DominantStats(CompanionGrowthProfile profile)
        {
            return new[]
                {
                    new { Name = "implementation", Value = profile.ImplementationScore },
                    new { Name = "debug", Value = profile.DebugScore },
                    new { Name = "structure", Value = profile.StructureScore },
                    new { Name = "cleanup", Value = profile.CleanupScore },
                    new { Name = "exploration", Value = profile.ExplorationScore },
                    new { Name = "burst", Value = profile.BurstScore }
                }
                .Where(item => item.Value > 0)
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .Take(3)
                .Select(item => item.Name)
                .ToList();
        }

        private static bool IsSmallChange(GitChangeSummary git, AgentActionSummary action)
        {
            var fileBucket = git.ChangedFileCountBucket;
            var changedFiles = git.ChangedFileCount > 0 ? git.ChangedFileCount : action.FileEditCount;
            var added = git.AddedLineBucket;
            var deleted = git.DeletedLineBucket;
            var smallFiles = changedFiles > 0 && changedFiles <= 3 ||
                             fileBucket == CountBucket.One ||
                             fileBucket == CountBucket.Small;
            var smallLines = (added == LineChangeBucket.None || added == LineChangeBucket.Small || added == LineChangeBucket.Unknown) &&
                             (deleted == LineChangeBucket.None || deleted == LineChangeBucket.Small || deleted == LineChangeBucket.Unknown);
            return smallFiles && smallLines;
        }

        private static bool IsLargeBurst(GitChangeSummary git, TokenUsageBucket tokenBucket)
        {
            return tokenBucket == TokenUsageBucket.Huge ||
                   git.ChangedFileCountBucket == CountBucket.Large ||
                   git.ChangedFileCountBucket == CountBucket.Huge ||
                   git.AddedLineBucket == LineChangeBucket.Large ||
                   git.AddedLineBucket == LineChangeBucket.Huge ||
                   git.DeletedLineBucket == LineChangeBucket.Large ||
                   git.DeletedLineBucket == LineChangeBucket.Huge;
        }

        private static bool HasExtensionCategory(GitChangeSummary summary, string category)
        {
            return (summary.ExtensionCategoryBuckets ?? new List<ExtensionCategoryCount>())
                .Any(item => string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase) && item.Count > 0);
        }

        private static bool HasDiverseCategories(GitChangeSummary summary)
        {
            var fileCategories = (summary.FileCategoryCounts ?? new List<FileCategoryCount>())
                .Count(item => item.Category != FileCategory.Unknown && item.Count > 0);
            var extensionCategories = (summary.ExtensionCategoryBuckets ?? new List<ExtensionCategoryCount>())
                .Count(item => !string.IsNullOrWhiteSpace(item.Category) && item.Count > 0);
            return fileCategories >= 3 || extensionCategories >= 3;
        }

        private static bool HasMeaningfulLocalActivity(GitChangeSummary git, AgentActionSummary action)
        {
            return git.ChangedFileCount > 0 ||
                   git.ChangedFileCountBucket == CountBucket.One ||
                   git.ChangedFileCountBucket == CountBucket.Small ||
                   git.ChangedFileCountBucket == CountBucket.Medium ||
                   git.ChangedFileCountBucket == CountBucket.Large ||
                   git.ChangedFileCountBucket == CountBucket.Huge ||
                   action.FileEditCount > 0 ||
                   action.TestRunCount > 0 ||
                   action.BuildRunCount > 0;
        }

        private static AgentProviderType NormalizeProvider(AgentWorkSession session)
        {
            var provider = session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown;
            if (provider != AgentProviderType.Unknown)
            {
                return provider;
            }

            if (!string.IsNullOrWhiteSpace(session.SourceProvider))
            {
                if (TryParseProvider(session.SourceProvider, out provider))
                {
                    return provider;
                }
            }

            foreach (var sourceProvider in session.SourceProviders ?? new List<string>())
            {
                if (TryParseProvider(sourceProvider, out provider))
                {
                    return provider;
                }
            }

            switch (session.AgentType)
            {
                case AgentType.Cursor: return AgentProviderType.Cursor;
                case AgentType.ClaudeCode: return AgentProviderType.ClaudeCode;
                case AgentType.Codex: return AgentProviderType.Codex;
                case AgentType.GitHubCopilot: return AgentProviderType.GitHubCopilot;
                case AgentType.ManualFallback: return AgentProviderType.Manual;
                default: return AgentProviderType.Unknown;
            }
        }

        private static bool TryParseProvider(string value, out AgentProviderType provider)
        {
            provider = AgentProviderType.Unknown;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
            switch (normalized)
            {
                case "CURSOR":
                    provider = AgentProviderType.Cursor;
                    return true;
                case "CLAUDE":
                case "CLAUDECODE":
                    provider = AgentProviderType.ClaudeCode;
                    return true;
                case "CODEX":
                    provider = AgentProviderType.Codex;
                    return true;
                case "GITHUBCOPILOT":
                case "COPILOT":
                    provider = AgentProviderType.GitHubCopilot;
                    return true;
                case "MANUAL":
                case "OTHER":
                case "MANUALFALLBACK":
                    provider = AgentProviderType.Manual;
                    return true;
                default:
                    return false;
            }
        }

        private static int CalculateCompanionXp(IEnumerable<CharacterGrowthResult> growthHistory, CompanionGrowthProfile profile)
        {
            var baseXp = Math.Max(0, (growthHistory ?? new List<CharacterGrowthResult>()).Sum(growth => Math.Max(0, growth.ExpGained)));
            var boost = Math.Max(0, profile.BalancedTokenGitActivityCount) * 30 + Math.Max(0, profile.SteadyReviewSaveCount) * 10;
            var caution = Math.Max(0, profile.HighTokenLowLocalActivityCount) * 20;
            return Math.Max(0, baseXp + boost - caution);
        }

        private static void ApplyHistoricalConsistency(CompanionGrowthProfile profile)
        {
            if (profile.ApprovedSessionCount < 3 || profile.SmallChangeSessionCount < 2)
            {
                return;
            }

            profile.SteadyReviewSaveCount = Math.Min(profile.ApprovedSessionCount, profile.SmallChangeSessionCount);
            profile.CleanupScore += 2 + profile.SteadyReviewSaveCount;
        }

        private static TokenUsageBucket DominantTokenBucket(List<TokenUsageBucket> buckets)
        {
            return (buckets ?? new List<TokenUsageBucket>())
                .Where(bucket => bucket != TokenUsageBucket.Unknown)
                .GroupBy(bucket => bucket)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => TokenBucketScore(group.Key))
                .Select(group => group.Key)
                .FirstOrDefault();
        }

        private static TokenUsageBucket HighestTokenBucket(List<TokenUsageBucket> buckets)
        {
            return (buckets ?? new List<TokenUsageBucket>())
                .OrderByDescending(TokenBucketScore)
                .FirstOrDefault();
        }

        private static CompanionTokenTrendBucket TokenTrendBucket(List<TokenUsageBucket> buckets)
        {
            if (buckets == null || buckets.Count < 3)
            {
                return CompanionTokenTrendBucket.Unknown;
            }

            var first = TokenBucketScore(buckets[0]);
            var last = TokenBucketScore(buckets[buckets.Count - 1]);
            if (last >= first + 2) return CompanionTokenTrendBucket.Rising;
            if (first >= last + 2) return CompanionTokenTrendBucket.Falling;
            return CompanionTokenTrendBucket.Flat;
        }

        private static int TokenBucketScore(TokenUsageBucket bucket)
        {
            switch (bucket)
            {
                case TokenUsageBucket.Small: return 1;
                case TokenUsageBucket.Medium: return 2;
                case TokenUsageBucket.Large: return 3;
                case TokenUsageBucket.Huge: return 4;
                default: return 0;
            }
        }

        private static int CountBucketScore(CountBucket bucket)
        {
            switch (bucket)
            {
                case CountBucket.One: return 1;
                case CountBucket.Small: return 2;
                case CountBucket.Medium: return 3;
                case CountBucket.Large: return 5;
                case CountBucket.Huge: return 8;
                default: return 0;
            }
        }

        private static int LineBucketScore(LineChangeBucket bucket)
        {
            switch (bucket)
            {
                case LineChangeBucket.Small: return 1;
                case LineChangeBucket.Medium: return 2;
                case LineChangeBucket.Large: return 4;
                case LineChangeBucket.Huge: return 7;
                default: return 0;
            }
        }

        private static CountBucket ToCountBucket(int count)
        {
            if (count <= 0) return CountBucket.None;
            if (count == 1) return CountBucket.One;
            if (count <= 5) return CountBucket.Small;
            if (count <= 20) return CountBucket.Medium;
            if (count <= 50) return CountBucket.Large;
            return CountBucket.Huge;
        }
    }
}
