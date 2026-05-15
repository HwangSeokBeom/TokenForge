using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Sync
{
    public sealed class SafeConflictMergePolicyEvaluator
    {
        private readonly PrivacySanitizer privacySanitizer;
        private readonly RemoteSafeSessionValidator remoteValidator;

        public SafeConflictMergePolicyEvaluator(PrivacySanitizer privacySanitizer = null, RemoteSafeSessionValidator remoteValidator = null)
        {
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.remoteValidator = remoteValidator ?? new RemoteSafeSessionValidator(this.privacySanitizer);
        }

        public SafeConflictMergePreview Preview(SafeSyncConflict conflict, SafeConflictMergePolicy policy, bool localSessionExists)
        {
            var preview = new SafeConflictMergePreview
            {
                ConflictId = conflict?.ConflictId ?? string.Empty,
                LocalSummary = Clone(conflict?.SafeLocalSummary),
                RemoteSummary = Clone(conflict?.SafeRemoteSummary),
                DiffSummary = conflict?.SafeDiffSummary ?? new SafeSessionDiffSummary(),
                SelectedPolicy = policy,
                EffectivePolicy = policy
            };

            if (conflict == null)
            {
                return Block(preview, SafeSyncApiError.ConflictResolutionFailed);
            }

            if (!privacySanitizer.ValidateNoForbiddenFields(preview).IsSuccess)
            {
                return Block(preview, SafeSyncApiError.KeepRemoteUnsafeRejected);
            }

            switch (policy)
            {
                case SafeConflictMergePolicy.KeepLocal:
                    preview.ResultingSummary = Clone(conflict.SafeLocalSummary);
                    preview.CanApply = localSessionExists;
                    preview.BlockedReason = localSessionExists ? string.Empty : SafeSyncApiError.LocalSessionMissing;
                    preview.Warnings.Add(SafeSyncApiError.ConflictKeepLocalQueued);
                    return preview;
                case SafeConflictMergePolicy.KeepRemote:
                    return KeepRemotePreview(preview, conflict);
                case SafeConflictMergePolicy.PreferHigherConfidence:
                    return PreferConfidence(preview, conflict, localSessionExists);
                case SafeConflictMergePolicy.PreferNewerSafeTimestamp:
                    return PreferTimestamp(preview, conflict, localSessionExists);
                case SafeConflictMergePolicy.MergeNonConflictingAggregates:
                    return MergeNonConflicting(preview, conflict);
                case SafeConflictMergePolicy.MarkResolvedOnly:
                    preview.CanApply = true;
                    preview.ResultingSummary = Clone(conflict.SafeLocalSummary);
                    preview.Warnings.Add(SafeSyncApiError.ConflictMarkedResolved);
                    return preview;
                default:
                    return Block(preview, SafeSyncApiError.ConflictResolutionFailed);
            }
        }

        private SafeConflictMergePreview KeepRemotePreview(SafeConflictMergePreview preview, SafeSyncConflict conflict)
        {
            preview.ResultingSummary = Clone(conflict.SafeRemoteSummary);
            if (conflict.SafeRemoteSession == null)
            {
                preview.CanApply = true;
                preview.Warnings.Add(SafeSyncApiError.KeepRemoteMarkerOnly);
                return preview;
            }

            var validation = remoteValidator.Validate(conflict.SafeRemoteSession);
            preview.CanApply = validation.IsSuccess;
            preview.BlockedReason = validation.IsSuccess ? string.Empty : validation.ErrorCode;
            if (!validation.IsSuccess)
            {
                preview.Warnings.Add(validation.ErrorCode);
            }

            return preview;
        }

        private SafeConflictMergePreview PreferConfidence(SafeConflictMergePreview preview, SafeSyncConflict conflict, bool localSessionExists)
        {
            var local = ConfidenceRank(conflict.SafeLocalSummary?.Confidence);
            var remote = ConfidenceRank(conflict.SafeRemoteSummary?.Confidence);
            if (local <= 0 || remote <= 0 || local == remote)
            {
                return Block(preview, SafeSyncApiError.MergePolicyManualChoiceRequired);
            }

            preview.EffectivePolicy = local > remote ? SafeConflictMergePolicy.KeepLocal : SafeConflictMergePolicy.KeepRemote;
            return Preview(conflict, preview.EffectivePolicy, localSessionExists).WithSelected(preview.SelectedPolicy);
        }

        private SafeConflictMergePreview PreferTimestamp(SafeConflictMergePreview preview, SafeSyncConflict conflict, bool localSessionExists)
        {
            var local = SafeTimestamp(conflict.SafeLocalSummary);
            var remote = SafeTimestamp(conflict.SafeRemoteSummary);
            if (!local.HasValue || !remote.HasValue || local.Value == remote.Value)
            {
                return Block(preview, SafeSyncApiError.MergePolicyManualChoiceRequired);
            }

            preview.EffectivePolicy = local.Value > remote.Value ? SafeConflictMergePolicy.KeepLocal : SafeConflictMergePolicy.KeepRemote;
            return Preview(conflict, preview.EffectivePolicy, localSessionExists).WithSelected(preview.SelectedPolicy);
        }

        private SafeConflictMergePreview MergeNonConflicting(SafeConflictMergePreview preview, SafeSyncConflict conflict)
        {
            var merged = Clone(conflict.SafeLocalSummary);
            merged.ServerSessionId = FirstNonEmpty(conflict.SafeRemoteSummary?.ServerSessionId, merged.ServerSessionId);

            var blockers = new List<string>();
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.SourceProvider), blockers, ignoreCase: true);
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.DayBucket), blockers, ignoreCase: false);
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.TimeBucket), blockers, ignoreCase: false);
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.ActivityCategory), blockers, ignoreCase: true);
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.ChangeCountBucket), blockers, ignoreCase: true);
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.LineCountBucket), blockers, ignoreCase: true);
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.SessionCountBucket), blockers, ignoreCase: true);
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.InteractionCountBucket), blockers, ignoreCase: true);
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.Confidence), blockers, ignoreCase: true);
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.ParserVersion), blockers, ignoreCase: false);
            MergeScalar(merged, conflict.SafeLocalSummary, conflict.SafeRemoteSummary, nameof(SafeSyncSessionSafeSummary.AnalyzerVersion), blockers, ignoreCase: false);

            if (conflict.SafeLocalSummary?.WarningCount != conflict.SafeRemoteSummary?.WarningCount)
            {
                blockers.Add("warningCount");
            }

            merged.CategoryBucketSummary = MergeBucketSummary(conflict.SafeLocalSummary?.CategoryBucketSummary, conflict.SafeRemoteSummary?.CategoryBucketSummary, "categoryBuckets", blockers);
            merged.ToolBucketSummary = MergeBucketSummary(conflict.SafeLocalSummary?.ToolBucketSummary, conflict.SafeRemoteSummary?.ToolBucketSummary, "toolBuckets", blockers);
            merged.LanguageBucketSummary = MergeBucketSummary(conflict.SafeLocalSummary?.LanguageBucketSummary, conflict.SafeRemoteSummary?.LanguageBucketSummary, "languageBuckets", blockers);
            merged.SchemaVersion = 1;

            preview.ResultingSummary = merged;
            preview.CanApply = blockers.Count == 0 && privacySanitizer.ValidateNoForbiddenFields(merged).IsSuccess;
            preview.BlockedReason = preview.CanApply ? string.Empty : SafeSyncApiError.MergePolicyAmbiguousAggregate;
            preview.Warnings = blockers.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
            return preview;
        }

        private static SafeConflictMergePreview Block(SafeConflictMergePreview preview, string reason)
        {
            preview.CanApply = false;
            preview.BlockedReason = reason ?? SafeSyncApiError.ConflictResolutionFailed;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                preview.Warnings.Add(reason);
            }

            return preview;
        }

        private static int ConfidenceRank(string value)
        {
            if (string.Equals(value, "HIGH", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(value, "MEDIUM", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(value, "LOW", StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        }

        private static DateTimeOffset? SafeTimestamp(SafeSyncSessionSafeSummary summary)
        {
            if (summary == null || string.IsNullOrWhiteSpace(summary.DayBucket))
            {
                return null;
            }

            if (!DateTimeOffset.TryParseExact(summary.DayBucket + "T00:00:00Z", "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var day))
            {
                return null;
            }

            var hour = 0;
            if (!string.IsNullOrWhiteSpace(summary.TimeBucket))
            {
                if (!summary.TimeBucket.StartsWith("HOUR_", StringComparison.Ordinal) ||
                    !int.TryParse(summary.TimeBucket.Substring(5), out hour) ||
                    hour < 0 ||
                    hour > 23)
                {
                    return null;
                }
            }

            return day.AddHours(hour);
        }

        private static string MergeBucketSummary(string left, string right, string fieldName, List<string> blockers)
        {
            var leftMap = ParseBucketSummary(left);
            var rightMap = ParseBucketSummary(right);
            foreach (var pair in rightMap)
            {
                if (leftMap.TryGetValue(pair.Key, out var existing) &&
                    !string.Equals(existing, pair.Value, StringComparison.OrdinalIgnoreCase))
                {
                    blockers.Add(fieldName);
                    continue;
                }

                leftMap[pair.Key] = pair.Value;
            }

            return string.Join(",", leftMap.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + ":" + pair.Value));
        }

        private static Dictionary<string, string> ParseBucketSummary(string value)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in (value ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pieces = part.Split(new[] { ':' }, 2);
                if (pieces.Length != 2 || string.IsNullOrWhiteSpace(pieces[0]) || string.IsNullOrWhiteSpace(pieces[1]))
                {
                    continue;
                }

                map[pieces[0].Trim()] = pieces[1].Trim();
            }

            return map;
        }

        private static void MergeScalar(SafeSyncSessionSafeSummary target, SafeSyncSessionSafeSummary local, SafeSyncSessionSafeSummary remote, string propertyName, List<string> blockers, bool ignoreCase)
        {
            var property = typeof(SafeSyncSessionSafeSummary).GetProperty(propertyName);
            var left = property?.GetValue(local) as string ?? string.Empty;
            var right = property?.GetValue(remote) as string ?? string.Empty;
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
            {
                property?.SetValue(target, right);
                return;
            }

            if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && !string.Equals(left, right, comparison))
            {
                blockers.Add(ToSafeFieldName(propertyName));
            }
        }

        private static string ToSafeFieldName(string propertyName)
        {
            return string.IsNullOrWhiteSpace(propertyName)
                ? string.Empty
                : char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : (second ?? string.Empty);
        }

        public static SafeSyncSessionSafeSummary Clone(SafeSyncSessionSafeSummary source)
        {
            source = source ?? new SafeSyncSessionSafeSummary();
            return new SafeSyncSessionSafeSummary
            {
                ClientSessionId = source.ClientSessionId ?? string.Empty,
                ServerSessionId = source.ServerSessionId ?? string.Empty,
                SourceProvider = source.SourceProvider ?? "UNKNOWN_AGENT",
                DayBucket = source.DayBucket ?? string.Empty,
                TimeBucket = source.TimeBucket ?? string.Empty,
                ActivityCategory = source.ActivityCategory ?? string.Empty,
                ChangeCountBucket = source.ChangeCountBucket ?? string.Empty,
                LineCountBucket = source.LineCountBucket ?? string.Empty,
                SessionCountBucket = source.SessionCountBucket ?? string.Empty,
                InteractionCountBucket = source.InteractionCountBucket ?? string.Empty,
                Confidence = source.Confidence ?? "LOW",
                WarningCount = source.WarningCount,
                CategoryBucketSummary = source.CategoryBucketSummary ?? string.Empty,
                ToolBucketSummary = source.ToolBucketSummary ?? string.Empty,
                LanguageBucketSummary = source.LanguageBucketSummary ?? string.Empty,
                AnalyzerVersion = source.AnalyzerVersion ?? string.Empty,
                ParserVersion = source.ParserVersion ?? string.Empty,
                SchemaVersion = 1
            };
        }
    }

    internal static class SafeConflictMergePreviewExtensions
    {
        public static SafeConflictMergePreview WithSelected(this SafeConflictMergePreview preview, SafeConflictMergePolicy selected)
        {
            preview.SelectedPolicy = selected;
            return preview;
        }
    }
}
