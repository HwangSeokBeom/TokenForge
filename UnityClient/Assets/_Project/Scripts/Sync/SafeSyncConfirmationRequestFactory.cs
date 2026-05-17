using System;
using System.Collections.Generic;
using System.Linq;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Sync
{
    public static class SafeSyncConfirmationRequestFactory
    {
        private static readonly PrivacySanitizer Sanitizer = new PrivacySanitizer();

        public static SafeSyncConfirmationRequest ForMergePreview(SafeConflictMergePreview preview)
        {
            preview = preview ?? new SafeConflictMergePreview { CanApply = false, BlockedReason = SafeSyncApiError.MergePolicyPreviewBlocked };
            if (!preview.CanApply)
            {
                return null;
            }

            var policyLabel = PolicyLabel(preview.EffectivePolicy);
            var request = Base(
                SafeSyncConfirmationActionType.ApplyConflictMergePolicy,
                "Apply merge policy",
                "Apply " + policyLabel + " using safe aggregate fields only.",
                "Apply",
                SafeSyncConfirmationRiskLevel.High,
                "MERGE");
            request.SafePreviewLines.Add("Selected policy: " + PolicyLabel(preview.SelectedPolicy));
            request.SafePreviewLines.Add("Effective policy: " + policyLabel);
            request.SafePreviewLines.Add("Changed safe fields: " + SafeList(preview.DiffSummary?.ChangedSafeFieldNames, "none"));
            SetExpectation(request, MergeExpectation(preview.EffectivePolicy));
            AddWarnings(request, preview.Warnings);
            return SafeOrBlocked(request);
        }

        public static SafeSyncConfirmationRequest ForConflictAction(SafeSyncConfirmationActionType actionType, SafeSyncConflict conflict)
        {
            var request = Base(actionType, ConflictTitle(actionType), ConflictBody(actionType), "Confirm", RiskFor(actionType), PhraseFor(actionType));
            request.SafePreviewLines.Add("Conflict: " + SafeConflictKind(conflict));
            request.SafePreviewLines.Add("Changed safe fields: " + SafeList(conflict?.SafeDiffSummary?.ChangedSafeFieldNames, "none"));
            SetExpectation(request, ConflictExpectation(actionType));
            if ((conflict?.SafeLocalSummary?.WarningCount ?? 0) > 0 || (conflict?.SafeRemoteSummary?.WarningCount ?? 0) > 0)
            {
                request.WarningLines.Add("Warning count: local " + (conflict?.SafeLocalSummary?.WarningCount ?? 0) + " | remote " + (conflict?.SafeRemoteSummary?.WarningCount ?? 0));
            }
            return SafeOrBlocked(request);
        }

        public static SafeSyncConfirmationRequest ForRetryAction(SafeSyncConfirmationActionType actionType, SafeSyncRetryQueueSummary summary, SafeSyncRetryQueueEntry selectedEntry = null)
        {
            var request = Base(actionType, RetryTitle(actionType), RetryBody(actionType), "Confirm", RiskFor(actionType), PhraseFor(actionType));
            summary = summary ?? new SafeSyncRetryQueueSummary();
            request.SafePreviewLines.Add("Pending: " + summary.PendingCount + " | failed: " + summary.FailedCount + " | paused: " + summary.PausedCount + " | succeeded: " + summary.SucceededCount);
            if (selectedEntry != null)
            {
                request.SafePreviewLines.Add("Selected entry: " + selectedEntry.OperationType + " | " + selectedEntry.Status + " | attempts " + selectedEntry.AttemptCount + "/" + selectedEntry.MaxAttempts);
                request.SafePreviewLines.Add("Safe status: " + SafeCode(selectedEntry.LastSafeErrorCode));
            }

            SetExpectation(request, RetryExpectation(actionType));
            return SafeOrBlocked(request);
        }

        public static SafeSyncConfirmationRequest ForTombstoneAction(SafeSyncConfirmationActionType actionType, SafeSyncTombstoneSummary summary, SafeSyncTombstone selectedTombstone = null)
        {
            var request = Base(actionType, TombstoneTitle(actionType), TombstoneBody(actionType), "Confirm", RiskFor(actionType), PhraseFor(actionType));
            summary = summary ?? new SafeSyncTombstoneSummary();
            request.SafePreviewLines.Add("Pending: " + summary.PendingDeleteCount + " | failed: " + summary.DeleteFailedCount + " | synced: " + summary.DeleteSyncedCount + " | resolved: " + summary.DeleteResolvedCount);
            if (selectedTombstone != null)
            {
                request.SafePreviewLines.Add("Selected delete state: " + selectedTombstone.SyncStatus + " | source " + selectedTombstone.DeleteSource);
                request.SafePreviewLines.Add("Safe status: " + SafeCode(selectedTombstone.LastSafeErrorCode));
            }

            SetExpectation(request, TombstoneExpectation(actionType));
            return SafeOrBlocked(request);
        }

        public static SafeSyncConfirmationRequest ForClearResolvedAuditHistory(SafeConflictAuditSummary summary)
        {
            summary = summary ?? new SafeConflictAuditSummary();
            var request = Base(
                SafeSyncConfirmationActionType.ClearResolvedAuditHistory,
                "Clear resolved history",
                "Clear resolved local-only conflict audit entries. Failed entries remain available.",
                "Clear history",
                SafeSyncConfirmationRiskLevel.High,
                "CLEAR HISTORY");
            request.SafePreviewLines.Add("Resolved audit entries: " + summary.ResolvedCount);
            request.SafePreviewLines.Add("Retention cap: latest 200 local-only entries.");
            SetExpectation(request, "resolved conflict audit entries are removed from local-only history.");
            return SafeOrBlocked(request);
        }

        public static SafeSyncConfirmationResult Cancel(SafeSyncConfirmationRequest request)
        {
            return new SafeSyncConfirmationResult
            {
                ConfirmationId = request?.ConfirmationId ?? string.Empty,
                Confirmed = false,
                TypedPhraseMatched = false
            };
        }

        public static SafeSyncConfirmationResult Confirm(SafeSyncConfirmationRequest request, string typedPhrase)
        {
            request = request ?? new SafeSyncConfirmationRequest();
            var matched = !request.RequiresTypedConfirmation ||
                          string.Equals((typedPhrase ?? string.Empty).Trim(), request.TypedConfirmationPhrase, StringComparison.Ordinal);
            return new SafeSyncConfirmationResult
            {
                ConfirmationId = request.ConfirmationId,
                Confirmed = matched,
                TypedPhraseMatched = matched
            };
        }

        public static bool ContainsOnlySafeText(SafeSyncConfirmationRequest request)
        {
            if (request == null)
            {
                return false;
            }

            return AllText(request).All(IsSafeText);
        }

        public static IEnumerable<string> AllText(SafeSyncConfirmationRequest request)
        {
            if (request == null)
            {
                return Enumerable.Empty<string>();
            }

            return new[]
                {
                    request.Title,
                    request.Body,
                    request.SafeActionLabel,
                    request.SafeResultExpectation,
                    request.ConfirmButtonLabel,
                    request.CancelButtonLabel,
                    request.TypedConfirmationPhrase,
                    request.TypedPhrasePrompt,
                    request.ValidationErrorMessage,
                    request.CancelResultMessage,
                    request.StaleStateMessage,
                    request.SuccessResultMessage,
                    request.FailureResultMessage
                }
                .Concat(request.SafePreviewLines ?? new List<string>())
                .Concat(request.WarningLines ?? new List<string>())
                .Where(text => !string.IsNullOrWhiteSpace(text));
        }

        public static string PolicyLabel(SafeConflictMergePolicy policy)
        {
            switch (policy)
            {
                case SafeConflictMergePolicy.KeepLocal:
                    return "Keep Local";
                case SafeConflictMergePolicy.KeepRemote:
                    return "Keep Remote";
                case SafeConflictMergePolicy.PreferHigherConfidence:
                    return "Prefer Higher Confidence";
                case SafeConflictMergePolicy.PreferNewerSafeTimestamp:
                    return "Prefer Newer Safe Timestamp";
                case SafeConflictMergePolicy.MergeNonConflictingAggregates:
                    return "Merge Non-Conflicting Aggregates";
                case SafeConflictMergePolicy.MarkResolvedOnly:
                    return "Mark Resolved Only";
                default:
                    return "Unknown Policy";
            }
        }

        private static SafeSyncConfirmationRequest Base(SafeSyncConfirmationActionType actionType, string title, string body, string confirmLabel, SafeSyncConfirmationRiskLevel riskLevel, string phrase)
        {
            return new SafeSyncConfirmationRequest
            {
                ActionType = actionType,
                Title = title,
                Body = body,
                SafeActionLabel = ActionLabel(actionType),
                ConfirmButtonLabel = confirmLabel,
                CancelButtonLabel = "Cancel",
                RiskLevel = riskLevel,
                RequiresTypedConfirmation = riskLevel == SafeSyncConfirmationRiskLevel.High,
                TypedConfirmationPhrase = riskLevel == SafeSyncConfirmationRiskLevel.High ? phrase : string.Empty,
                TypedPhrasePrompt = riskLevel == SafeSyncConfirmationRiskLevel.High ? "Type " + phrase + " to confirm." : string.Empty,
                ValidationErrorMessage = "Typed confirmation did not match. No Safe Sync action was applied.",
                CancelResultMessage = "Safe Sync action canceled. No changes were applied.",
                StaleStateMessage = "Safe Sync state changed. Review the latest safe summary before trying again.",
                SuccessResultMessage = "Safe Sync action completed. Review the updated safe summary.",
                FailureResultMessage = "Safe Sync action did not complete. Review the safe status message."
            };
        }

        private static void SetExpectation(SafeSyncConfirmationRequest request, string expectation)
        {
            request.SafeResultExpectation = expectation;
            request.SafePreviewLines.Add("Expected result: " + expectation);
        }

        private static SafeSyncConfirmationRequest SafeOrBlocked(SafeSyncConfirmationRequest request)
        {
            return ContainsOnlySafeText(request) ? request : null;
        }

        private static void AddWarnings(SafeSyncConfirmationRequest request, IEnumerable<string> warnings)
        {
            foreach (var warning in warnings ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(warning) && IsSafeText(warning))
                {
                    request.WarningLines.Add("Warning: " + warning);
                }
            }
        }

        private static string SafeList(IEnumerable<string> values, string fallback)
        {
            var safe = (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value) && IsSafeText(value))
                .Take(6)
                .ToList();
            return safe.Count == 0 ? fallback : string.Join(", ", safe);
        }

        private static string SafeCode(string value)
        {
            return string.IsNullOrWhiteSpace(value) || !IsSafeText(value) ? "none" : value;
        }

        private static bool IsSafeText(string value)
        {
            value = value ?? string.Empty;
            return value.IndexOf("{\"", StringComparison.Ordinal) < 0 &&
                   value.IndexOf("/Users/", StringComparison.OrdinalIgnoreCase) < 0 &&
                   value.IndexOf("\\Users\\", StringComparison.OrdinalIgnoreCase) < 0 &&
                   Sanitizer.ValidateNoForbiddenFields(value).IsSuccess;
        }

        private static string SafeConflictKind(SafeSyncConflict conflict)
        {
            if (conflict == null)
            {
                return "unknown";
            }

            return conflict.ConflictType + " | " + conflict.ResolutionStatus + " | code " + SafeCode(conflict.SafeErrorCode);
        }

        private static SafeSyncConfirmationRiskLevel RiskFor(SafeSyncConfirmationActionType actionType)
        {
            switch (actionType)
            {
                case SafeSyncConfirmationActionType.ApplyConflictMergePolicy:
                case SafeSyncConfirmationActionType.ForceRetry:
                case SafeSyncConfirmationActionType.CancelTombstoneBatch:
                case SafeSyncConfirmationActionType.ClearResolvedAuditHistory:
                    return SafeSyncConfirmationRiskLevel.High;
                case SafeSyncConfirmationActionType.KeepRemote:
                case SafeSyncConfirmationActionType.KeepLocal:
                case SafeSyncConfirmationActionType.MarkConflictResolved:
                case SafeSyncConfirmationActionType.ProcessTombstoneBatch:
                case SafeSyncConfirmationActionType.ProcessRetryBatch:
                    return SafeSyncConfirmationRiskLevel.Medium;
                default:
                    return SafeSyncConfirmationRiskLevel.Low;
            }
        }

        private static string PhraseFor(SafeSyncConfirmationActionType actionType)
        {
            switch (actionType)
            {
                case SafeSyncConfirmationActionType.ApplyConflictMergePolicy:
                    return "MERGE";
                case SafeSyncConfirmationActionType.ForceRetry:
                    return "FORCE RETRY";
                case SafeSyncConfirmationActionType.CancelTombstoneBatch:
                    return "CANCEL DELETE";
                case SafeSyncConfirmationActionType.ClearResolvedAuditHistory:
                    return "CLEAR HISTORY";
                default:
                    return string.Empty;
            }
        }

        private static string ActionLabel(SafeSyncConfirmationActionType actionType)
        {
            switch (actionType)
            {
                case SafeSyncConfirmationActionType.ApplyConflictMergePolicy:
                    return "applyMergePolicy";
                case SafeSyncConfirmationActionType.ForceRetry:
                    return "forceRetry";
                case SafeSyncConfirmationActionType.ProcessRetryBatch:
                    return "processRetryBatch";
                case SafeSyncConfirmationActionType.CancelRetryBatch:
                    return "updateRetryBatch";
                case SafeSyncConfirmationActionType.ProcessTombstoneBatch:
                    return "processDeleteTombstones";
                case SafeSyncConfirmationActionType.CancelTombstoneBatch:
                    return "updateDeleteTombstones";
                case SafeSyncConfirmationActionType.ClearResolvedAuditHistory:
                    return "clearResolvedConflictAuditHistory";
                case SafeSyncConfirmationActionType.MarkConflictResolved:
                    return "markConflictResolved";
                case SafeSyncConfirmationActionType.KeepLocal:
                    return "keepLocal";
                case SafeSyncConfirmationActionType.KeepRemote:
                    return "keepRemote";
                default:
                    return "safeSyncAction";
            }
        }

        private static string ConflictTitle(SafeSyncConfirmationActionType actionType)
        {
            switch (actionType)
            {
                case SafeSyncConfirmationActionType.KeepLocal:
                    return "Keep local conflict";
                case SafeSyncConfirmationActionType.KeepRemote:
                    return "Keep remote conflict";
                case SafeSyncConfirmationActionType.MarkConflictResolved:
                    return "Mark conflict resolved";
                default:
                    return "Apply conflict action";
            }
        }

        private static string ConflictBody(SafeSyncConfirmationActionType actionType)
        {
            switch (actionType)
            {
                case SafeSyncConfirmationActionType.KeepLocal:
                    return "Queue the local safe aggregate for explicit retry.";
                case SafeSyncConfirmationActionType.KeepRemote:
                    return "Apply the safe remote aggregate locally when validation allows it.";
                case SafeSyncConfirmationActionType.MarkConflictResolved:
                    return "Record a resolved decision without changing local aggregate data.";
                default:
                    return "Apply this explicit conflict action.";
            }
        }

        private static string ConflictExpectation(SafeSyncConfirmationActionType actionType)
        {
            switch (actionType)
            {
                case SafeSyncConfirmationActionType.KeepLocal:
                    return "a retry entry is queued for explicit processing.";
                case SafeSyncConfirmationActionType.KeepRemote:
                    return "local aggregate state changes only after remote aggregate validation.";
                case SafeSyncConfirmationActionType.MarkConflictResolved:
                    return "the conflict is marked resolved without data changes.";
                default:
                    return "the selected conflict action runs once.";
            }
        }

        private static string RetryTitle(SafeSyncConfirmationActionType actionType)
        {
            return actionType == SafeSyncConfirmationActionType.ForceRetry ? "Force retry selected entry" :
                actionType == SafeSyncConfirmationActionType.ProcessRetryBatch ? "Process eligible retries" : "Update retry batch";
        }

        private static string RetryBody(SafeSyncConfirmationActionType actionType)
        {
            return actionType == SafeSyncConfirmationActionType.ForceRetry
                ? "Run the selected safe retry entry now, bypassing its next attempt time only after confirmation."
                : "Run the selected retry queue action once. No background retry is started.";
        }

        private static string RetryExpectation(SafeSyncConfirmationActionType actionType)
        {
            return actionType == SafeSyncConfirmationActionType.ForceRetry
                ? "the selected retry entry is attempted once, unless auth, unsafe, or permanent-failure blocks apply."
                : "eligible retry entries are updated once according to current safe status.";
        }

        private static string TombstoneTitle(SafeSyncConfirmationActionType actionType)
        {
            return actionType == SafeSyncConfirmationActionType.ProcessTombstoneBatch ? "Process pending deletes" : "Update delete tombstones";
        }

        private static string TombstoneBody(SafeSyncConfirmationActionType actionType)
        {
            return actionType == SafeSyncConfirmationActionType.ProcessTombstoneBatch
                ? "Process pending remote deletes once. No background delete processing is started."
                : "Update local-only delete tombstone state.";
        }

        private static string TombstoneExpectation(SafeSyncConfirmationActionType actionType)
        {
            return actionType == SafeSyncConfirmationActionType.ProcessTombstoneBatch
                ? "pending delete tombstones are processed once when authentication and validation allow it."
                : "selected local-only tombstone entries are cancelled or cleared.";
        }

        private static string MergeExpectation(SafeConflictMergePolicy policy)
        {
            switch (policy)
            {
                case SafeConflictMergePolicy.KeepLocal:
                    return "local safe aggregate is queued for explicit retry.";
                case SafeConflictMergePolicy.KeepRemote:
                    return "safe remote aggregate is validated before any local apply.";
                case SafeConflictMergePolicy.MarkResolvedOnly:
                    return "conflict is marked resolved without changing aggregate data.";
                case SafeConflictMergePolicy.MergeNonConflictingAggregates:
                    return "non-conflicting safe aggregate fields are merged locally and queued for retry.";
                default:
                    return "policy result is applied once after confirmation.";
            }
        }
    }
}
