using System;

namespace TokenForge.Client.Git
{
    public enum GitAnalysisMode
    {
        Auto,
        FullBaseline,
        Incremental,
        RecentTrend
    }

    public sealed class GitRepositoryAnalysisInput
    {
        public const int DefaultAnalysisWindowDays = 7;
        public const int MaxAnalysisWindowDays = 30;
        public const int DefaultMaxCommitsToInspect = 50;
        public const int MaxCommitsToInspectLimit = 200;

        public string RepositoryRootPath { get; set; } = string.Empty;
        public GitAnalysisMode AnalysisMode { get; set; } = GitAnalysisMode.Auto;
        public string LastAnalyzedCommit { get; set; } = string.Empty;
        public bool RebuildFullHistory { get; set; }
        public int AnalysisWindowDays { get; set; } = DefaultAnalysisWindowDays;
        public bool IncludeUncommittedChanges { get; set; } = true;
        public bool IncludeRecentCommits { get; set; } = true;
        public int MaxCommitsToInspect { get; set; } = DefaultMaxCommitsToInspect;

        public GitAnalysisMode ResolveAnalysisMode()
        {
            if (RebuildFullHistory || AnalysisMode == GitAnalysisMode.FullBaseline)
            {
                return GitAnalysisMode.FullBaseline;
            }

            if (AnalysisMode == GitAnalysisMode.Incremental)
            {
                return string.IsNullOrWhiteSpace(LastAnalyzedCommit) ? GitAnalysisMode.FullBaseline : GitAnalysisMode.Incremental;
            }

            if (AnalysisMode == GitAnalysisMode.RecentTrend)
            {
                return GitAnalysisMode.RecentTrend;
            }

            return string.IsNullOrWhiteSpace(LastAnalyzedCommit) ? GitAnalysisMode.FullBaseline : GitAnalysisMode.Incremental;
        }

        public int ClampedAnalysisWindowDays()
        {
            return Clamp(AnalysisWindowDays <= 0 ? DefaultAnalysisWindowDays : AnalysisWindowDays, 1, MaxAnalysisWindowDays);
        }

        public int ClampedMaxCommitsToInspect()
        {
            return Clamp(MaxCommitsToInspect <= 0 ? DefaultMaxCommitsToInspect : MaxCommitsToInspect, 1, MaxCommitsToInspectLimit);
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
