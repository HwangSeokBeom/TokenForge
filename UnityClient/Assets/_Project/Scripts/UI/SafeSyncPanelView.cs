using System.Linq;
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

        private ApprovedActivityAnalysisViewModel viewModel;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            this.viewModel = viewModel;

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

            Render();
        }

        public void Render()
        {
            SetText(noticeLabel, "Only privacy-safe aggregate sessions are synced.\nApproved locations and raw local data are never synced.");
            SetText(statusLabel, BootstrapUiTextFormatter.SafeSyncStatus(viewModel));
            if (viewModel != null && baseUrlInput != null && !baseUrlInput.isFocused)
            {
                baseUrlInput.text = viewModel.SafeSyncBaseUrl;
            }

            FillRemoteDropdown();

            var hasService = viewModel != null && viewModel.HasSafeSyncService;
            var busy = viewModel != null && viewModel.IsSafeSyncRequestInProgress;
            var authenticated = viewModel != null && viewModel.CanUseAuthenticatedSafeSync;
            SetButton(healthButton, hasService && !busy);
            SetButton(syncButton, hasService && authenticated && !busy);
            SetButton(fetchButton, hasService && authenticated && !busy);
            SetButton(deleteRemoteButton, hasService && authenticated && !busy && viewModel.RemoteSafeSessions.Count > 0);
        }

        private string SelectedRemoteSessionId()
        {
            var index = remoteSessionsDropdown != null ? remoteSessionsDropdown.value : -1;
            return viewModel != null && index >= 0 && index < viewModel.RemoteSafeSessions.Count
                ? viewModel.RemoteSafeSessions[index].ServerSessionId
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
            return valid;
        }
    }
}
