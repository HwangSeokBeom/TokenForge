namespace TokenForge.Client.Domain
{
    public static class CompanionGrowthReasonIds
    {
        public const string ApprovedAggregateGrowth = "approved_aggregate_growth";
        public const string NoApprovedGrowthYet = "no_approved_growth_yet";
        public const string ProviderCursorBuilderSignal = "provider_cursor_builder_signal";
        public const string ProviderClaudeArchitectSignal = "provider_claude_architect_signal";
        public const string ProviderCodexBuilderSignal = "provider_codex_builder_signal";
        public const string ProviderCodexDebuggerSignal = "provider_codex_debugger_signal";
        public const string ProviderCopilotRefinerSignal = "provider_copilot_refiner_signal";
        public const string GitFrequentSmallCommitsRefinerSignal = "git_frequent_small_commits_refiner_signal";
        public const string GitLargeBurstSprinterSignal = "git_large_burst_sprinter_signal";
        public const string GitTestDebugDebuggerSignal = "git_test_debug_debugger_signal";
        public const string GitDocsRefactorArchitectSignal = "git_docs_refactor_architect_signal";
        public const string BalancedTokenGitGrowthBoost = "balanced_token_git_growth_boost";
        public const string HighTokenLowLocalActivityCautiousGrowth = "high_token_low_local_activity_cautious_growth";
        public const string ProviderDiversityExplorerSignal = "provider_diversity_explorer_signal";
        public const string SteadyReviewSaveConsistencyBonus = "steady_review_save_consistency_bonus";
    }
}
