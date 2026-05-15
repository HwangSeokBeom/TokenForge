using System;
using System.Collections.Generic;
using NUnit.Framework;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase24ConflictReviewTests
    {
        [Test]
        public void SafeSummaryComparerReportsFieldNamesOnly()
        {
            var comparer = new SafeSessionSummaryComparer();
            var left = Summary("HIGH", "WORK_FEATURE", "parser.1");
            var right = Summary("MEDIUM", "WORK_BUGFIX", "parser.2");

            var diff = comparer.Compare(left, right);
            var text = string.Join(" ", diff.ChangedSafeFieldNames);

            Assert.IsTrue(diff.IsDifferent);
            Assert.That(diff.ChangedSafeFieldNames, Does.Contain("confidence"));
            Assert.That(diff.ChangedSafeFieldNames, Does.Contain("categoryBuckets"));
            Assert.That(diff.ChangedSafeFieldNames, Does.Contain("parserVersion"));
            Assert.That(text, Does.Not.Contain("HIGH"));
            Assert.That(text, Does.Not.Contain("WORK_BUGFIX"));
            Assert.That(text, Does.Not.Contain("parser.2"));
        }

        [Test]
        public void ConflictReviewFormatterShowsSideBySideSafeSummaries()
        {
            var text = "Local: " + BootstrapUiTextFormatter.SafeConflictSummaryLine(Summary("HIGH", "WORK_FEATURE:ONE", "parser.1"), false) +
                       "\nRemote: " + BootstrapUiTextFormatter.SafeConflictSummaryLine(Summary("MEDIUM", "WORK_BUGFIX:ONE", "parser.2"), true) +
                       "\nKeep Local queues safe re-upload. Keep Remote applies safe remote aggregate locally when possible. Mark Resolved records a decision without data changes.";

            Assert.That(text, Does.Contain("Local:"));
            Assert.That(text, Does.Contain("Remote:"));
            Assert.That(text, Does.Contain("Keep Remote applies safe remote aggregate locally when possible"));
            Assert.That(text, Does.Not.Contain("{\""));
            Assert.That(text, Does.Not.Contain("/Users/"));
            Assert.That(text, Does.Not.Contain("prompt"));
            Assert.That(text, Does.Not.Contain("response"));
        }

        private static SafeSyncSessionSafeSummary Summary(string confidence, string category, string parserVersion)
        {
            return new SafeSyncSessionSafeSummary
            {
                ClientSessionId = "client-session-1234567890",
                ServerSessionId = "server-session-1234567890",
                SourceProvider = "CODEX",
                DayBucket = "2026-05-15",
                TimeBucket = "HOUR_02",
                Confidence = confidence,
                WarningCount = 1,
                ActivityCategory = "WORK_FEATURE",
                CategoryBucketSummary = category,
                ToolBucketSummary = "TOOL_EDIT:FEW",
                LanguageBucketSummary = "LANG_CSHARP:FEW",
                ParserVersion = parserVersion,
                AnalyzerVersion = "analyzer.1"
            };
        }
    }
}
