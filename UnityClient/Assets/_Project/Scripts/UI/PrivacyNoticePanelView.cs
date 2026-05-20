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
            SetText(bodyLabel, "Only approved aggregate summaries can sync.\n\n" +
                               "Private local details stay on this device and are not rendered in the dashboard.");
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            return Require(bodyLabel, nameof(bodyLabel), ref error);
        }
    }
}
