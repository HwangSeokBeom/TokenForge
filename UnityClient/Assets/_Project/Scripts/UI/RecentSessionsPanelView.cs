using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class RecentSessionsPanelView : UiBinderBase
    {
        [SerializeField] public Text localRecentSessionsLabel;
        [SerializeField] public Text remoteSessionsLabel;

        private ApprovedActivityAnalysisViewModel viewModel;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            this.viewModel = viewModel;
            Render();
        }

        public void Render()
        {
            SetText(localRecentSessionsLabel, BootstrapUiTextFormatter.RecentSessions(viewModel));
            SetText(remoteSessionsLabel, BootstrapUiTextFormatter.RemoteSessions(viewModel));
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            var valid = true;
            valid &= Require(localRecentSessionsLabel, nameof(localRecentSessionsLabel), ref error);
            valid &= Require(remoteSessionsLabel, nameof(remoteSessionsLabel), ref error);
            return valid;
        }
    }
}
