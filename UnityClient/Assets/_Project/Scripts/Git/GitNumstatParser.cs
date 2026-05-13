using System;
using System.Collections.Generic;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Git
{
    public sealed class GitNumstatEntry
    {
        public int AddedLines { get; set; }
        public int DeletedLines { get; set; }
        public bool IsBinary { get; set; }
        public FileCategory Category { get; set; } = FileCategory.Unknown;
    }

    public static class GitNumstatParser
    {
        public static List<GitNumstatEntry> Parse(string numstatOutput)
        {
            var entries = new List<GitNumstatEntry>();
            if (string.IsNullOrWhiteSpace(numstatOutput))
            {
                return entries;
            }

            var lines = numstatOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (parts.Length < 3)
                {
                    continue;
                }

                var binary = parts[0] == "-" || parts[1] == "-";
                var added = ParseCount(parts[0]);
                var deleted = ParseCount(parts[1]);
                var category = GitPathClassifier.Classify(parts[2]);
                entries.Add(new GitNumstatEntry
                {
                    AddedLines = added,
                    DeletedLines = deleted,
                    IsBinary = binary,
                    Category = category
                });
            }

            return entries;
        }

        public static LineChangeBucket ToBucket(int lines)
        {
            if (lines <= 0) return LineChangeBucket.None;
            if (lines <= 25) return LineChangeBucket.Small;
            if (lines <= 150) return LineChangeBucket.Medium;
            if (lines <= 600) return LineChangeBucket.Large;
            return LineChangeBucket.Huge;
        }

        private static int ParseCount(string raw)
        {
            return int.TryParse(raw, out var value) ? Math.Max(0, value) : 0;
        }
    }
}
