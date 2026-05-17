using System;
using System.Collections.Generic;

namespace TokenForge.Client.Sync
{
    public enum SafeSyncConfirmationActionType
    {
        ApplyConflictMergePolicy,
        ForceRetry,
        ProcessRetryBatch,
        CancelRetryBatch,
        ProcessTombstoneBatch,
        CancelTombstoneBatch,
        ClearResolvedAuditHistory,
        MarkConflictResolved,
        KeepLocal,
        KeepRemote
    }

    public enum SafeSyncConfirmationRiskLevel
    {
        Low,
        Medium,
        High
    }

    [Serializable]
    public sealed class SafeSyncConfirmationRequest
    {
        public string ConfirmationId { get; set; } = Guid.NewGuid().ToString("N");
        public SafeSyncConfirmationActionType ActionType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string SafeActionLabel { get; set; } = string.Empty;
        public string SafeResultExpectation { get; set; } = string.Empty;
        public List<string> SafePreviewLines { get; set; } = new List<string>();
        public List<string> WarningLines { get; set; } = new List<string>();
        public string ConfirmButtonLabel { get; set; } = "Confirm";
        public string CancelButtonLabel { get; set; } = "Cancel";
        public bool RequiresTypedConfirmation { get; set; }
        public string TypedConfirmationPhrase { get; set; } = string.Empty;
        public string TypedPhrasePrompt { get; set; } = string.Empty;
        public string ValidationErrorMessage { get; set; } = "Typed confirmation did not match. No Safe Sync action was applied.";
        public string CancelResultMessage { get; set; } = "Safe Sync action canceled. No changes were applied.";
        public string StaleStateMessage { get; set; } = "Safe Sync state changed. Review the latest safe summary before trying again.";
        public string SuccessResultMessage { get; set; } = "Safe Sync action completed. Review the updated safe summary.";
        public string FailureResultMessage { get; set; } = "Safe Sync action did not complete. Review the safe status message.";
        public SafeSyncConfirmationRiskLevel RiskLevel { get; set; } = SafeSyncConfirmationRiskLevel.Medium;
    }

    [Serializable]
    public sealed class SafeSyncConfirmationResult
    {
        public string ConfirmationId { get; set; } = string.Empty;
        public bool Confirmed { get; set; }
        public bool TypedPhraseMatched { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
