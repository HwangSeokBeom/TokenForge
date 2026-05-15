using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class ApprovedLocationsPanelView : UiBinderBase
    {
        [SerializeField] public Text statusLabel;
        [SerializeField] public InputField gitAliasInput;
        [SerializeField] public Dropdown gitLocationsDropdown;
        [SerializeField] public Button approveGitButton;
        [SerializeField] public Button useGitButton;
        [SerializeField] public Button disableGitButton;
        [SerializeField] public Button removeGitButton;
        [SerializeField] public InputField agentAliasInput;
        [SerializeField] public Dropdown agentLocationsDropdown;
        [SerializeField] public Button approveAgentButton;
        [SerializeField] public Button useAgentButton;
        [SerializeField] public Button disableAgentButton;
        [SerializeField] public Button removeAgentButton;

        private ApprovedActivityAnalysisViewModel viewModel;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            this.viewModel = viewModel;

            ReplaceClick(approveGitButton, () => RunViewModelAction(() => this.viewModel.AddCurrentGitSelectionToApprovedLocationsAsync(gitAliasInput != null ? gitAliasInput.text : string.Empty)));
            ReplaceClick(useGitButton, () => RunViewModelAction(() => this.viewModel.SelectApprovedGitLocationAsync(SelectedGitLocalId())));
            ReplaceClick(disableGitButton, () => RunViewModelAction(() => this.viewModel.DisableApprovedLocationAsync(SelectedGitLocalId())));
            ReplaceClick(removeGitButton, () => RunViewModelAction(() => this.viewModel.RemoveApprovedLocationAsync(SelectedGitLocalId())));
            ReplaceClick(approveAgentButton, () => RunViewModelAction(() => this.viewModel.AddCurrentAgentSelectionToApprovedLocationsAsync(agentAliasInput != null ? agentAliasInput.text : string.Empty)));
            ReplaceClick(useAgentButton, () => RunViewModelAction(() => this.viewModel.SelectApprovedAgentLocationAsync(SelectedAgentLocalId())));
            ReplaceClick(disableAgentButton, () => RunViewModelAction(() => this.viewModel.DisableApprovedLocationAsync(SelectedAgentLocalId())));
            ReplaceClick(removeAgentButton, () => RunViewModelAction(() => this.viewModel.RemoveApprovedLocationAsync(SelectedAgentLocalId())));

            Render();
        }

        public void Render()
        {
            var gitCount = viewModel?.ApprovedGitLocations.Count ?? 0;
            var agentCount = viewModel?.ApprovedAgentLocations.Count ?? 0;
            var emptyState = gitCount == 0 && agentCount == 0
                ? " No approved locations yet. Select a local source, then approve it for this device."
                : " Git approved: " + gitCount + " | Agent approved: " + agentCount + ".";
            SetText(statusLabel, ApprovedActivityAnalysisViewModel.ApprovedLocationsNotice + emptyState);
            FillDropdown(gitLocationsDropdown, viewModel?.ApprovedGitLocations);
            FillDropdown(agentLocationsDropdown, viewModel?.ApprovedAgentLocations);
            SetButton(approveGitButton, viewModel != null && viewModel.GitFlow.HasSelectedRepositoryForLocalOnlyApproval);
            SetButton(useGitButton, viewModel != null && viewModel.ApprovedGitLocations.Any(item => item.Enabled));
            SetButton(disableGitButton, viewModel != null && viewModel.ApprovedGitLocations.Count > 0);
            SetButton(removeGitButton, viewModel != null && viewModel.ApprovedGitLocations.Count > 0);
            SetButton(approveAgentButton, viewModel != null && viewModel.AgentFlow.HasSelectedAgentLogLocationForLocalOnlyApproval);
            SetButton(useAgentButton, viewModel != null && viewModel.ApprovedAgentLocations.Any(item => item.Enabled));
            SetButton(disableAgentButton, viewModel != null && viewModel.ApprovedAgentLocations.Count > 0);
            SetButton(removeAgentButton, viewModel != null && viewModel.ApprovedAgentLocations.Count > 0);
        }

        private string SelectedGitLocalId()
        {
            var index = gitLocationsDropdown != null ? gitLocationsDropdown.value : -1;
            return viewModel != null && index >= 0 && index < viewModel.ApprovedGitLocations.Count
                ? viewModel.ApprovedGitLocations[index].LocalId
                : string.Empty;
        }

        private string SelectedAgentLocalId()
        {
            var index = agentLocationsDropdown != null ? agentLocationsDropdown.value : -1;
            return viewModel != null && index >= 0 && index < viewModel.ApprovedAgentLocations.Count
                ? viewModel.ApprovedAgentLocations[index].LocalId
                : string.Empty;
        }

        private static void FillDropdown(Dropdown dropdown, System.Collections.Generic.List<ApprovedLocationDisplayItem> locations)
        {
            if (dropdown == null)
            {
                return;
            }

            dropdown.options = locations == null || locations.Count == 0
                ? new System.Collections.Generic.List<Dropdown.OptionData> { new Dropdown.OptionData("No approved locations") }
                : locations.Select(item => new Dropdown.OptionData(BootstrapUiTextFormatter.ApprovedLocationLabel(item))).ToList();
            if (dropdown.value >= dropdown.options.Count)
            {
                dropdown.value = 0;
            }

            dropdown.RefreshShownValue();
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            var valid = true;
            valid &= Require(statusLabel, nameof(statusLabel), ref error);
            valid &= Require(gitAliasInput, nameof(gitAliasInput), ref error);
            valid &= Require(gitLocationsDropdown, nameof(gitLocationsDropdown), ref error);
            valid &= Require(approveGitButton, nameof(approveGitButton), ref error);
            valid &= Require(useGitButton, nameof(useGitButton), ref error);
            valid &= Require(disableGitButton, nameof(disableGitButton), ref error);
            valid &= Require(removeGitButton, nameof(removeGitButton), ref error);
            valid &= Require(agentAliasInput, nameof(agentAliasInput), ref error);
            valid &= Require(agentLocationsDropdown, nameof(agentLocationsDropdown), ref error);
            valid &= Require(approveAgentButton, nameof(approveAgentButton), ref error);
            valid &= Require(useAgentButton, nameof(useAgentButton), ref error);
            valid &= Require(disableAgentButton, nameof(disableAgentButton), ref error);
            valid &= Require(removeAgentButton, nameof(removeAgentButton), ref error);
            return valid;
        }
    }
}
