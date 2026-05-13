using System;

namespace TokenForge.Client.Domain
{
    public enum AgentType
    {
        Unknown,
        ClaudeCode,
        Codex,
        Cursor,
        Windsurf,
        GitHubCopilot,
        Continue,
        Aider,
        RooCode,
        Cline,
        JetBrainsAI,
        GitOnly,
        ManualFallback,
        Mock
    }

    public enum WorkType
    {
        Unknown,
        Feature,
        Bugfix,
        Refactor,
        Test,
        UIUX,
        Docs,
        Build,
        Chore,
        Research,
        Mixed
    }

    public enum EvolutionType
    {
        Unknown,
        Debugger,
        Guardian,
        CleanCodeArchitect,
        TokenBerserker,
        ProductBard,
        EfficiencyNinja,
        PromptSummoner,
        IncidentSurvivor,
        RealtimeRanger
    }

    public enum ResultStatus
    {
        Unknown,
        Succeeded,
        PartiallySucceeded,
        Failed,
        Cancelled
    }

    public enum ProviderConfidence
    {
        Unknown,
        Low,
        Medium,
        High
    }

    public enum TokenUsageBucket
    {
        Unknown,
        None,
        Small,
        Medium,
        Large,
        Huge
    }

    public enum LineChangeBucket
    {
        Unknown,
        None,
        Small,
        Medium,
        Large,
        Huge
    }

    public enum DurationBucket
    {
        Unknown,
        Under5Minutes,
        FiveTo15Minutes,
        FifteenTo60Minutes,
        OneTo3Hours,
        Over3Hours
    }

    public enum WorkingTreeStatus
    {
        Unknown,
        Clean,
        HasChanges,
        NotRepository
    }

    public enum FileCategory
    {
        Unknown,
        Test,
        Docs,
        UI,
        Architecture,
        Config,
        Domain
    }

    public enum MiniGameResultGrade
    {
        Unknown,
        C,
        B,
        A,
        S
    }

    public enum DeduplicationDecision
    {
        KeepSeparate,
        MergeAutomatically,
        RequiresUserReview
    }
}
