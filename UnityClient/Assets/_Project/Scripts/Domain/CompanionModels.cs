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
        AnalysisComplete
    }

    public enum CompanionDesktopOverlayState
    {
        Unavailable,
        Disabled,
        Active,
        Fallback
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
        public string VisualThemeId { get; set; } = "pixel-default";

        public static DesktopCompanionSettings CreateDefault()
        {
            return new DesktopCompanionSettings();
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
            var scale = mode == CompanionDesktopMotionMode.Calm ? 0.65f : mode == CompanionDesktopMotionMode.Playful ? 1.25f : 1f;
            switch (stage)
            {
                case CompanionStage.Hatching:
                    return new CompanionMotionProfile { DefaultMode = CompanionMotionMode.Idle, IdleRadius = 7f * scale, WanderRadius = 14f * scale, WanderSpeed = 7f * scale, DecisionIntervalSeconds = 2.4f, AllowsWandering = false };
                case CompanionStage.Baby:
                    return new CompanionMotionProfile { DefaultMode = CompanionMotionMode.Wandering, IdleRadius = 6f * scale, WanderRadius = 64f * scale, WanderSpeed = 15f * scale, DecisionIntervalSeconds = 3.2f, AllowsWandering = true };
                case CompanionStage.Junior:
                    return new CompanionMotionProfile { DefaultMode = CompanionMotionMode.Wandering, IdleRadius = 5f * scale, WanderRadius = 140f * scale, WanderSpeed = 23f * scale, DecisionIntervalSeconds = 3.8f, AllowsWandering = true };
                case CompanionStage.Adult:
                    return new CompanionMotionProfile { DefaultMode = CompanionMotionMode.Wandering, IdleRadius = 5f * scale, WanderRadius = 220f * scale, WanderSpeed = 30f * scale, DecisionIntervalSeconds = 4.4f, AllowsWandering = true };
                default:
                    return new CompanionMotionProfile { DefaultMode = CompanionMotionMode.Idle, IdleRadius = 5f * scale, WanderRadius = 18f * scale, WanderSpeed = 5f * scale, DecisionIntervalSeconds = 4.6f, AllowsWandering = false };
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
    public sealed class CompanionState
    {
        public int SchemaVersion { get; set; } = 1;
        public CompanionStage Stage { get; set; } = CompanionStage.Egg;
        public CompanionArchetype Archetype { get; set; } = CompanionArchetype.Unknown;
        public int Level { get; set; } = 1;
        public int TotalXp { get; set; }
        public int XpToNextStage { get; set; } = 250;
        public CompanionStatProfile Stats { get; set; } = CompanionStatProfile.Empty();
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
        public string SafeRepositoryAlias { get; set; } = "Local Repository";
        public CompanionState CompanionState { get; set; } = CompanionState.CreateDefault();
        public DesktopCompanionSettings DesktopCompanionSettings { get; set; } = DesktopCompanionSettings.CreateDefault();
        public List<SourceProviderMixEntry> SourceProviderMix { get; set; } = new List<SourceProviderMixEntry>();
        public string LastApprovedActivityBucket { get; set; } = string.Empty;
        public DateTimeOffset? ArchivedAtUtc { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
