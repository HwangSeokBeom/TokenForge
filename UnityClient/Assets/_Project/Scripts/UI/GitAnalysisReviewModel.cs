using System;
using System.Collections.Generic;
using System.Linq;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;

namespace TokenForge.Client.UI
{
    public sealed class GitAnalysisSettings
    {
        public int AnalysisWindowDays { get; set; } = GitRepositoryAnalysisInput.DefaultAnalysisWindowDays;
        public bool IncludeUncommittedChanges { get; set; } = true;
        public bool IncludeRecentCommits { get; set; } = true;
        public int MaxCommitsToInspect { get; set; } = GitRepositoryAnalysisInput.DefaultMaxCommitsToInspect;

        public GitRepositoryAnalysisInput ToInput(string repositoryRootPath)
        {
            return new GitRepositoryAnalysisInput
            {
                RepositoryRootPath = repositoryRootPath ?? string.Empty,
                AnalysisWindowDays = AnalysisWindowDays,
                IncludeUncommittedChanges = IncludeUncommittedChanges,
                IncludeRecentCommits = IncludeRecentCommits,
                MaxCommitsToInspect = MaxCommitsToInspect
            };
        }
    }

    public sealed class GitExtensionCategoryReviewItem
    {
        public string Category { get; set; } = string.Empty;
        public CountBucket CountBucket { get; set; } = CountBucket.Unknown;
    }

    public sealed class GitAnalysisReviewModel
    {
        public string SourceProvider { get; set; } = GitAnalysisSessionProvider.SourceProviderId;
        public string SessionAlias { get; set; } = string.Empty;
        public string AnalysisTimeBucket { get; set; } = string.Empty;
        public int AnalysisWindowDays { get; set; }
        public CountBucket ChangedFilesBucket { get; set; } = CountBucket.Unknown;
        public LineChangeBucket AddedLinesBucket { get; set; } = LineChangeBucket.Unknown;
        public LineChangeBucket DeletedLinesBucket { get; set; } = LineChangeBucket.Unknown;
        public CountBucket CommitCountBucket { get; set; } = CountBucket.Unknown;
        public List<GitExtensionCategoryReviewItem> ExtensionCategoryBuckets { get; set; } = new List<GitExtensionCategoryReviewItem>();
        public ConfidenceLevel ConfidenceLevel { get; set; } = ConfidenceLevel.Unknown;
        public int PrivacyWarningCount { get; set; }
        public List<string> PrivacyWarningCategories { get; set; } = new List<string>();
        public CharacterStats DerivedStatDeltas { get; set; } = new CharacterStats();
        public int DerivedExpGained { get; set; }
        public string AnalyzerVersion { get; set; } = string.Empty;

        public static GitAnalysisReviewModel From(AgentWorkSession session, CharacterGrowthResult growthResult)
        {
            var summary = session?.GitChangeSummary ?? GitChangeSummary.Empty();
            return new GitAnalysisReviewModel
            {
                SourceProvider = GitAnalysisSessionProvider.SourceProviderId,
                SessionAlias = string.IsNullOrWhiteSpace(summary.SafeSessionAlias) ? "Git aggregate session" : summary.SafeSessionAlias,
                AnalysisTimeBucket = summary.AnalysisTimeBucket,
                AnalysisWindowDays = summary.AnalysisWindowDays,
                ChangedFilesBucket = summary.ChangedFileCountBucket,
                AddedLinesBucket = summary.AddedLineBucket,
                DeletedLinesBucket = summary.DeletedLineBucket,
                CommitCountBucket = summary.CommitCountBucket,
                ExtensionCategoryBuckets = (summary.ExtensionCategoryBuckets ?? Enumerable.Empty<ExtensionCategoryCount>())
                    .Select(item => new GitExtensionCategoryReviewItem
                    {
                        Category = item.Category,
                        CountBucket = item.CountBucket
                    })
                    .OrderBy(item => item.Category, StringComparer.Ordinal)
                    .ToList(),
                ConfidenceLevel = summary.ConfidenceLevel,
                PrivacyWarningCount = summary.PrivacyWarnings?.Count ?? 0,
                PrivacyWarningCategories = (summary.PrivacyWarnings ?? Enumerable.Empty<string>())
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToList(),
                DerivedStatDeltas = growthResult?.StatDeltas ?? new CharacterStats(),
                DerivedExpGained = growthResult?.ExpGained ?? 0,
                AnalyzerVersion = summary.AnalyzerVersion
            };
        }
    }
}
