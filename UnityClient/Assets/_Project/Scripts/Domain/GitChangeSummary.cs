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
    public sealed class GitChangeSummary
    {
        public int ChangedFileCount { get; set; }
        public LineChangeBucket AddedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public LineChangeBucket DeletedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public bool TestFileChanged { get; set; }
        public bool DocsFileChanged { get; set; }
        public bool UiFileChanged { get; set; }
        public bool ArchitectureFileChanged { get; set; }
        public List<FileCategoryCount> FileCategoryCounts { get; set; } = new List<FileCategoryCount>();
        public WorkingTreeStatus WorkingTreeStatus { get; set; } = WorkingTreeStatus.Unknown;
        public string ProjectPathHash { get; set; } = string.Empty;

        public static GitChangeSummary Empty()
        {
            return new GitChangeSummary();
        }
    }
}
