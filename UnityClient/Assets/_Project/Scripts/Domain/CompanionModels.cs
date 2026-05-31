using System;
using System.Collections.Generic;

namespace TokenForge.Client.Domain
{
    public enum CompanionStage
    {
        Egg,
        Hatching,
        Baby,
        Junior,
        Adult
    }

    public enum CompanionArchetype
    {
        Unknown,
        Explorer,
        Builder,
        Debugger,
        Refiner,
        Architect,
        Sprinter
    }

    public enum CompanionAnimationState
    {
        Idle,
        Wander,
        Hop,
        HatchShake,
        GrowthPulse,
        TapReaction,
        HappyReaction,
        Sleep
    }

    public enum CompanionDesktopMotionMode
    {
        Calm,
        Normal,
        Playful
    }

    public enum CompanionMotionMode
    {
        Idle,
        Wandering,
        Dragged,
        Reacting,
        Sleeping
    }

    public enum CompanionInteractionMode
    {
        ClickThrough,
        Interactive
    }

    public enum CompanionReaction
    {
        Tap,
        Happy,
        Wake,
        LevelUp,
        HungryNoActivity,
        AnalysisComplete,
        Attention,
        GrowthSaved,
        AiAssisted,
        Warning
    }

    public enum CompanionActivityLevel
    {
        Idle,
        Low,
        Medium,
        High,
        ReadyToEvolve,
        Warning
    }

    public enum CompanionMotionReaction
    {
        None,
        CalmWander,
        FocusedWander,
        AiPulse,
        TokenPulse,
        ReadyToReview,
        EvolvePulse,
        GrowthSaved,
        LevelUp,
        WarningShake
    }

    public enum CompanionDesktopOverlayState
    {
        Unavailable,
        Disabled,
        Active,
        Fallback
    }

    public enum CompanionGrowthStat
    {
        Unknown,
        Code,
        Focus,
        Debug,
        Design,
        Sync
    }

    public enum CompanionTokenTrendBucket
    {
        Unknown,
        Flat,
        Rising,
        Falling
    }

    [Serializable]
    public sealed class DesktopCompanionSettings
    {
        public int SchemaVersion { get; set; } = 2;
        public bool IsDesktopCompanionEnabled { get; set; } = true;
        public CompanionDesktopMotionMode MotionMode { get; set; } = CompanionDesktopMotionMode.Normal;
        public bool IsClickThroughEnabled { get; set; }
        public CountBucket LastOverlayPositionXBucket { get; set; } = CountBucket.Unknown;
        public CountBucket LastOverlayPositionYBucket { get; set; } = CountBucket.Unknown;
        public float LastOverlayPositionX { get; set; } = -1f;
        public float LastOverlayPositionY { get; set; } = -1f;
        public bool HasSavedOverlayPosition { get; set; }
        public string VisualThemeId { get; set; } = CompanionSkinCatalog.DefaultSkinId;

        public static DesktopCompanionSettings CreateDefault()
        {
            return new DesktopCompanionSettings();
        }
    }

    public static class CompanionSkinCatalog
    {
        public const string DefaultSkinId = "orange_cat";

        private static readonly HashSet<string> KnownSkinIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "orange_cat",
            "white_cat",
            "calico",
            "black_cat",
            "retriever",
            "runner",
            "midnight",
            "aurora"
        };

        public static string Normalize(string skinId)
        {
            skinId = string.IsNullOrWhiteSpace(skinId) ? DefaultSkinId : skinId.Trim();
            if (string.Equals(skinId, "pixel-default", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultSkinId;
            }

            return KnownSkinIds.Contains(skinId) ? skinId.ToLowerInvariant() : DefaultSkinId;
        }
    }

