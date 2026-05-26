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

    public enum AgentProviderType
    {
        Unknown = 0,
        Claude = 1,
        Codex = 2,
        Cursor = 3,
        ClaudeCode = 4,
        GitHubCopilot = 5,
        Manual = 6,
        GeminiCli = 7
    }

    public enum AgentSourceKind
    {
        Unknown,
        DetectedLocal,
        ManualFolder,
        ExportFile
    }

    public enum AgentToolUsageCategory
    {
        Unknown,
        CodeEditing,
        ShellCommand,
        TestRun,
        BuildRun,
        FileNavigation,
        Search
    }

    public enum AgentLanguageCategory
    {
        Unknown,
        CSharp,
        JavaScript,
        TypeScript,
        Python,
        Web,
        Config,
        Docs,
        Test,
        Shell
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

    public enum ConfidenceLevel
    {
        Unknown,
        Low,
        Medium,
        High
    }

    public enum SourceProvider
    {
        Unknown,
        ManualSession,
        ManualFallback,
        GitDiff,
        Mock,
        Deduplicated
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

    public enum CountBucket
    {
        Unknown,
        None,
        One,
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
