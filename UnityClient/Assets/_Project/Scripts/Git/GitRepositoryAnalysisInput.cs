using System;

namespace TokenForge.Client.Git
{
    public sealed class GitRepositoryAnalysisInput
    {
        public const int DefaultAnalysisWindowDays = 7;
        public const int MaxAnalysisWindowDays = 30;
        public const int DefaultMaxCommitsToInspect = 50;
        public const int MaxCommitsToInspectLimit = 200;

        public string RepositoryRootPath { get; set; } = string.Empty;
        public int AnalysisWindowDays { get; set; } = DefaultAnalysisWindowDays;
        public bool IncludeUncommittedChanges { get; set; } = true;
        public bool IncludeRecentCommits { get; set; } = true;
        public int MaxCommitsToInspect { get; set; } = DefaultMaxCommitsToInspect;

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
