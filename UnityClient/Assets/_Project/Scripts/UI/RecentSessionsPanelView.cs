using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class RecentSessionsPanelView : UiBinderBase
    {
        [SerializeField] public Text localRecentSessionsLabel;
        [SerializeField] public Text localDeleteStatusLabel;
        [SerializeField] public Dropdown localSessionsDropdown;
        [SerializeField] public Button deleteLocalSessionButton;
        [SerializeField] public Text remoteSessionsLabel;

        private ApprovedActivityAnalysisViewModel viewModel;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            this.viewModel = viewModel;
            ReplaceClick(deleteLocalSessionButton, () => RunViewModelAction(() => this.viewModel.DeleteLocalSavedSessionAsync(SelectedLocalSessionId())));
            Render();
        }

        public void Render()
        {
            SetText(localRecentSessionsLabel, BootstrapUiTextFormatter.RecentSessions(viewModel));
            SetText(localDeleteStatusLabel, "Deleting a synced local session creates a local tombstone so remote delete can be synced explicitly.");
            SetText(remoteSessionsLabel, BootstrapUiTextFormatter.RemoteSessions(viewModel));
            FillLocalDropdown();
            var hasLocal = viewModel != null && viewModel.RecentSessions.Count > 0;
            SetButton(deleteLocalSessionButton, viewModel != null && viewModel.HasSafeSyncService && !viewModel.IsSafeSyncRequestInProgress && hasLocal);
        }

        private string SelectedLocalSessionId()
        {
            var index = localSessionsDropdown != null ? localSessionsDropdown.value : -1;
            return viewModel != null && index >= 0 && index < viewModel.RecentSessions.Count
                ? viewModel.RecentSessions[index].ClientSessionId
                : string.Empty;
        }

        private void FillLocalDropdown()
        {
            if (localSessionsDropdown == null)
            {
                return;
            }

            localSessionsDropdown.options = viewModel == null || viewModel.RecentSessions.Count == 0
                ? new System.Collections.Generic.List<Dropdown.OptionData> { new Dropdown.OptionData("No local sessions") }
                : viewModel.RecentSessions.Select(item => new Dropdown.OptionData(BootstrapUiTextFormatter.SafeLocalSessionLabel(item))).ToList();
            if (localSessionsDropdown.value >= localSessionsDropdown.options.Count)
            {
                localSessionsDropdown.value = 0;
            }

            localSessionsDropdown.RefreshShownValue();
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            var valid = true;
            valid &= Require(localRecentSessionsLabel, nameof(localRecentSessionsLabel), ref error);
            valid &= Require(localDeleteStatusLabel, nameof(localDeleteStatusLabel), ref error);
            valid &= Require(localSessionsDropdown, nameof(localSessionsDropdown), ref error);
            valid &= Require(deleteLocalSessionButton, nameof(deleteLocalSessionButton), ref error);
            valid &= Require(remoteSessionsLabel, nameof(remoteSessionsLabel), ref error);
            return valid;
        }
    }
}
