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
            SetText(bodyLabel, "Only saved aggregate summaries can sync. Raw paths and source data stay local.\n\n" +
                "TokenForge does not render prompts, responses, commands, filenames, repo names, branch names, source snippets, tokens, or payload bodies.");
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            return Require(bodyLabel, nameof(bodyLabel), ref error);
        }
    }
}
