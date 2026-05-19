using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class ReviewPanelView : UiBinderBase
    {
        [SerializeField] public Text noticeLabel;
        [SerializeField] public Text reviewLabel;
        [SerializeField] public Button saveGitButton;
        [SerializeField] public Button discardGitButton;
        [SerializeField] public Button saveAgentButton;
        [SerializeField] public Button discardAgentButton;

        private ApprovedActivityAnalysisViewModel viewModel;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            this.viewModel = viewModel;

            ReplaceClick(saveGitButton, () => RunViewModelAction(() => this.viewModel.SaveGitSessionAsync()));
            ReplaceClick(discardGitButton, () =>
            {
                this.viewModel.DiscardGitReview();
                RenderAll();
            });
            ReplaceClick(saveAgentButton, () => RunViewModelAction(() => this.viewModel.SaveAgentSessionAsync()));
            ReplaceClick(discardAgentButton, () =>
            {
                this.viewModel.DiscardAgentReview();
                RenderAll();
            });

            Render();
        }

        public void Render()
        {
            SetText(noticeLabel, "Only safe aggregate data can be saved. Raw paths and source content stay local.");
            SetText(reviewLabel, BootstrapUiTextFormatter.ReviewSummary(viewModel));
            var gitReady = viewModel != null && viewModel.GitFlow.State == GitAnalysisFlowState.ReviewReady;
            var agentReady = viewModel != null && viewModel.AgentFlow.State == AgentAnalysisFlowState.ReviewReady;
            SetButton(saveGitButton, gitReady);
            SetButton(discardGitButton, gitReady);
            SetButton(saveAgentButton, agentReady && viewModel.AgentFlow.Review != null && viewModel.AgentFlow.Review.SaveEligible);
            SetButton(discardAgentButton, agentReady);
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            var valid = true;
            valid &= Require(noticeLabel, nameof(noticeLabel), ref error);
            valid &= Require(reviewLabel, nameof(reviewLabel), ref error);
            valid &= Require(saveGitButton, nameof(saveGitButton), ref error);
            valid &= Require(discardGitButton, nameof(discardGitButton), ref error);
            valid &= Require(saveAgentButton, nameof(saveAgentButton), ref error);
            valid &= Require(discardAgentButton, nameof(discardAgentButton), ref error);
            return valid;
        }
    }
}
