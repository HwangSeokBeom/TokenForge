using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class PrivacyNoticePanelView : UiBinderBase
    {
        [SerializeField] public Text bodyLabel;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            Render();
        }

        public void Render()
        {
            SetText(bodyLabel, "Safe Sync sends aggregate-only session summaries. " +
                ApprovedActivityAnalysisViewModel.ApprovedLocationsNotice + " " +
                "Raw local data is never synced. " +
                ApprovedActivityAnalysisViewModel.RawDataNotice);
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            return Require(bodyLabel, nameof(bodyLabel), ref error);
        }
    }
}
