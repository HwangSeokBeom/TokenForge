using System.Linq;
using TokenForge.Client.Sync;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class SafeSyncPanelView : UiBinderBase
    {
        [SerializeField] public Text noticeLabel;
        [SerializeField] public Text statusLabel;
        [SerializeField] public InputField baseUrlInput;
        [SerializeField] public Button healthButton;
        [SerializeField] public Button syncButton;
        [SerializeField] public Button fetchButton;
        [SerializeField] public Dropdown remoteSessionsDropdown;
        [SerializeField] public Button deleteRemoteButton;
        [SerializeField] public Text retryQueueLabel;
        [SerializeField] public Text conflictLabel;
        [SerializeField] public Text tombstoneLabel;
        [SerializeField] public Dropdown retryEntriesDropdown;
        [SerializeField] public Button retryPendingButton;
        [SerializeField] public Button forceRetryButton;
        [SerializeField] public Button cancelRetryButton;
        [SerializeField] public Button cancelAllFailedRetryButton;
        [SerializeField] public Button clearSucceededButton;
        [SerializeField] public Button pauseAllPendingRetryButton;
        [SerializeField] public Button resumeAllPausedRetryButton;
        [SerializeField] public Dropdown tombstonesDropdown;
        [SerializeField] public Button enqueueTombstoneDeletesButton;
        [SerializeField] public Button processTombstoneDeletesButton;
        [SerializeField] public Button cancelTombstoneButton;
        [SerializeField] public Button cancelAllFailedTombstonesButton;
        [SerializeField] public Button clearResolvedTombstonesButton;
        [SerializeField] public Dropdown conflictsDropdown;
        [SerializeField] public Button keepLocalButton;
        [SerializeField] public Button keepRemoteButton;
        [SerializeField] public Button applyMergePolicyButton;
        [SerializeField] public Button markConflictResolvedButton;
        [SerializeField] public Button clearConflictAuditButton;
        [SerializeField] public Button cancelConflictResolutionButton;
        [SerializeField] public GameObject confirmationPanel;
        [SerializeField] public Text confirmationTitleLabel;
        [SerializeField] public Text confirmationBodyLabel;
        [SerializeField] public Text confirmationPreviewLabel;
        [SerializeField] public InputField confirmationTypedPhraseInput;
        [SerializeField] public Button confirmationConfirmButton;
        [SerializeField] public Button confirmationCancelButton;

        private ApprovedActivityAnalysisViewModel viewModel;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            this.viewModel = viewModel;
            EnsureConfirmationPanel();
            EnsureMergePolicyButton();

            ReplaceClick(healthButton, () =>
            {
                this.viewModel.SetSafeSyncBaseUrl(baseUrlInput != null ? baseUrlInput.text : string.Empty);
                RunViewModelAction(() => this.viewModel.CheckSyncHealthAsync());
            });
            ReplaceClick(syncButton, () =>
            {
                this.viewModel.SetSafeSyncBaseUrl(baseUrlInput != null ? baseUrlInput.text : string.Empty);
                RunViewModelAction(() => this.viewModel.SyncSafeSessionsAsync());
            });
            ReplaceClick(fetchButton, () =>
            {
                this.viewModel.SetSafeSyncBaseUrl(baseUrlInput != null ? baseUrlInput.text : string.Empty);
                RunViewModelAction(() => this.viewModel.FetchSyncedSessionsAsync());
            });
            ReplaceClick(deleteRemoteButton, () => RunViewModelAction(() => this.viewModel.DeleteRemoteSessionAsync(SelectedRemoteSessionId())));
            ReplaceClick(retryPendingButton, () => BeginConfirmation(() => this.viewModel.BeginProcessRetryBatchConfirmation()));
            ReplaceClick(forceRetryButton, () => BeginConfirmation(() => this.viewModel.BeginForceRetryEntryConfirmation(SelectedRetryQueueEntryId())));
            ReplaceClick(cancelRetryButton, () => BeginConfirmation(() => this.viewModel.BeginCancelRetryBatchConfirmation(token => this.viewModel.CancelRetryEntryAsync(SelectedRetryQueueEntryId(), token))));
            ReplaceClick(cancelAllFailedRetryButton, () => BeginConfirmation(() => this.viewModel.BeginCancelRetryBatchConfirmation(token => this.viewModel.CancelAllFailedRetryEntriesAsync(token))));
            ReplaceClick(clearSucceededButton, () => BeginConfirmation(() => this.viewModel.BeginCancelRetryBatchConfirmation(token => this.viewModel.ClearSucceededRetryEntriesAsync(token))));
            ReplaceClick(pauseAllPendingRetryButton, () => BeginConfirmation(() => this.viewModel.BeginCancelRetryBatchConfirmation(token => this.viewModel.PauseAllPendingRetryEntriesAsync(token))));
            ReplaceClick(resumeAllPausedRetryButton, () => BeginConfirmation(() => this.viewModel.BeginCancelRetryBatchConfirmation(token => this.viewModel.ResumeAllPausedRetryEntriesAsync(token))));
            ReplaceClick(enqueueTombstoneDeletesButton, () => RunViewModelAction(() => this.viewModel.EnqueuePendingTombstoneDeletesAsync()));
            ReplaceClick(processTombstoneDeletesButton, () => BeginConfirmation(() => this.viewModel.BeginProcessTombstoneBatchConfirmation()));
            ReplaceClick(cancelTombstoneButton, () => BeginConfirmation(() => this.viewModel.BeginCancelTombstoneBatchConfirmation(token => this.viewModel.CancelTombstoneAsync(SelectedTombstoneId(), token), SelectedTombstoneId())));
            ReplaceClick(cancelAllFailedTombstonesButton, () => BeginConfirmation(() => this.viewModel.BeginCancelTombstoneBatchConfirmation(token => this.viewModel.CancelAllFailedTombstonesAsync(token))));
            ReplaceClick(clearResolvedTombstonesButton, () => BeginConfirmation(() => this.viewModel.BeginCancelTombstoneBatchConfirmation(token => this.viewModel.ClearResolvedTombstonesAsync(token))));
            ReplaceClick(keepLocalButton, () => BeginConfirmation(() => this.viewModel.BeginKeepLocalConflictConfirmation(SelectedConflictId())));
            ReplaceClick(keepRemoteButton, () => BeginConfirmation(() => this.viewModel.BeginKeepRemoteConflictConfirmation(SelectedConflictId())));
            ReplaceClick(applyMergePolicyButton, () => RunViewModelAction(async () =>
            {
                await this.viewModel.BeginApplyConflictMergePolicyConfirmationAsync(SelectedConflictId(), SafeConflictMergePolicy.MergeNonConflictingAggregates);
            }));
            ReplaceClick(markConflictResolvedButton, () => BeginConfirmation(() => this.viewModel.BeginMarkConflictResolvedConfirmation(SelectedConflictId())));
            ReplaceClick(clearConflictAuditButton, () => BeginConfirmation(() => this.viewModel.BeginClearResolvedConflictAuditHistoryConfirmation()));
            ReplaceClick(cancelConflictResolutionButton, () => RunViewModelAction(() => this.viewModel.CancelConflictResolutionAsync(SelectedConflictId())));
            ReplaceClick(confirmationCancelButton, () =>
            {
                this.viewModel.CancelSafeSyncConfirmation();
                Render();
            });
            ReplaceClick(confirmationConfirmButton, () =>
            {
                this.viewModel.SafeSyncConfirmationTypedPhrase = confirmationTypedPhraseInput != null ? confirmationTypedPhraseInput.text : string.Empty;
                RunViewModelAction(() => this.viewModel.ConfirmSafeSyncConfirmationAsync());
            });

            Render();
        }

        public void Render()
        {
            SetText(noticeLabel, "Only privacy-safe aggregate sessions are synced.\nApproved locations and raw local data are never synced.");
            SetText(statusLabel, BootstrapUiTextFormatter.SafeSyncStatus(viewModel));
            SetText(retryQueueLabel, BootstrapUiTextFormatter.RetryQueueStatus(viewModel));
            SetText(conflictLabel, BootstrapUiTextFormatter.ConflictStatus(viewModel) + "\n" + BootstrapUiTextFormatter.ConflictAuditHistory(viewModel));
            SetText(tombstoneLabel, BootstrapUiTextFormatter.TombstoneStatus(viewModel));
            RenderConfirmationPanel();
            if (viewModel != null && baseUrlInput != null && !baseUrlInput.isFocused)
            {
                baseUrlInput.text = viewModel.SafeSyncBaseUrl;
            }

            FillRemoteDropdown();
            FillRetryDropdown();
            FillTombstoneDropdown();
            FillConflictDropdown();

            var hasService = viewModel != null && viewModel.HasSafeSyncService;
            var busy = viewModel != null && viewModel.IsSafeSyncRequestInProgress;
            var authenticated = viewModel != null && viewModel.CanUseAuthenticatedSafeSync;
            SetButton(healthButton, hasService && !busy);
            SetButton(syncButton, hasService && authenticated && !busy);
            SetButton(fetchButton, hasService && authenticated && !busy);
            SetButton(deleteRemoteButton, hasService && authenticated && !busy && viewModel.RemoteSafeSessions.Count > 0);
            SetButton(retryPendingButton, hasService && authenticated && !busy && viewModel.RetryQueueSummary.PendingCount > 0);
            SetButton(forceRetryButton, hasService && authenticated && !busy && viewModel.RetryQueueSummary.SafeEntries.Any(entry => entry.Status == TokenForge.Client.Sync.SafeSyncRetryQueueEntryStatus.Pending || entry.Status == TokenForge.Client.Sync.SafeSyncRetryQueueEntryStatus.Failed || entry.Status == TokenForge.Client.Sync.SafeSyncRetryQueueEntryStatus.Conflict));
            SetButton(cancelRetryButton, hasService && !busy && viewModel.RetryQueueSummary.SafeEntries.Any(entry => entry.Status == TokenForge.Client.Sync.SafeSyncRetryQueueEntryStatus.Pending || entry.Status == TokenForge.Client.Sync.SafeSyncRetryQueueEntryStatus.Paused || entry.Status == TokenForge.Client.Sync.SafeSyncRetryQueueEntryStatus.Failed));
            SetButton(cancelAllFailedRetryButton, hasService && !busy && viewModel.RetryQueueSummary.FailedCount > 0);
            SetButton(clearSucceededButton, hasService && !busy && viewModel.RetryQueueSummary.SucceededCount > 0);
            SetButton(pauseAllPendingRetryButton, hasService && !busy && viewModel.RetryQueueSummary.PendingCount > 0);
            SetButton(resumeAllPausedRetryButton, hasService && !busy && viewModel.RetryQueueSummary.PausedCount > 0);
            SetButton(enqueueTombstoneDeletesButton, hasService && !busy && viewModel.TombstoneSummary.PendingDeleteCount > 0);
            SetButton(processTombstoneDeletesButton, hasService && authenticated && !busy && viewModel.TombstoneSummary.PendingDeleteCount > 0);
            SetButton(cancelTombstoneButton, hasService && !busy && viewModel.TombstoneSummary.SafeTombstones.Any(item => item.SyncStatus == TokenForge.Client.Sync.SafeSyncTombstoneStatus.PendingDelete || item.SyncStatus == TokenForge.Client.Sync.SafeSyncTombstoneStatus.DeleteFailed));
            SetButton(cancelAllFailedTombstonesButton, hasService && !busy && viewModel.TombstoneSummary.DeleteFailedCount > 0);
            SetButton(clearResolvedTombstonesButton, hasService && !busy && viewModel.TombstoneSummary.DeleteResolvedCount + viewModel.TombstoneSummary.DeleteSyncedCount > 0);
            SetButton(keepLocalButton, hasService && !busy && viewModel.ConflictSummary.SafeConflicts.Any(item => item.ResolutionStatus == TokenForge.Client.Sync.SafeSyncConflictResolutionStatus.Unresolved));
            SetButton(keepRemoteButton, hasService && !busy && viewModel.ConflictSummary.SafeConflicts.Any(item => item.ResolutionStatus == TokenForge.Client.Sync.SafeSyncConflictResolutionStatus.Unresolved));
            SetButton(applyMergePolicyButton, hasService && !busy && viewModel.ConflictSummary.SafeConflicts.Any(item => item.ResolutionStatus == TokenForge.Client.Sync.SafeSyncConflictResolutionStatus.Unresolved));
            SetButton(markConflictResolvedButton, hasService && !busy && viewModel.ConflictSummary.SafeConflicts.Any(item => item.ResolutionStatus == TokenForge.Client.Sync.SafeSyncConflictResolutionStatus.Unresolved));
            SetButton(clearConflictAuditButton, hasService && !busy && viewModel.ConflictAuditSummary.ResolvedCount > 0);
            SetButton(cancelConflictResolutionButton, hasService && !busy && viewModel.ConflictSummary.SafeConflicts.Any(item => item.ResolutionStatus == TokenForge.Client.Sync.SafeSyncConflictResolutionStatus.Unresolved));
        }

        private void BeginConfirmation(System.Func<SafeSyncConfirmationRequest> requestFactory)
        {
            requestFactory?.Invoke();
            Render();
        }

        private void RenderConfirmationPanel()
        {
            EnsureConfirmationPanel();
            var request = viewModel?.PendingSafeSyncConfirmation;
            if (confirmationPanel == null)
            {
                return;
            }

            confirmationPanel.SetActive(request != null);
            if (request == null)
            {
                return;
            }

            SetText(confirmationTitleLabel, request.Title);
            var typed = request.RequiresTypedConfirmation
                ? "\n" + request.TypedPhrasePrompt
                : string.Empty;
            SetText(confirmationBodyLabel, request.Body + typed);
            var warnings = request.WarningLines == null || request.WarningLines.Count == 0
                ? "Warnings: none"
                : "Warnings:\n" + string.Join("\n", request.WarningLines);
            var qaLines = new System.Collections.Generic.List<string>
            {
                "Action: " + request.SafeActionLabel,
                "Expected result: " + request.SafeResultExpectation
            };
            qaLines.AddRange(request.SafePreviewLines ?? new System.Collections.Generic.List<string>());
            SetText(confirmationPreviewLabel, string.Join("\n", qaLines) + "\n" + warnings);
            SetText(ButtonLabel(confirmationConfirmButton), request.ConfirmButtonLabel);
            SetText(ButtonLabel(confirmationCancelButton), request.CancelButtonLabel);
            if (confirmationTypedPhraseInput != null)
            {
                confirmationTypedPhraseInput.gameObject.SetActive(request.RequiresTypedConfirmation);
                if (!confirmationTypedPhraseInput.isFocused)
                {
                    confirmationTypedPhraseInput.text = viewModel.SafeSyncConfirmationTypedPhrase;
                }
            }
        }

        private void EnsureConfirmationPanel()
        {
            if (confirmationPanel != null)
            {
                return;
            }

            confirmationPanel = new GameObject("Safe Sync Confirmation Modal", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            confirmationPanel.transform.SetParent(transform, false);
            confirmationPanel.GetComponent<Image>().color = new Color(0.075f, 0.095f, 0.125f, 1f);
            var layout = confirmationPanel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            confirmationPanel.GetComponent<LayoutElement>().minHeight = 210f;
            confirmationTitleLabel = RuntimeText("Confirmation Title", confirmationPanel.transform, 15, FontStyle.Bold, 26f);
            confirmationBodyLabel = RuntimeText("Confirmation Body", confirmationPanel.transform, 13, FontStyle.Normal, 44f);
            confirmationPreviewLabel = RuntimeText("Confirmation Preview", confirmationPanel.transform, 12, FontStyle.Normal, 76f);
            confirmationTypedPhraseInput = RuntimeInput("Typed Confirmation", confirmationPanel.transform);
            var row = new GameObject("Confirmation Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(confirmationPanel.transform, false);
            row.GetComponent<HorizontalLayoutGroup>().spacing = 8f;
            row.GetComponent<LayoutElement>().minHeight = 36f;
            confirmationConfirmButton = RuntimeButton("Confirm", row.transform);
            confirmationCancelButton = RuntimeButton("Cancel", row.transform);
            confirmationPanel.SetActive(false);
        }

        private void EnsureMergePolicyButton()
        {
            if (applyMergePolicyButton != null)
            {
                return;
            }

            applyMergePolicyButton = RuntimeButton("Apply Merge", transform);
        }

        private Text RuntimeText(string name, Transform parent, int size, FontStyle style, float minHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = new Color(0.92f, 0.95f, 0.98f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            go.GetComponent<LayoutElement>().minHeight = minHeight;
            return text;
        }

        private InputField RuntimeInput(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.045f, 0.055f, 0.075f, 1f);
            go.GetComponent<LayoutElement>().minHeight = 32f;
            var text = RuntimeText("Text", go.transform, 13, FontStyle.Normal, 28f);
            var placeholder = RuntimeText("Placeholder", go.transform, 13, FontStyle.Italic, 28f);
            placeholder.text = "Typed confirmation";
            var input = go.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private Button RuntimeButton(string label, Transform parent)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.93f, 0.58f, 0.26f, 1f);
            go.GetComponent<LayoutElement>().preferredWidth = 140f;
            go.GetComponent<LayoutElement>().minHeight = 34f;
            var labelText = RuntimeText("Label", go.transform, 13, FontStyle.Bold, 30f);
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.text = label;
            return go.GetComponent<Button>();
        }

        private static Text ButtonLabel(Button button)
        {
            return button == null ? null : button.GetComponentInChildren<Text>(true);
        }

        private string SelectedRemoteSessionId()
        {
            var index = remoteSessionsDropdown != null ? remoteSessionsDropdown.value : -1;
            return viewModel != null && index >= 0 && index < viewModel.RemoteSafeSessions.Count
                ? viewModel.RemoteSafeSessions[index].ServerSessionId
                : string.Empty;
        }

        private string SelectedRetryQueueEntryId()
        {
            var index = retryEntriesDropdown != null ? retryEntriesDropdown.value : -1;
            var entries = viewModel?.RetryQueueSummary?.SafeEntries ?? new System.Collections.Generic.List<TokenForge.Client.Sync.SafeSyncRetryQueueEntry>();
            return index >= 0 && index < entries.Count
                ? entries[index].QueueEntryId
                : string.Empty;
        }

        private string SelectedTombstoneId()
        {
            var index = tombstonesDropdown != null ? tombstonesDropdown.value : -1;
            var tombstones = viewModel?.TombstoneSummary?.SafeTombstones ?? new System.Collections.Generic.List<TokenForge.Client.Sync.SafeSyncTombstone>();
            return index >= 0 && index < tombstones.Count
                ? tombstones[index].TombstoneId
                : string.Empty;
        }

        private string SelectedConflictId()
        {
            var index = conflictsDropdown != null ? conflictsDropdown.value : -1;
            var conflicts = viewModel?.ConflictSummary?.SafeConflicts ?? new System.Collections.Generic.List<TokenForge.Client.Sync.SafeSyncConflict>();
            return index >= 0 && index < conflicts.Count
                ? conflicts[index].ConflictId
                : string.Empty;
        }

        private void FillRemoteDropdown()
        {
            if (remoteSessionsDropdown == null)
            {
                return;
            }

            remoteSessionsDropdown.options = viewModel == null || viewModel.RemoteSafeSessions.Count == 0
                ? new System.Collections.Generic.List<Dropdown.OptionData> { new Dropdown.OptionData("No remote sessions") }
                : viewModel.RemoteSafeSessions.Select(item => new Dropdown.OptionData(BootstrapUiTextFormatter.SafeRemoteSessionLabel(item))).ToList();
            if (remoteSessionsDropdown.value >= remoteSessionsDropdown.options.Count)
            {
                remoteSessionsDropdown.value = 0;
            }

            remoteSessionsDropdown.RefreshShownValue();
        }

        private void FillRetryDropdown()
        {
            if (retryEntriesDropdown == null)
            {
                return;
            }

            var entries = viewModel?.RetryQueueSummary?.SafeEntries ?? new System.Collections.Generic.List<TokenForge.Client.Sync.SafeSyncRetryQueueEntry>();
            retryEntriesDropdown.options = entries.Count == 0
                ? new System.Collections.Generic.List<Dropdown.OptionData> { new Dropdown.OptionData("No retry entries") }
                : entries.Select(item => new Dropdown.OptionData(item.OperationType + " | " + item.Status + " | attempts " + item.AttemptCount + "/" + item.MaxAttempts + " | code " + item.LastSafeErrorCode)).ToList();
            if (retryEntriesDropdown.value >= retryEntriesDropdown.options.Count)
            {
                retryEntriesDropdown.value = 0;
            }

            retryEntriesDropdown.RefreshShownValue();
        }

        private void FillTombstoneDropdown()
        {
            if (tombstonesDropdown == null)
            {
                return;
            }

            var tombstones = viewModel?.TombstoneSummary?.SafeTombstones ?? new System.Collections.Generic.List<TokenForge.Client.Sync.SafeSyncTombstone>();
            tombstonesDropdown.options = tombstones.Count == 0
                ? new System.Collections.Generic.List<Dropdown.OptionData> { new Dropdown.OptionData("No tombstones") }
                : tombstones.Select(item => new Dropdown.OptionData(BootstrapUiTextFormatter.SafeTombstoneLabel(item))).ToList();
            if (tombstonesDropdown.value >= tombstonesDropdown.options.Count)
            {
                tombstonesDropdown.value = 0;
            }

            tombstonesDropdown.RefreshShownValue();
        }

        private void FillConflictDropdown()
        {
            if (conflictsDropdown == null)
            {
                return;
            }

            var conflicts = viewModel?.ConflictSummary?.SafeConflicts ?? new System.Collections.Generic.List<TokenForge.Client.Sync.SafeSyncConflict>();
            conflictsDropdown.options = conflicts.Count == 0
                ? new System.Collections.Generic.List<Dropdown.OptionData> { new Dropdown.OptionData("No conflicts") }
                : conflicts.Select(item => new Dropdown.OptionData(BootstrapUiTextFormatter.SafeConflictLabel(item))).ToList();
            if (conflictsDropdown.value >= conflictsDropdown.options.Count)
            {
                conflictsDropdown.value = 0;
            }

            conflictsDropdown.RefreshShownValue();
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            var valid = true;
            valid &= Require(noticeLabel, nameof(noticeLabel), ref error);
            valid &= Require(statusLabel, nameof(statusLabel), ref error);
            valid &= Require(baseUrlInput, nameof(baseUrlInput), ref error);
            valid &= Require(healthButton, nameof(healthButton), ref error);
            valid &= Require(syncButton, nameof(syncButton), ref error);
            valid &= Require(fetchButton, nameof(fetchButton), ref error);
            valid &= Require(remoteSessionsDropdown, nameof(remoteSessionsDropdown), ref error);
            valid &= Require(deleteRemoteButton, nameof(deleteRemoteButton), ref error);
            valid &= Require(retryQueueLabel, nameof(retryQueueLabel), ref error);
            valid &= Require(conflictLabel, nameof(conflictLabel), ref error);
            valid &= Require(tombstoneLabel, nameof(tombstoneLabel), ref error);
            valid &= Require(retryEntriesDropdown, nameof(retryEntriesDropdown), ref error);
            valid &= Require(retryPendingButton, nameof(retryPendingButton), ref error);
            valid &= Require(cancelRetryButton, nameof(cancelRetryButton), ref error);
            valid &= Require(cancelAllFailedRetryButton, nameof(cancelAllFailedRetryButton), ref error);
            valid &= Require(clearSucceededButton, nameof(clearSucceededButton), ref error);
            valid &= Require(pauseAllPendingRetryButton, nameof(pauseAllPendingRetryButton), ref error);
            valid &= Require(resumeAllPausedRetryButton, nameof(resumeAllPausedRetryButton), ref error);
            valid &= Require(tombstonesDropdown, nameof(tombstonesDropdown), ref error);
            valid &= Require(enqueueTombstoneDeletesButton, nameof(enqueueTombstoneDeletesButton), ref error);
            valid &= Require(processTombstoneDeletesButton, nameof(processTombstoneDeletesButton), ref error);
            valid &= Require(cancelTombstoneButton, nameof(cancelTombstoneButton), ref error);
            valid &= Require(cancelAllFailedTombstonesButton, nameof(cancelAllFailedTombstonesButton), ref error);
            valid &= Require(clearResolvedTombstonesButton, nameof(clearResolvedTombstonesButton), ref error);
            valid &= Require(conflictsDropdown, nameof(conflictsDropdown), ref error);
            valid &= Require(keepLocalButton, nameof(keepLocalButton), ref error);
            valid &= Require(keepRemoteButton, nameof(keepRemoteButton), ref error);
            valid &= Require(markConflictResolvedButton, nameof(markConflictResolvedButton), ref error);
            valid &= Require(cancelConflictResolutionButton, nameof(cancelConflictResolutionButton), ref error);
            return valid;
        }
    }
}