    [Serializable]
    public sealed class CompanionMotionProfile
    {
        public CompanionMotionMode DefaultMode { get; set; } = CompanionMotionMode.Idle;
        public float IdleRadius { get; set; } = 4f;
        public float WanderRadius { get; set; } = 12f;
        public float WanderSpeed { get; set; } = 8f;
        public float DecisionIntervalSeconds { get; set; } = 4f;
        public bool AllowsWandering { get; set; }
    }

    [Serializable]
    public sealed class CompanionMotionState
    {
        public string RepositoryId { get; set; } = string.Empty;
        public CompanionActivityLevel ActivityLevel { get; set; } = CompanionActivityLevel.Idle;
        public float MovementSpeed { get; set; } = 0.35f;
        public float BounceAmplitude { get; set; } = 2.0f;
        public float IdleFrequency { get; set; } = 0.6f;
        public float PulseFrequency { get; set; } = 0.2f;
        public CompanionMotionReaction Reaction { get; set; } = CompanionMotionReaction.None;
        public string Mood { get; set; } = "idle";
        public string ReasonSummary { get; set; } = "No recent aggregate activity.";
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public static CompanionMotionState Idle(string repositoryId)
        {
            return new CompanionMotionState { RepositoryId = repositoryId ?? string.Empty };
        }
    }

    [Serializable]
    public sealed class CompanionMotionSignal
    {
        public string RepositoryId { get; set; } = string.Empty;
        public CountBucket RecentGitChangedFiles { get; set; } = CountBucket.Unknown;
        public CountBucket CommitCount { get; set; } = CountBucket.Unknown;
        public LineChangeBucket AddedLines { get; set; } = LineChangeBucket.Unknown;
        public LineChangeBucket DeletedLines { get; set; } = LineChangeBucket.Unknown;
        public int RecentRepositoryXp { get; set; }
        public CountBucket AiAgentSessionCount { get; set; } = CountBucket.Unknown;
        public CountBucket AiAgentInteractionCount { get; set; } = CountBucket.Unknown;
        public TokenUsageBucket EstimatedTokenActivity { get; set; } = TokenUsageBucket.Unknown;
        public int AiAgentXp { get; set; }
        public bool HasPendingReview { get; set; }
        public bool CanLevelUp { get; set; }
        public bool HasWarnings { get; set; }
        public CompanionMotionReaction ForcedReaction { get; set; } = CompanionMotionReaction.None;
    }

