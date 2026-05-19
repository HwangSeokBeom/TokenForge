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
        GrowthPulse
    }

    public enum CompanionDesktopMotionMode
    {
        Calm,
        Normal,
        Playful
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
        public int SchemaVersion { get; set; } = 1;
        public bool IsDesktopCompanionEnabled { get; set; }
        public CompanionDesktopMotionMode MotionMode { get; set; } = CompanionDesktopMotionMode.Normal;
        public bool IsClickThroughEnabled { get; set; } = true;
        public CountBucket LastOverlayPositionXBucket { get; set; } = CountBucket.Unknown;
        public CountBucket LastOverlayPositionYBucket { get; set; } = CountBucket.Unknown;
        public string VisualThemeId { get; set; } = "pixel-default";

        public static DesktopCompanionSettings CreateDefault()
        {
            return new DesktopCompanionSettings();
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
    public sealed class CompanionState
    {
        public int SchemaVersion { get; set; } = 1;
        public CompanionStage Stage { get; set; } = CompanionStage.Egg;
        public CompanionArchetype Archetype { get; set; } = CompanionArchetype.Unknown;
        public int Level { get; set; } = 1;
        public int TotalXp { get; set; }
        public int XpToNextStage { get; set; } = 250;
        public CompanionGrowthProfile GrowthProfile { get; set; } = new CompanionGrowthProfile();
        public List<string> LastGrowthReasonIds { get; set; } = new List<string> { "no_approved_growth_yet" };

        public static CompanionState CreateDefault()
        {
            return new CompanionState();
        }
    }
}
