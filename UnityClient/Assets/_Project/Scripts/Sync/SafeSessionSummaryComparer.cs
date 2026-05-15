using System;
using System.Collections.Generic;

namespace TokenForge.Client.Sync
{
    public sealed class SafeSessionSummaryComparer
    {
        public SafeSessionDiffSummary Compare(SafeSyncSessionSafeSummary local, SafeSyncSessionSafeSummary remote)
        {
            local = local ?? new SafeSyncSessionSafeSummary();
            remote = remote ?? new SafeSyncSessionSafeSummary();
            var changed = new List<string>();

            AddIfDifferent(changed, "sourceProvider", local.SourceProvider, remote.SourceProvider, ignoreCase: true);
            AddIfDifferent(changed, "dayBucket", local.DayBucket, remote.DayBucket, ignoreCase: false);
            AddIfDifferent(changed, "timeBucket", local.TimeBucket, remote.TimeBucket, ignoreCase: false);
            AddIfDifferent(changed, "confidence", local.Confidence, remote.Confidence, ignoreCase: true);
            AddIfDifferent(changed, "activityCategory", local.ActivityCategory, remote.ActivityCategory, ignoreCase: true);
            AddIfDifferent(changed, "changeCountBucket", local.ChangeCountBucket, remote.ChangeCountBucket, ignoreCase: true);
            AddIfDifferent(changed, "lineCountBucket", local.LineCountBucket, remote.LineCountBucket, ignoreCase: true);
            AddIfDifferent(changed, "sessionCountBucket", local.SessionCountBucket, remote.SessionCountBucket, ignoreCase: true);
            AddIfDifferent(changed, "interactionCountBucket", local.InteractionCountBucket, remote.InteractionCountBucket, ignoreCase: true);
            AddIfDifferent(changed, "categoryBuckets", local.CategoryBucketSummary, remote.CategoryBucketSummary, ignoreCase: false);
            AddIfDifferent(changed, "toolBuckets", local.ToolBucketSummary, remote.ToolBucketSummary, ignoreCase: false);
            AddIfDifferent(changed, "languageBuckets", local.LanguageBucketSummary, remote.LanguageBucketSummary, ignoreCase: false);
            AddIfDifferent(changed, "parserVersion", local.ParserVersion, remote.ParserVersion, ignoreCase: false);
            AddIfDifferent(changed, "analyzerVersion", local.AnalyzerVersion, remote.AnalyzerVersion, ignoreCase: false);
            if (local.WarningCount != remote.WarningCount)
            {
                changed.Add("warningCount");
            }

            return new SafeSessionDiffSummary
            {
                IsDifferent = changed.Count > 0,
                ChangedSafeFieldNames = changed
            };
        }

        private static void AddIfDifferent(List<string> changed, string fieldName, string left, string right, bool ignoreCase)
        {
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(left ?? string.Empty, right ?? string.Empty, comparison))
            {
                changed.Add(fieldName);
            }
        }
    }
}