    public static class CompanionMotionStateResolver
    {
        public static CompanionMotionState Resolve(CompanionMotionSignal signal)
        {
            signal = signal ?? new CompanionMotionSignal();
            var gitScore = CountBucketScore(signal.RecentGitChangedFiles) +
                           CountBucketScore(signal.CommitCount) +
                           LineBucketScore(signal.AddedLines) +
                           LineBucketScore(signal.DeletedLines) +
                           Math.Min(4, Math.Max(0, signal.RecentRepositoryXp) / 100);
            var aiScore = CountBucketScore(signal.AiAgentSessionCount) +
                          CountBucketScore(signal.AiAgentInteractionCount) +
                          TokenBucketScore(signal.EstimatedTokenActivity) +
                          Math.Min(4, Math.Max(0, signal.AiAgentXp) / 100);
            var total = gitScore + aiScore;
            var state = CompanionMotionState.Idle(signal.RepositoryId);

            if (signal.HasWarnings)
            {
                state.ActivityLevel = CompanionActivityLevel.Warning;
                state.Reaction = CompanionMotionReaction.WarningShake;
                state.Mood = "cautious";
                state.MovementSpeed = 0.55f;
                state.BounceAmplitude = 4.0f;
                state.PulseFrequency = 0.8f;
                state.ReasonSummary = "Warnings need attention.";
            }
            else if (signal.CanLevelUp)
            {
                state.ActivityLevel = CompanionActivityLevel.ReadyToEvolve;
                state.Reaction = CompanionMotionReaction.EvolvePulse;
                state.Mood = "readyToEvolve";
                state.MovementSpeed = 0.85f;
                state.BounceAmplitude = 8.0f;
                state.IdleFrequency = 1.1f;
                state.PulseFrequency = 1.4f;
                state.ReasonSummary = "Enough XP is stored for evolution.";
            }
            else if (signal.HasPendingReview)
            {
                state.ActivityLevel = CompanionActivityLevel.Medium;
                state.Reaction = CompanionMotionReaction.ReadyToReview;
                state.Mood = "attention";
                state.MovementSpeed = 0.75f;
                state.BounceAmplitude = 6.0f;
                state.PulseFrequency = 1.0f;
                state.ReasonSummary = "A pending review is ready.";
            }
            else if (total >= 10)
            {
                state.ActivityLevel = CompanionActivityLevel.High;
                state.Reaction = aiScore > gitScore ? CompanionMotionReaction.TokenPulse : CompanionMotionReaction.FocusedWander;
                state.Mood = aiScore > gitScore ? "thinking" : "focused";
                state.MovementSpeed = 1.2f;
                state.BounceAmplitude = 7.0f;
                state.IdleFrequency = 1.25f;
                state.PulseFrequency = aiScore > 0 ? 1.1f : 0.6f;
                state.ReasonSummary = aiScore > gitScore ? "High AI/token aggregate activity." : "High Git aggregate activity.";
            }
            else if (total >= 5)
            {
                state.ActivityLevel = CompanionActivityLevel.Medium;
                state.Reaction = aiScore > 0 ? CompanionMotionReaction.AiPulse : CompanionMotionReaction.FocusedWander;
                state.Mood = aiScore > 0 ? "aiAssisted" : "focused";
                state.MovementSpeed = 0.85f;
                state.BounceAmplitude = 5.0f;
                state.IdleFrequency = 0.95f;
                state.PulseFrequency = aiScore > 0 ? 0.9f : 0.4f;
                state.ReasonSummary = aiScore > 0 ? "AI agent aggregate activity detected." : "Moderate Git aggregate activity.";
            }
            else if (total > 0)
            {
                state.ActivityLevel = CompanionActivityLevel.Low;
                state.Reaction = CompanionMotionReaction.CalmWander;
                state.Mood = "calm";
                state.MovementSpeed = 0.55f;
                state.BounceAmplitude = 3.0f;
                state.IdleFrequency = 0.75f;
                state.PulseFrequency = 0.25f;
                state.ReasonSummary = "Light aggregate activity detected.";
            }

            if (signal.ForcedReaction != CompanionMotionReaction.None)
            {
                state.Reaction = signal.ForcedReaction;
                if (signal.ForcedReaction == CompanionMotionReaction.GrowthSaved)
                {
                    state.Mood = "growthSaved";
                    state.PulseFrequency = Math.Max(state.PulseFrequency, 1.0f);
                    state.BounceAmplitude = Math.Max(state.BounceAmplitude, 6.0f);
                    state.ReasonSummary = "Growth was saved.";
                }
                else if (signal.ForcedReaction == CompanionMotionReaction.LevelUp)
                {
                    state.Mood = "levelUp";
                    state.PulseFrequency = Math.Max(state.PulseFrequency, 1.4f);
                    state.BounceAmplitude = Math.Max(state.BounceAmplitude, 9.0f);
                    state.ReasonSummary = "Evolution was saved.";
                }
            }

            state.UpdatedAt = DateTimeOffset.UtcNow;
            return state;
        }

        private static int CountBucketScore(CountBucket bucket)
        {
            switch (bucket)
            {
                case CountBucket.One: return 1;
                case CountBucket.Small: return 2;
                case CountBucket.Medium: return 4;
                case CountBucket.Large: return 6;
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
                case LineChangeBucket.Huge: return 6;
                default: return 0;
            }
        }

