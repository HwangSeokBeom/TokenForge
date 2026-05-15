using System.Linq;
using System.Text;
using TokenForge.Client.Agents;
using TokenForge.Client.Domain;
using TokenForge.Client.Sync;

namespace TokenForge.Client.UI
{
    public static class BootstrapUiTextFormatter
    {
        public static string GitStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var flow = viewModel?.GitFlow;
            if (flow == null)
            {
                return "Git analysis is unavailable.";
            }

            var builder = new StringBuilder();
            builder.Append("State: ");
            builder.Append(flow.State);
            builder.Append(" | ");
            builder.Append(flow.SelectionStatus);
            builder.Append(" | ");
            builder.Append(flow.UserMessage);

            if (!string.IsNullOrWhiteSpace(flow.ErrorCategory))
            {
                builder.Append(" | Error: ");
                builder.Append(flow.ErrorCategory);
            }

            return builder.ToString();
        }

        public static string AgentStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var flow = viewModel?.AgentFlow;
            if (flow == null)
            {
                return "Agent analysis is unavailable.";
            }

            var provider = viewModel.SelectedAgentProviderType == AgentProviderType.Unknown
                ? "Unknown/Auto"
                : viewModel.SelectedAgentProviderType.ToString();
            var builder = new StringBuilder();
            builder.Append("State: ");
            builder.Append(flow.State);
            builder.Append(" | ");
            builder.Append(viewModel.AgentSelectionStatus);
            builder.Append(" | Provider: ");
            builder.Append(provider);
            builder.Append(" | ");
            builder.Append(flow.UserMessage);

            if (!string.IsNullOrWhiteSpace(flow.ErrorCategory))
            {
                builder.Append(" | Error: ");
                builder.Append(flow.ErrorCategory);
            }

            if (!string.IsNullOrWhiteSpace(viewModel.AgentPickerErrorCategory))
            {
                builder.Append(" | Picker: ");
                builder.Append(viewModel.AgentPickerErrorCategory);
            }

