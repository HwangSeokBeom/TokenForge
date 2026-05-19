using TokenForge.Client.Agents;
using TokenForge.Client.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class ActivityAnalysisPanelView : UiBinderBase
    {
        [SerializeField] public Text gitStatusLabel;
        [SerializeField] public InputField gitWindowDaysInput;
        [SerializeField] public InputField gitMaxCommitsInput;
        [SerializeField] public Toggle gitIncludeUncommittedToggle;
        [SerializeField] public Toggle gitIncludeCommitsToggle;
        [SerializeField] public Button selectGitButton;
        [SerializeField] public Button analyzeGitButton;
        [SerializeField] public Text agentStatusLabel;
        [SerializeField] public Dropdown agentProviderDropdown;
        [SerializeField] public InputField agentWindowDaysInput;
        [SerializeField] public InputField agentMaxFilesInput;
        [SerializeField] public InputField agentMaxEntriesInput;
        [SerializeField] public Button selectAgentButton;
        [SerializeField] public Button analyzeAgentButton;

        private ApprovedActivityAnalysisViewModel viewModel;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            this.viewModel = viewModel;
            ConfigureProviderDropdown();

            ReplaceClick(selectGitButton, () => RunViewModelAction(() => this.viewModel.SelectLocalGitRepositoryForOnboardingAsync()));
            ReplaceClick(analyzeGitButton, () =>
            {
                ApplyGitSettings();
                RunViewModelAction(() => this.viewModel.AnalyzeGitActivityAsync());
            });
            ReplaceClick(selectAgentButton, () =>
            {
                ApplyAgentSettings();
                RunViewModelAction(() => this.viewModel.SelectAgentLogLocationAsync());
            });
            ReplaceClick(analyzeAgentButton, () =>
            {
                ApplyAgentSettings();
                RunViewModelAction(() => this.viewModel.AnalyzeSelectedAgentActivityAsync());
            });

            Render();
        }

        public void Render()
        {
            SetText(gitStatusLabel, BootstrapUiTextFormatter.FriendlyGitStatus(viewModel));
            SetText(agentStatusLabel, BootstrapUiTextFormatter.FriendlyAgentStatus(viewModel));

            if (viewModel != null)
            {
                SetInputIfIdle(gitWindowDaysInput, viewModel.GitFlow.Settings.AnalysisWindowDays.ToString());
                SetInputIfIdle(gitMaxCommitsInput, viewModel.GitFlow.Settings.MaxCommitsToInspect.ToString());
                if (gitIncludeUncommittedToggle != null)
                {
                    gitIncludeUncommittedToggle.isOn = viewModel.GitFlow.Settings.IncludeUncommittedChanges;
                }

                if (gitIncludeCommitsToggle != null)
                {
                    gitIncludeCommitsToggle.isOn = viewModel.GitFlow.Settings.IncludeRecentCommits;
                }

                SetInputIfIdle(agentWindowDaysInput, viewModel.AgentSettings.AnalysisWindowDays.ToString());
                SetInputIfIdle(agentMaxFilesInput, viewModel.AgentSettings.MaxFilesToScan.ToString());
                SetInputIfIdle(agentMaxEntriesInput, viewModel.AgentSettings.MaxLogEntriesToScan.ToString());
                if (agentProviderDropdown != null)
                {
                    agentProviderDropdown.value = ProviderToDropdownIndex(viewModel.SelectedAgentProviderType);
                    agentProviderDropdown.RefreshShownValue();
                }
            }

            var gitBusy = viewModel != null && viewModel.GitFlow.State == GitAnalysisFlowState.Analyzing;
            var agentBusy = viewModel != null && viewModel.AgentFlow.State == AgentAnalysisFlowState.Analyzing;
            SetButton(selectGitButton, viewModel != null && !gitBusy);
            SetButton(analyzeGitButton, viewModel != null && !gitBusy);
            SetButton(selectAgentButton, viewModel != null && !agentBusy);
            SetButton(analyzeAgentButton, viewModel != null && !agentBusy);
        }

        private void ApplyGitSettings()
        {
            if (viewModel == null)
            {
                return;
            }

            if (gitWindowDaysInput != null && int.TryParse(gitWindowDaysInput.text, out var days))
            {
                viewModel.GitFlow.Settings.AnalysisWindowDays = days;
            }

            if (gitMaxCommitsInput != null && int.TryParse(gitMaxCommitsInput.text, out var commits))
            {
                viewModel.GitFlow.Settings.MaxCommitsToInspect = commits;
            }

            if (gitIncludeUncommittedToggle != null)
            {
                viewModel.GitFlow.Settings.IncludeUncommittedChanges = gitIncludeUncommittedToggle.isOn;
            }

            if (gitIncludeCommitsToggle != null)
            {
                viewModel.GitFlow.Settings.IncludeRecentCommits = gitIncludeCommitsToggle.isOn;
            }
        }

        private void ApplyAgentSettings()
        {
            if (viewModel == null)
            {
                return;
            }

            viewModel.SelectedAgentProviderType = DropdownIndexToProvider(agentProviderDropdown != null ? agentProviderDropdown.value : 0);
            if (agentWindowDaysInput != null && int.TryParse(agentWindowDaysInput.text, out var days))
            {
                viewModel.AgentSettings.AnalysisWindowDays = days;
            }

            if (agentMaxFilesInput != null && int.TryParse(agentMaxFilesInput.text, out var files))
            {
                viewModel.AgentSettings.MaxFilesToScan = files;
            }

            if (agentMaxEntriesInput != null && int.TryParse(agentMaxEntriesInput.text, out var entries))
            {
                viewModel.AgentSettings.MaxLogEntriesToScan = entries;
            }
        }

        private void ConfigureProviderDropdown()
        {
            if (agentProviderDropdown == null)
            {
                return;
            }

            agentProviderDropdown.options = new System.Collections.Generic.List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("Cursor"),
                new Dropdown.OptionData("Claude Code"),
                new Dropdown.OptionData("Codex"),
                new Dropdown.OptionData("GitHub Copilot"),
                new Dropdown.OptionData("Other / Manual Log Folder")
            };
            agentProviderDropdown.value = viewModel != null ? ProviderToDropdownIndex(viewModel.SelectedAgentProviderType) : 0;
            agentProviderDropdown.onValueChanged.RemoveAllListeners();
            agentProviderDropdown.onValueChanged.AddListener(value =>
            {
                if (this.viewModel != null)
                {
                    this.viewModel.SelectedAgentProviderType = DropdownIndexToProvider(value);
                }
            });
            agentProviderDropdown.RefreshShownValue();
        }

        private static int ProviderToDropdownIndex(AgentProviderType providerType)
        {
            switch (providerType)
            {
                case AgentProviderType.Cursor: return 0;
                case AgentProviderType.Claude:
                case AgentProviderType.ClaudeCode: return 1;
                case AgentProviderType.Codex: return 2;
                case AgentProviderType.GitHubCopilot: return 3;
                case AgentProviderType.Manual: return 4;
                default: return 0;
            }
        }

        private static AgentProviderType DropdownIndexToProvider(int value)
        {
            switch (value)
            {
                case 0: return AgentProviderType.Cursor;
                case 1: return AgentProviderType.ClaudeCode;
                case 2: return AgentProviderType.Codex;
                case 3: return AgentProviderType.GitHubCopilot;
                case 4: return AgentProviderType.Manual;
                default: return AgentProviderType.Cursor;
            }
        }

        private static void SetInputIfIdle(InputField input, string value)
        {
            if (input != null && !input.isFocused)
            {
                input.text = value;
            }
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            var valid = true;
            valid &= Require(gitStatusLabel, nameof(gitStatusLabel), ref error);
            valid &= Require(gitWindowDaysInput, nameof(gitWindowDaysInput), ref error);
            valid &= Require(gitMaxCommitsInput, nameof(gitMaxCommitsInput), ref error);
            valid &= Require(gitIncludeUncommittedToggle, nameof(gitIncludeUncommittedToggle), ref error);
            valid &= Require(gitIncludeCommitsToggle, nameof(gitIncludeCommitsToggle), ref error);
            valid &= Require(selectGitButton, nameof(selectGitButton), ref error);
            valid &= Require(analyzeGitButton, nameof(analyzeGitButton), ref error);
            valid &= Require(agentStatusLabel, nameof(agentStatusLabel), ref error);
            valid &= Require(agentProviderDropdown, nameof(agentProviderDropdown), ref error);
            valid &= Require(agentWindowDaysInput, nameof(agentWindowDaysInput), ref error);
            valid &= Require(agentMaxFilesInput, nameof(agentMaxFilesInput), ref error);
            valid &= Require(agentMaxEntriesInput, nameof(agentMaxEntriesInput), ref error);
            valid &= Require(selectAgentButton, nameof(selectAgentButton), ref error);
            valid &= Require(analyzeAgentButton, nameof(analyzeAgentButton), ref error);
            return valid;
        }
    }
}