        private static int TokenBucketScore(TokenUsageBucket bucket)
        {
            switch (bucket)
            {
                case TokenUsageBucket.Small: return 1;
                case TokenUsageBucket.Medium: return 3;
                case TokenUsageBucket.Large: return 5;
                case TokenUsageBucket.Huge: return 7;
                default: return 0;
            }
        }
    }

    [Serializable]
    public sealed class CompanionReactionProfile
    {
        public CompanionReaction PrimaryClickReaction { get; set; } = CompanionReaction.Tap;
        public float CooldownSeconds { get; set; } = 1.1f;
        public string DefaultSpeech { get; set; } = "Waiting for the first safe summary.";
    }

    [Serializable]
    public sealed class CompanionVisualProfile
    {
        public CompanionStage Stage { get; set; } = CompanionStage.Egg;
        public CompanionArchetype Archetype { get; set; } = CompanionArchetype.Unknown;
        public string MainSpriteKey { get; set; } = "companion.sprite.egg.pixel";
        public string MenuBarIconKey { get; set; } = "companion.status.egg.pixel";
        public string DashboardHeroImageKey { get; set; } = "companion.hero.egg.pixel";
        public CompanionAnimationState IdleAnimation { get; set; } = CompanionAnimationState.Idle;
        public CompanionMotionProfile MotionProfile { get; set; } = new CompanionMotionProfile();
        public CompanionReactionProfile ReactionProfile { get; set; } = new CompanionReactionProfile();
    }

    public static class CompanionVisualProfileResolver
    {
        public static CompanionVisualProfile Resolve(CompanionState state, CompanionDesktopMotionMode motionMode = CompanionDesktopMotionMode.Normal)
        {
            state = CompanionProgressionRules.Normalize(state);
            var profile = new CompanionVisualProfile
            {
                Stage = state.Stage,
                Archetype = state.Archetype,
                MainSpriteKey = SpriteKey(state.Stage),
                MenuBarIconKey = StatusIconKey(state.Stage),
                DashboardHeroImageKey = HeroImageKey(state.Stage),
                IdleAnimation = IdleAnimationFor(state.Stage),
                MotionProfile = MotionProfileFor(state.Stage, motionMode),
                ReactionProfile = ReactionProfileFor(state.Stage)
            };

            return profile;
        }

        private static CompanionAnimationState IdleAnimationFor(CompanionStage stage)
        {
            switch (stage)
            {
                case CompanionStage.Hatching: return CompanionAnimationState.HatchShake;
                case CompanionStage.Baby:
                case CompanionStage.Junior:
                case CompanionStage.Adult:
                    return CompanionAnimationState.Wander;
                default:
                    return CompanionAnimationState.Idle;
            }
        }

        private static CompanionMotionProfile MotionProfileFor(CompanionStage stage, CompanionDesktopMotionMode mode)
        {
            if (mode == CompanionDesktopMotionMode.Calm)
            {
                return new CompanionMotionProfile
                {
                    DefaultMode = CompanionMotionMode.Idle,
                    IdleRadius = 0f,
                    WanderRadius = 0f,
                    WanderSpeed = 0f,
                    DecisionIntervalSeconds = 10f,
                    AllowsWandering = false
                };
            }

            var scale = mode == CompanionDesktopMotionMode.Calm ? 0.65f : mode == CompanionDesktopMotionMode.Playful ? 1.25f : 1f;
            switch (stage)
            {
                case CompanionStage.Hatching:
                    return new CompanionMotionProfile { DefaultMode = CompanionMotionMode.Wandering, IdleRadius = 7f * scale, WanderRadius = 24f * scale, WanderSpeed = 7f * scale, DecisionIntervalSeconds = 2.4f, AllowsWandering = true };
                case CompanionStage.Baby:
                    return new CompanionMotionProfile { DefaultMode = CompanionMotionMode.Wandering, IdleRadius = 6f * scale, WanderRadius = 64f * scale, WanderSpeed = 15f * scale, DecisionIntervalSeconds = 3.2f, AllowsWandering = true };
                case CompanionStage.Junior:
                    return new CompanionMotionProfile { DefaultMode = CompanionMotionMode.Wandering, IdleRadius = 5f * scale, WanderRadius = 140f * scale, WanderSpeed = 23f * scale, DecisionIntervalSeconds = 3.8f, AllowsWandering = true };
                case CompanionStage.Adult:
                    return new CompanionMotionProfile { DefaultMode = CompanionMotionMode.Wandering, IdleRadius = 5f * scale, WanderRadius = 220f * scale, WanderSpeed = 30f * scale, DecisionIntervalSeconds = 4.4f, AllowsWandering = true };
                default:
                    return new CompanionMotionProfile { DefaultMode = CompanionMotionMode.Wandering, IdleRadius = 5f * scale, WanderRadius = 28f * scale, WanderSpeed = 5f * scale, DecisionIntervalSeconds = 4.6f, AllowsWandering = true };
            }
        }