            return builder.ToString();
        }

        public static string ReviewSummary(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel == null)
            {
                return "No review ready.";
            }

            var builder = new StringBuilder();
            if (viewModel.GitFlow.Review != null)
            {
                var review = viewModel.GitFlow.Review;
                builder.Append("Git: ");
                builder.Append(review.SessionAlias);
                builder.Append(" | day ");
                builder.Append(review.AnalysisTimeBucket);
                builder.Append(" | changes ");
                builder.Append(review.ChangedFilesBucket);
                builder.Append(" | lines +");
                builder.Append(review.AddedLinesBucket);
                builder.Append(" -");
                builder.Append(review.DeletedLinesBucket);
                builder.Append(" | confidence ");
                builder.Append(review.ConfidenceLevel);
                builder.Append(" | warning IDs ");
                builder.Append(review.PrivacyWarningCategories.Count == 0 ? "none" : string.Join(", ", review.PrivacyWarningCategories));
                builder.AppendLine();
            }

            if (viewModel.AgentFlow.Review != null)
            {
                var review = viewModel.AgentFlow.Review;
                builder.Append("Agent: ");
                builder.Append(review.ProviderType);
                builder.Append(" | day ");
                builder.Append(review.DayBucket);
                builder.Append(" | sessions ");
                builder.Append(review.SessionCountBucket);
                builder.Append(" | interactions ");
                builder.Append(review.InteractionCountBucket);
                builder.Append(" | confidence ");
                builder.Append(review.ConfidenceLevel);
                builder.Append(" | warning IDs ");
                builder.Append(review.WarningIds.Count == 0 ? "none" : string.Join(", ", review.WarningIds));
                builder.Append(" | save ");
                builder.Append(review.SaveEligible ? "eligible" : "blocked");
            }

            return builder.Length == 0
                ? "No review ready. Select an approved location and run analysis to review safe aggregate data before saving."
                : builder.ToString();
        }

        public static string RecentSessions(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel?.RecentSessions == null || viewModel.RecentSessions.Count == 0)
            {
                return "Local: no saved safe sessions yet.";
            }

            return string.Join("\n", viewModel.RecentSessions.Select(session =>
            {
                return SafeLocalSessionLabel(session);
            }));
        }

        public static string SafeLocalSessionLabel(RecentSafeSessionSummary session)
        {
            if (session == null)
            {
                return "Unknown local session";
            }

            if (session.SourceProvider == "AI_AGENT")
            {
                return $"{session.DayBucket} | AI agent {session.AgentProviderType} | sessions {session.AgentSessionCountBucket} | interactions {session.AgentInteractionCountBucket} | confidence {session.Confidence}";
            }

            return $"{session.DayBucket} | {session.SourceProvider} | work {session.WorkType} | changes {session.GitChangeCountBucket} | lines +{session.GitAddedLineBucket} -{session.GitDeletedLineBucket} | confidence {session.Confidence}";
        }

        public static string AuthStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel == null)
            {
                return "Authentication is unavailable.";
            }

            var builder = new StringBuilder();
            builder.Append("Status: ");
            builder.Append(SafeUserMessageMapper.AuthStateLabel(viewModel.AuthState));
            builder.Append(" | ");
            builder.Append(viewModel.AuthStatusMessage);
            builder.Append(" | Session: ");
            builder.Append(viewModel.AuthSessionExpirySummary);
            if (!string.IsNullOrWhiteSpace(viewModel.AuthErrorCode))
            {
                builder.Append(" | Code: ");
                builder.Append(viewModel.AuthErrorCode);
            }

            return builder.ToString();
        }

        public static string SafeSyncStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel == null)
            {
                return "Safe Sync is unavailable.";
            }

            var builder = new StringBuilder();
            builder.Append("Status: ");
            builder.Append(SafeUserMessageMapper.SafeSyncStatusLabel(viewModel.SafeSyncStatus));
            builder.Append(" | Auth: ");
            builder.Append(SafeUserMessageMapper.AuthStateLabel(viewModel.AuthState));
            builder.Append(" | Server: ");
            builder.Append(viewModel.SafeSyncBaseUrl);
            builder.Append(" | Message: ");
            builder.Append(viewModel.SafeSyncMessage);
            builder.Append(" | Accepted ");
            builder.Append(viewModel.LastSyncAcceptedCount);
            builder.Append(" / rejected ");
            builder.Append(viewModel.LastSyncRejectedCount);
            if (!string.IsNullOrWhiteSpace(viewModel.SafeSyncErrorCode))
            {
                builder.Append(" | Code: ");
                builder.Append(viewModel.SafeSyncErrorCode);
            }

            return builder.ToString();
        }

        public static string RetryQueueStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var summary = viewModel?.RetryQueueSummary ?? new SafeSyncRetryQueueSummary();
            var builder = new StringBuilder();
            builder.Append("Retry Queue: pending ");
            builder.Append(summary.PendingCount);
            builder.Append(" | failed ");
            builder.Append(summary.FailedCount);
            builder.Append(" | paused ");
            builder.Append(summary.PausedCount);
            builder.Append(" | succeeded ");
            builder.Append(summary.SucceededCount);
            if (summary.NextAttemptAt.HasValue)
            {
                builder.Append(" | next ");
                builder.Append(summary.NextAttemptAt.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.Append(" UTC");
            }

            return builder.ToString();
        }

        public static string ConflictStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var summary = viewModel?.ConflictSummary ?? new SafeSyncConflictSummary();
            if (summary.UnresolvedCount == 0)
            {
                return "Conflicts: none unresolved.";
            }

            var first = summary.SafeConflicts.FirstOrDefault();
            if (first == null)
            {
                return "Conflicts: unresolved " + summary.UnresolvedCount;
            }

            var diff = first.SafeDiffSummary?.ChangedSafeFieldNames == null || first.SafeDiffSummary.ChangedSafeFieldNames.Count == 0
                ? "none"
                : string.Join(", ", first.SafeDiffSummary.ChangedSafeFieldNames.Take(6));
            return "Conflicts: unresolved " + summary.UnresolvedCount +
                   " | " + first.ConflictType +
                   " | " + first.ResolutionStatus +
                   " | detected " + first.DetectedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" +
                   "\nLocal: " + SafeConflictSummaryLine(first.SafeLocalSummary, includeServer: false) +
                   "\nRemote: " + SafeConflictSummaryLine(first.SafeRemoteSummary, includeServer: true) +
                   "\nChanged safe fields: " + diff +
                   "\nPolicies: Keep Local, Keep Remote, Prefer Higher Confidence, Prefer Newer Safe Timestamp, Merge Non-Conflicting Aggregates, Mark Resolved Only." +
                   "\nKeep Local queues safe re-upload. Keep Remote applies safe remote aggregate locally when possible. Aggregate merge only uses deterministic safe fields." +
                   "\nResult: " + SafeUserMessageMapper.FromSyncError(first.SafeErrorCode);
        }

        public static string ConflictAuditHistory(ApprovedActivityAnalysisViewModel viewModel)
        {
            var summary = viewModel?.ConflictAuditSummary ?? new SafeConflictAuditSummary();
            if (summary.SafeEntries == null || summary.SafeEntries.Count == 0)
            {
                return "Conflict History: no local-only audit entries. Latest 200 resolved decisions are retained when present.";
            }

            var first = summary.SafeEntries.First();
            var fields = first.SafeDiffFieldNames == null || first.SafeDiffFieldNames.Count == 0
                ? "none"
                : string.Join(", ", first.SafeDiffFieldNames.Take(6));
            var warnings = first.WarningIds == null || first.WarningIds.Count == 0
                ? "none"
                : string.Join(", ", first.WarningIds.Take(4));
            var retry = string.IsNullOrWhiteSpace(first.QueuedRetryEntryId) ? "none" : "queued";
            return "Conflict History: total " + summary.TotalCount +
                   " | resolved " + summary.ResolvedCount +
                   " | failed " + summary.FailedCount +
                   " | local-only latest 200" +
                   "\nRecent: " + first.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" +
                   " | " + first.Action +
                   " | " + SafeSyncConfirmationRequestFactory.PolicyLabel(first.Policy) +
                   " | " + first.ResultStatus +
                   "\nFields: " + fields +
                   "\nRetry: " + retry +
                   "\nWarnings: " + warnings +
                   "\nMessage: " + SafeUserMessageMapper.FromSyncError(first.UserMessageCode);
        }

        public static string TombstoneStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var summary = viewModel?.TombstoneSummary ?? new SafeSyncTombstoneSummary();
            return $"Deletes: pending {summary.PendingDeleteCount} | synced {summary.DeleteSyncedCount} | failed {summary.DeleteFailedCount} | resolved {summary.DeleteResolvedCount}";
        }

        public static string SafeTombstoneLabel(SafeSyncTombstone tombstone)
        {
            if (tombstone == null)
            {
                return "Unknown tombstone";
            }

            return $"{tombstone.SyncStatus} | source {tombstone.DeleteSource} | code {tombstone.LastSafeErrorCode}";
        }

        public static string SafeConflictLabel(SafeSyncConflict conflict)
        {
            if (conflict == null)
            {
                return "Unknown conflict";
            }

            return $"{conflict.ConflictType} | {conflict.ResolutionStatus} | day {conflict.SafeLocalSummary?.DayBucket ?? conflict.SafeRemoteSummary?.DayBucket ?? string.Empty} | code {conflict.SafeErrorCode}";
        }

        public static string SafeConflictSummaryLine(SafeSyncSessionSafeSummary summary, bool includeServer)
        {
            summary = summary ?? new SafeSyncSessionSafeSummary();
            var id = includeServer ? ShortId(summary.ServerSessionId) : ShortId(summary.ClientSessionId);
            var idLabel = includeServer ? "server " : "client ";
            return (string.IsNullOrWhiteSpace(id) ? string.Empty : idLabel + id + " | ") +
                   summary.SourceProvider +
                   " | day " + summary.DayBucket +
                   (string.IsNullOrWhiteSpace(summary.TimeBucket) ? string.Empty : " | " + summary.TimeBucket) +
                   " | confidence " + summary.Confidence +
                   " | warnings " + summary.WarningCount +
                   " | category " + EmptyAsNone(summary.CategoryBucketSummary, summary.ActivityCategory) +
                   " | tool " + EmptyAsNone(summary.ToolBucketSummary, "none") +
                   " | language " + EmptyAsNone(summary.LanguageBucketSummary, "none");
        }

        public static string RemoteSessions(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel?.RemoteSafeSessions == null || viewModel.RemoteSafeSessions.Count == 0)
            {
                return "Remote: no fetched remote safe sessions.";
            }

            return string.Join("\n", viewModel.RemoteSafeSessions.Take(6).Select(SafeRemoteSessionLabel));
        }

        public static string SafeRemoteSessionLabel(RemoteSafeSessionSummary session)
        {
            if (session == null)
            {
                return "Unknown remote session";
            }

            return $"{session.DayBucket} | {session.SourceProvider} | {session.ActivityCategory} | changes {session.ChangeCountBucket} | lines {session.LineCountBucket} | sessions {session.SessionCountBucket} | interactions {session.InteractionCountBucket} | confidence {session.Confidence} | warnings {session.WarningCount} | schema {session.SchemaVersion}";
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= 8 ? value : value.Substring(0, 8);
        }

        private static string EmptyAsNone(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public static string ApprovedLocationLabel(ApprovedLocationDisplayItem item)
        {
            if (item == null)
            {
                return "No approved locations";
            }

            return item.DisplayAlias + (item.Enabled ? string.Empty : " (disabled)") + " / " + item.SourceType;
        }
    }
}
