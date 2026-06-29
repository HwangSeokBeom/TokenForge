using System;
using System.Collections.Generic;

namespace TokenForge.Client.Domain
{
    [Serializable]
    public sealed class FileCategoryCount
    {
        public FileCategory Category { get; set; } = FileCategory.Unknown;
        public int Count { get; set; }
    }

    [Serializable]
    public sealed class ExtensionCategoryCount
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
        public CountBucket CountBucket { get; set; } = CountBucket.Unknown;
    }

    [Serializable]
    public sealed class GitChangeSummary
    {
        public int ChangedFileCount { get; set; }
        public CountBucket ChangedFileCountBucket { get; set; } = CountBucket.Unknown;
        public LineChangeBucket AddedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public LineChangeBucket DeletedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public CountBucket ModifiedFileCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket CreatedFileCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket DeletedFileCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket RenamedFileCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket BinaryFileCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket CommitCountBucket { get; set; } = CountBucket.Unknown;
        public bool TestFileChanged { get; set; }
        public bool DocsFileChanged { get; set; }
        public bool UiFileChanged { get; set; }
        public bool ArchitectureFileChanged { get; set; }
        public List<FileCategoryCount> FileCategoryCounts { get; set; } = new List<FileCategoryCount>();
        public List<ExtensionCategoryCount> ExtensionCategoryBuckets { get; set; } = new List<ExtensionCategoryCount>();
        public WorkingTreeStatus WorkingTreeStatus { get; set; } = WorkingTreeStatus.Unknown;
        public string ProjectPathHash { get; set; } = string.Empty;
        public string AnalysisTimeBucket { get; set; } = string.Empty;
        public bool HasUncommittedChanges { get; set; }
        public int AnalysisWindowDays { get; set; }
        public string AnalysisMode { get; set; } = string.Empty;
        public string FirstCommitHash { get; set; } = string.Empty;
        public string FirstCommitAtUtc { get; set; } = string.Empty;
        public int TotalCommitsAnalyzed { get; set; }
        public int IncrementalCommitCount { get; set; }
        public string LastAnalyzedCommit { get; set; } = string.Empty;
        public string AnalyzedStartCommit { get; set; } = string.Empty;
        public string AnalyzedEndCommit { get; set; } = string.Empty;
        public string AnalysisRangeSummary { get; set; } = string.Empty;
        public string AnalysisIdempotencyKey { get; set; } = string.Empty;
        public ConfidenceLevel ConfidenceLevel { get; set; } = ConfidenceLevel.Unknown;
        public string AnalyzerVersion { get; set; } = string.Empty;
        public string SafeSessionAlias { get; set; } = string.Empty;
        public List<string> PrivacyWarnings { get; set; } = new List<string>();

        public static GitChangeSummary Empty()
        {
            return new GitChangeSummary();
        }
    }
}