        private static CompanionReactionProfile ReactionProfileFor(CompanionStage stage)
        {
            switch (stage)
            {
                case CompanionStage.Hatching:
                    return new CompanionReactionProfile { PrimaryClickReaction = CompanionReaction.Wake, CooldownSeconds = 1.0f, DefaultSpeech = "Something is moving inside." };
                case CompanionStage.Baby:
                case CompanionStage.Junior:
                case CompanionStage.Adult:
                    return new CompanionReactionProfile { PrimaryClickReaction = CompanionReaction.Happy, CooldownSeconds = 0.9f, DefaultSpeech = "Ready for the next safe review." };
                default:
                    return new CompanionReactionProfile { PrimaryClickReaction = CompanionReaction.Tap, CooldownSeconds = 1.1f, DefaultSpeech = "First safe summary will start growth." };
            }
        }

        private static string SpriteKey(CompanionStage stage)
        {
            return "companion.sprite." + stage.ToString().ToLowerInvariant() + ".pixel";
        }

        private static string StatusIconKey(CompanionStage stage)
        {
            return "companion.status." + stage.ToString().ToLowerInvariant() + ".pixel";
        }

        private static string HeroImageKey(CompanionStage stage)
        {
            return "companion.hero." + stage.ToString().ToLowerInvariant() + ".pixel";
        }
    }

    [Serializable]
    public sealed class CompanionTokenUsageProfile
    {
        public TokenUsageBucket InputTokenBucket { get; set; } = TokenUsageBucket.Unknown;
        public TokenUsageBucket OutputTokenBucket { get; set; } = TokenUsageBucket.Unknown;
        public TokenUsageBucket TotalTokenBucket { get; set; } = TokenUsageBucket.Unknown;
        public TokenUsageBucket TokenIntensityBucket { get; set; } = TokenUsageBucket.Unknown;
        public CompanionTokenTrendBucket TokenTrendBucket { get; set; } = CompanionTokenTrendBucket.Unknown;
    }

    [Serializable]
    public sealed class CompanionGrowthProfile
    {
        public int SchemaVersion { get; set; } = 1;
        public int ApprovedSessionCount { get; set; }
        public int ImplementationScore { get; set; }
        public int DebugScore { get; set; }
        public int StructureScore { get; set; }
        public int CleanupScore { get; set; }
        public int ExplorationScore { get; set; }
        public int BurstScore { get; set; }
        public int SmallChangeSessionCount { get; set; }
        public int LargeBurstSessionCount { get; set; }
        public int HighTokenLowLocalActivityCount { get; set; }
        public int BalancedTokenGitActivityCount { get; set; }
        public int SteadyReviewSaveCount { get; set; }
        public int CursorProviderCount { get; set; }
        public int ClaudeCodeProviderCount { get; set; }
        public int CodexProviderCount { get; set; }
        public int CopilotProviderCount { get; set; }
        public int ManualProviderCount { get; set; }
        public CountBucket ProviderDiversityBucket { get; set; } = CountBucket.Unknown;
        public CountBucket WorkTypeDiversityBucket { get; set; } = CountBucket.Unknown;
        public CompanionTokenUsageProfile TokenUsageProfile { get; set; } = new CompanionTokenUsageProfile();
        public List<string> DominantStats { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class CompanionStatProfile
    {
        public int CodeStat { get; set; }
        public int FocusStat { get; set; }
        public int DebugStat { get; set; }
        public int DesignStat { get; set; }
        public int SyncStat { get; set; }

        public static CompanionStatProfile Empty()
        {
            return new CompanionStatProfile();
        }
    }

    [Serializable]
    public sealed class CompanionEvolutionBias
    {
        public CompanionGrowthStat DominantStat { get; set; } = CompanionGrowthStat.Unknown;
        public CompanionGrowthStat SecondaryStat { get; set; } = CompanionGrowthStat.Unknown;
        public string MainPath { get; set; } = "Unknown";
        public string SecondaryTrait { get; set; } = "Unknown";
        public string CurrentBias { get; set; } = "Unknown";
        public string NextEvolutionPreview { get; set; } = "Repository Hatchling";
        public string EggInfluenceText { get; set; } = "아직 부화 전이에요. 최근 Git 성장 성향이 미래 진화 방향에 영향을 줍니다.";
    }

    [Serializable]
    public sealed class TokenShopState
    {
        public string CurrencyName { get; set; } = "Forge Coins";
        public int CurrencyBalance { get; set; }
        public int LifetimeTokenUsageScore { get; set; }
        public bool TrackLocalAiTokenUsage { get; set; } = true;
        public List<string> PurchasedItemIds { get; set; } = new List<string>();
        public List<string> EquippedItemIds { get; set; } = new List<string>();
        public List<TokenShopPurchaseHistoryEntry> PurchaseHistory { get; set; } = new List<TokenShopPurchaseHistoryEntry>();
    }

    public enum ShopTargetType
    {
        RepositoryCompanion,
        AiAgent
    }

    public enum ZodiacCompanionType
    {
        Rat,
        Ox,
        Tiger,
        Rabbit,
        Dragon,
        Snake,
        Horse,
        Goat,
        Monkey,
        Rooster,
        Dog,
        Pig
    }

    public enum ShopItemCategory
    {
        Featured,
        Skins,
        Accessories,
        Effects,
        Motions,
        Themes,
        Owned,
        Badges,
        TokenEffects
    }

    public enum ShopItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum ShopOwnershipState
    {
        Available,
        Owned,
        Equipped,
        Locked,
        InsufficientBalance,
        NotCompatible
    }

    [Serializable]
    public sealed class ShopCatalogItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Price { get; set; }
        public bool OneTimePurchase { get; set; } = true;
        public string PreviewEffect { get; set; } = "Cosmetic preview";
        public string VisualThemeId { get; set; } = string.Empty;
        public ShopTargetType TargetType { get; set; } = ShopTargetType.RepositoryCompanion;
        public ShopItemCategory Category { get; set; } = ShopItemCategory.Skins;
        public ShopItemRarity Rarity { get; set; } = ShopItemRarity.Common;
        public bool Featured { get; set; }
        public string ItemType { get; set; } = "Skin";
        public string PreviewIcon { get; set; } = "TF";
        public string Compatibility { get; set; } = "Repository Companion";
        public List<string> CompatibleAgentIds { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class TokenShopItemDefinition
    {
        public string ItemId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Price { get; set; }
        public bool OneTimePurchase { get; set; } = true;
        public string PreviewEffect { get; set; } = "Cosmetic preview";
        public string VisualThemeId { get; set; } = string.Empty;
        public ShopTargetType TargetType { get; set; } = ShopTargetType.RepositoryCompanion;
        public ShopItemCategory Category { get; set; } = ShopItemCategory.Skins;
        public ShopItemRarity Rarity { get; set; } = ShopItemRarity.Common;
        public bool Featured { get; set; }
        public string ItemType { get; set; } = "Skin";
        public string PreviewIcon { get; set; } = "TF";
        public string PreviewType { get; set; } = "generic";
        public string Compatibility { get; set; } = "Repository Companion";
        public List<string> CompatibleAgentIds { get; set; } = new List<string>();
        public string ZodiacTypeId { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class AiAgentShopState
    {
        public string AgentId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ZodiacTypeId { get; set; } = string.Empty;
        public TokenShopState TokenShop { get; set; } = new TokenShopState();
    }

    [Serializable]
    public sealed class ZodiacCompanionDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string KoreanName { get; set; } = string.Empty;
        public string EnglishName { get; set; } = string.Empty;
        public string Personality { get; set; } = string.Empty;
        public string BasePalette { get; set; } = string.Empty;
        public string SilhouetteHint { get; set; } = string.Empty;
        public string EvolutionStageMapping { get; set; } = "egg,hatchling,companion";
    }

    [Serializable]
    public sealed class TokenShopPurchaseHistoryEntry
    {
        public string ItemId { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public int Price { get; set; }
        public DateTimeOffset PurchasedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    [Serializable]
    public sealed class TokenShopPurchaseResult
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CurrencyName { get; set; } = "Forge Coins";
        public int BalanceBefore { get; set; }
        public int BalanceAfter { get; set; }
        public bool Purchased { get; set; }
        public bool Owned { get; set; }
        public bool Equipped { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class CompanionState
    {
        public int SchemaVersion { get; set; } = 1;
        public CompanionStage Stage { get; set; } = CompanionStage.Egg;
        public CompanionArchetype Archetype { get; set; } = CompanionArchetype.Unknown;
        public int Level { get; set; } = 1;
        public int TotalXp { get; set; }
        public int CurrentXp { get; set; }
        public int TotalLifetimeXp { get; set; }
        public int XpRequiredForNextLevel { get; set; } = 250;
        public bool CanLevelUp { get; set; }
        public int XpToNextStage { get; set; } = 250;
        public CompanionStatProfile Stats { get; set; } = CompanionStatProfile.Empty();
        public CompanionStatProfile WeeklyStats { get; set; } = CompanionStatProfile.Empty();
        public CompanionEvolutionBias EvolutionBias { get; set; } = new CompanionEvolutionBias();
        public CompanionGrowthProfile GrowthProfile { get; set; } = new CompanionGrowthProfile();
        public List<string> LastGrowthReasonIds { get; set; } = new List<string> { "no_approved_growth_yet" };

        public static CompanionState CreateDefault()
        {
            return new CompanionState();
        }
    }

    [Serializable]
    public sealed class SourceProviderMixEntry
    {
        public string Provider { get; set; } = string.Empty;
        public int ApprovedAggregateCount { get; set; }
    }

    [Serializable]
    public sealed class RepositoryCompanionProfile
    {
        public int SchemaVersion { get; set; } = 1;
        public string RepositoryHash { get; set; } = string.Empty;
        public string SafeRepositoryAlias { get; set; } = "Repository";
        public DateTimeOffset? ApprovedAtUtc { get; set; }
        public string ConnectionSource { get; set; } = "userSelected";
        public string CompanionId { get; set; } = Guid.NewGuid().ToString("N");
        public CompanionState CompanionState { get; set; } = CompanionState.CreateDefault();
        public DesktopCompanionSettings DesktopCompanionSettings { get; set; } = DesktopCompanionSettings.CreateDefault();
        public TokenShopState TokenShop { get; set; } = new TokenShopState();
        public List<SourceProviderMixEntry> SourceProviderMix { get; set; } = new List<SourceProviderMixEntry>();
        public string LastApprovedActivityBucket { get; set; } = string.Empty;
        public DateTimeOffset? ArchivedAtUtc { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
