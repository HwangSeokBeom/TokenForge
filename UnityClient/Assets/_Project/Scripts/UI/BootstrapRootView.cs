using System;
using TokenForge.Client.Auth;
using TokenForge.Client.Domain;
using TokenForge.Client.Sync;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class BootstrapRootView : MonoBehaviour
    {
        [SerializeField] public Text headerStatusLabel;
        [SerializeField] public Text validationStatusLabel;
        [SerializeField] public ScrollRect rootScrollRect;
        [SerializeField] public GameObject startScreenRoot;
        [SerializeField] public GameObject gameDashboardRoot;
        [SerializeField] public GameObject runAnalysisRoot;
        [SerializeField] public GameObject settingsAdvancedRoot;
        [SerializeField] public GameObject developerDiagnosticsRoot;
        [SerializeField] public GameObject conflictBanner;
        [SerializeField] public Text loginStatusSmallLabel;
        [SerializeField] public Text onboardingStepIndicatorLabel;
        [SerializeField] public Text onboardingPrimaryTitleLabel;
        [SerializeField] public Text onboardingAccountStatusLabel;
        [SerializeField] public Text onboardingAiAgentsStatusLabel;
        [SerializeField] public Text onboardingGitStatusLabel;
        [SerializeField] public Text onboardingReadySummaryLabel;
        [SerializeField] public GameObject onboardingAccountStepRoot;
        [SerializeField] public GameObject onboardingAiAgentsStepRoot;
        [SerializeField] public GameObject onboardingGitStepRoot;
        [SerializeField] public GameObject onboardingReadyStepRoot;
        [SerializeField] public InputField onboardingEmailInput;
        [SerializeField] public InputField onboardingDisplayNameInput;
        [SerializeField] public InputField onboardingPasswordInput;
        [SerializeField] public Button onboardingCreateAccountButton;
        [SerializeField] public Button onboardingLoginButton;
        [SerializeField] public Button onboardingContinueOfflineButton;
        [SerializeField] public Button onboardingBackButton;
        [SerializeField] public Button onboardingContinueButton;
        [SerializeField] public Toggle cursorAgentToggle;
        [SerializeField] public Toggle claudeAgentToggle;
        [SerializeField] public Toggle codexAgentToggle;
        [SerializeField] public Toggle copilotAgentToggle;
        [SerializeField] public Toggle otherAgentToggle;
        [SerializeField] public Text cursorAgentStatusLabel;
        [SerializeField] public Text claudeAgentStatusLabel;
        [SerializeField] public Text codexAgentStatusLabel;
        [SerializeField] public Text copilotAgentStatusLabel;
        [SerializeField] public Text otherAgentStatusLabel;
        [SerializeField] public Button cursorAgentConnectButton;
        [SerializeField] public Button claudeAgentConnectButton;
        [SerializeField] public Button codexAgentConnectButton;
        [SerializeField] public Button copilotAgentConnectButton;
        [SerializeField] public Button otherAgentSelectLogFolderButton;
        [SerializeField] public Button onboardingSelectLocalRepositoryButton;
        [SerializeField] public Button onboardingConnectGitAccountButton;
        [SerializeField] public Button onboardingSkipGitButton;
        [SerializeField] public Button onboardingClearLocalRepositoryButton;
        [SerializeField] public Text runAnalysisTitleLabel;
        [SerializeField] public Button runAnalysisBackButton;
        [SerializeField] public Button runAnalysisSettingsButton;
        [SerializeField] public Text settingsSummaryLabel;
        [SerializeField] public Text settingsSyncConflictLabel;
        [SerializeField] public Button settingsDeveloperDiagnosticsButton;
        [SerializeField] public Button diagnosticsBackButton;
        [SerializeField] public Text startCharacterLabel;
        [SerializeField] public Text startStatsLabel;
        [SerializeField] public Text startGrowthLabel;
        [SerializeField] public Text startRecentSessionsLabel;
        [SerializeField] public Text startPrivacySummaryLabel;
        [SerializeField] public Text startRunStatusLabel;
        [SerializeField] public Button startGameButton;
        [SerializeField] public Button continueButton;
        [SerializeField] public Button startAnalyzeRepositoryButton;
        [SerializeField] public Button startAnalyzeAgentLogsButton;
        [SerializeField] public Button startReviewAnalysisButton;
        [SerializeField] public Button startSaveRunButton;
        [SerializeField] public Button startCreateSyncAccountButton;
        [SerializeField] public Button startLoginButton;
        [SerializeField] public Button startCheckServerButton;
        [SerializeField] public Button startSyncProgressButton;
        [SerializeField] public Text dashboardCharacterStatusLabel;
        [SerializeField] public CompanionView companionView;
        [SerializeField] public CompanionStatusPanelView companionStatusPanel;
        [SerializeField] public Text dashboardQuestLabel;
        [SerializeField] public Text dashboardActivityLogLabel;
        [SerializeField] public Text dashboardSyncReasonLabel;
        [SerializeField] public Text dashboardDesktopCompanionLabel;
        [SerializeField] public Button dashboardEnableDesktopCompanionButton;
        [SerializeField] public Button dashboardDisableDesktopCompanionButton;
        [SerializeField] public Text conflictBannerLabel;
        [SerializeField] public Button dashboardAnalyzeRepositoryButton;
        [SerializeField] public Button dashboardSaveSessionButton;
        [SerializeField] public Button dashboardSyncButton;
        [SerializeField] public Button dashboardHistoryButton;
        [SerializeField] public Button dashboardSettingsButton;
        [SerializeField] public Button dashboardBackButton;
        [SerializeField] public Button settingsBackButton;
        [SerializeField] public Text settingsDesktopCompanionStatusLabel;
        [SerializeField] public Toggle settingsDesktopCompanionEnabledToggle;
        [SerializeField] public Toggle settingsDesktopCompanionClickThroughToggle;
        [SerializeField] public Dropdown settingsDesktopCompanionMotionModeDropdown;
        [SerializeField] public Button settingsResetOverlayPositionButton;
        [SerializeField] public AccountPanelView accountPanel;
        [SerializeField] public ActivityAnalysisPanelView activityAnalysisPanel;
        [SerializeField] public ApprovedLocationsPanelView approvedLocationsPanel;
        [SerializeField] public ReviewPanelView reviewPanel;
        [SerializeField] public SafeSyncPanelView safeSyncPanel;
        [SerializeField] public RecentSessionsPanelView recentSessionsPanel;
        [SerializeField] public PrivacyNoticePanelView privacyNoticePanel;
        [SerializeField] public DesktopCompanionOverlayController desktopCompanionOverlayController;

        private LocalClientStatus status;
        private ApprovedActivityAnalysisViewModel viewModel;
        private ScreenMode currentScreen = ScreenMode.Start;
        private const string PrivacySummaryText = "Raw paths, prompts, logs, source code, diffs, and file names stay local.";

        private enum ScreenMode
        {
            Start,
            Dashboard,
            RunAnalysis,
            Settings,
            DeveloperDiagnostics
        }

        public void Bind(LocalClientStatus status, ApprovedActivityAnalysisViewModel viewModel)
        {
            this.status = status;
            this.viewModel = viewModel;
            EnsureDesktopCompanionRuntimeControls();
            if (!ValidateReferences(out var error))
            {
                if (validationStatusLabel != null)
                {
                    validationStatusLabel.text = "UI prefab reference error: " + error;
                }

                return;
            }

            accountPanel.Bind(viewModel, Render);
            activityAnalysisPanel.Bind(viewModel, Render);
            approvedLocationsPanel.Bind(viewModel, Render);
            reviewPanel.Bind(viewModel, Render);
            safeSyncPanel.Bind(viewModel, Render);
            recentSessionsPanel.Bind(viewModel, Render);
            privacyNoticePanel.Bind(viewModel, Render);
            companionView.Bind(viewModel, Render);
            companionStatusPanel.Bind(viewModel, Render);
            if (desktopCompanionOverlayController == null)
            {
                desktopCompanionOverlayController = gameObject.AddComponent<DesktopCompanionOverlayController>();
            }

            desktopCompanionOverlayController.Initialize();
            WireNavigation();
            Render();
        }

        public void Render()
        {
            if (headerStatusLabel != null)
            {
                headerStatusLabel.text = status == null
                    ? "TokenForge client UI"
                    : "Local client initialized | " + status.InitializedAt.ToString("yyyy-MM-dd HH:mm:ss zzz");
            }

            if (validationStatusLabel != null)
            {
                validationStatusLabel.text = ValidateReferences(out var error)
                    ? "Privacy status: aggregate-only UI ready."
                    : "UI prefab reference error: " + error;
            }

            accountPanel?.Render();
            activityAnalysisPanel?.Render();
            approvedLocationsPanel?.Render();
            reviewPanel?.Render();
            safeSyncPanel?.Render();
            recentSessionsPanel?.Render();
            privacyNoticePanel?.Render();
            companionView?.Render(currentScreen != ScreenMode.Dashboard);
            companionStatusPanel?.Render();
            desktopCompanionOverlayController?.ApplySettings(
                viewModel?.CharacterDashboard?.DesktopCompanionSettings ?? DesktopCompanionSettings.CreateDefault(),
                viewModel?.CharacterDashboard?.CompanionState ?? CompanionState.CreateDefault());
            RenderGameShell();
        }

        public bool ValidateReferences(out string error)
        {
            error = string.Empty;
            var valid = true;
            valid &= Require(headerStatusLabel, nameof(headerStatusLabel), ref error);
            valid &= Require(validationStatusLabel, nameof(validationStatusLabel), ref error);
            valid &= Require(rootScrollRect, nameof(rootScrollRect), ref error);
            valid &= Require(startScreenRoot, nameof(startScreenRoot), ref error);
            valid &= Require(gameDashboardRoot, nameof(gameDashboardRoot), ref error);
            valid &= Require(runAnalysisRoot, nameof(runAnalysisRoot), ref error);
            valid &= Require(settingsAdvancedRoot, nameof(settingsAdvancedRoot), ref error);
            valid &= Require(developerDiagnosticsRoot, nameof(developerDiagnosticsRoot), ref error);
            valid &= Require(loginStatusSmallLabel, nameof(loginStatusSmallLabel), ref error);
            valid &= Require(onboardingStepIndicatorLabel, nameof(onboardingStepIndicatorLabel), ref error);
            valid &= Require(onboardingPrimaryTitleLabel, nameof(onboardingPrimaryTitleLabel), ref error);
            valid &= Require(onboardingAccountStatusLabel, nameof(onboardingAccountStatusLabel), ref error);
            valid &= Require(onboardingAiAgentsStatusLabel, nameof(onboardingAiAgentsStatusLabel), ref error);
            valid &= Require(onboardingGitStatusLabel, nameof(onboardingGitStatusLabel), ref error);
            valid &= Require(onboardingReadySummaryLabel, nameof(onboardingReadySummaryLabel), ref error);
            valid &= Require(onboardingAccountStepRoot, nameof(onboardingAccountStepRoot), ref error);
            valid &= Require(onboardingAiAgentsStepRoot, nameof(onboardingAiAgentsStepRoot), ref error);
            valid &= Require(onboardingGitStepRoot, nameof(onboardingGitStepRoot), ref error);
            valid &= Require(onboardingReadyStepRoot, nameof(onboardingReadyStepRoot), ref error);
            valid &= Require(onboardingEmailInput, nameof(onboardingEmailInput), ref error);
            valid &= Require(onboardingDisplayNameInput, nameof(onboardingDisplayNameInput), ref error);
            valid &= Require(onboardingPasswordInput, nameof(onboardingPasswordInput), ref error);
            valid &= Require(onboardingCreateAccountButton, nameof(onboardingCreateAccountButton), ref error);
            valid &= Require(onboardingLoginButton, nameof(onboardingLoginButton), ref error);
            valid &= Require(onboardingContinueOfflineButton, nameof(onboardingContinueOfflineButton), ref error);
            valid &= Require(onboardingBackButton, nameof(onboardingBackButton), ref error);
            valid &= Require(onboardingContinueButton, nameof(onboardingContinueButton), ref error);
            valid &= Require(cursorAgentToggle, nameof(cursorAgentToggle), ref error);
            valid &= Require(claudeAgentToggle, nameof(claudeAgentToggle), ref error);
            valid &= Require(codexAgentToggle, nameof(codexAgentToggle), ref error);
            valid &= Require(copilotAgentToggle, nameof(copilotAgentToggle), ref error);
            valid &= Require(otherAgentToggle, nameof(otherAgentToggle), ref error);
            valid &= Require(cursorAgentStatusLabel, nameof(cursorAgentStatusLabel), ref error);
            valid &= Require(claudeAgentStatusLabel, nameof(claudeAgentStatusLabel), ref error);
            valid &= Require(codexAgentStatusLabel, nameof(codexAgentStatusLabel), ref error);
            valid &= Require(copilotAgentStatusLabel, nameof(copilotAgentStatusLabel), ref error);
            valid &= Require(otherAgentStatusLabel, nameof(otherAgentStatusLabel), ref error);
            valid &= Require(cursorAgentConnectButton, nameof(cursorAgentConnectButton), ref error);
            valid &= Require(claudeAgentConnectButton, nameof(claudeAgentConnectButton), ref error);
            valid &= Require(codexAgentConnectButton, nameof(codexAgentConnectButton), ref error);
            valid &= Require(copilotAgentConnectButton, nameof(copilotAgentConnectButton), ref error);
            valid &= Require(otherAgentSelectLogFolderButton, nameof(otherAgentSelectLogFolderButton), ref error);
            valid &= Require(onboardingSelectLocalRepositoryButton, nameof(onboardingSelectLocalRepositoryButton), ref error);
            valid &= Require(onboardingConnectGitAccountButton, nameof(onboardingConnectGitAccountButton), ref error);
            valid &= Require(onboardingSkipGitButton, nameof(onboardingSkipGitButton), ref error);
            valid &= Require(onboardingClearLocalRepositoryButton, nameof(onboardingClearLocalRepositoryButton), ref error);
            valid &= Require(runAnalysisTitleLabel, nameof(runAnalysisTitleLabel), ref error);
            valid &= Require(runAnalysisBackButton, nameof(runAnalysisBackButton), ref error);
            valid &= Require(runAnalysisSettingsButton, nameof(runAnalysisSettingsButton), ref error);
            valid &= Require(settingsSummaryLabel, nameof(settingsSummaryLabel), ref error);
            valid &= Require(settingsSyncConflictLabel, nameof(settingsSyncConflictLabel), ref error);
            valid &= Require(settingsDeveloperDiagnosticsButton, nameof(settingsDeveloperDiagnosticsButton), ref error);
            valid &= Require(diagnosticsBackButton, nameof(diagnosticsBackButton), ref error);
            valid &= Require(startCharacterLabel, nameof(startCharacterLabel), ref error);
            valid &= Require(startStatsLabel, nameof(startStatsLabel), ref error);
            valid &= Require(startGrowthLabel, nameof(startGrowthLabel), ref error);
            valid &= Require(startRecentSessionsLabel, nameof(startRecentSessionsLabel), ref error);
            valid &= Require(startPrivacySummaryLabel, nameof(startPrivacySummaryLabel), ref error);
            valid &= Require(startRunStatusLabel, nameof(startRunStatusLabel), ref error);
            valid &= Require(startGameButton, nameof(startGameButton), ref error);
            valid &= Require(continueButton, nameof(continueButton), ref error);
            valid &= Require(startAnalyzeRepositoryButton, nameof(startAnalyzeRepositoryButton), ref error);
            valid &= Require(startAnalyzeAgentLogsButton, nameof(startAnalyzeAgentLogsButton), ref error);
            valid &= Require(startReviewAnalysisButton, nameof(startReviewAnalysisButton), ref error);
            valid &= Require(startSaveRunButton, nameof(startSaveRunButton), ref error);
            valid &= Require(startCreateSyncAccountButton, nameof(startCreateSyncAccountButton), ref error);
            valid &= Require(startLoginButton, nameof(startLoginButton), ref error);
            valid &= Require(startCheckServerButton, nameof(startCheckServerButton), ref error);
            valid &= Require(startSyncProgressButton, nameof(startSyncProgressButton), ref error);
            valid &= Require(dashboardCharacterStatusLabel, nameof(dashboardCharacterStatusLabel), ref error);
            valid &= Require(companionView, nameof(companionView), ref error);
            valid &= Require(companionStatusPanel, nameof(companionStatusPanel), ref error);
            valid &= Require(dashboardQuestLabel, nameof(dashboardQuestLabel), ref error);
            valid &= Require(dashboardActivityLogLabel, nameof(dashboardActivityLogLabel), ref error);
            valid &= Require(dashboardSyncReasonLabel, nameof(dashboardSyncReasonLabel), ref error);
            valid &= Require(dashboardDesktopCompanionLabel, nameof(dashboardDesktopCompanionLabel), ref error);
            valid &= Require(dashboardEnableDesktopCompanionButton, nameof(dashboardEnableDesktopCompanionButton), ref error);
            valid &= Require(dashboardDisableDesktopCompanionButton, nameof(dashboardDisableDesktopCompanionButton), ref error);
            valid &= Require(conflictBanner, nameof(conflictBanner), ref error);
            valid &= Require(conflictBannerLabel, nameof(conflictBannerLabel), ref error);
            valid &= Require(dashboardAnalyzeRepositoryButton, nameof(dashboardAnalyzeRepositoryButton), ref error);
            valid &= Require(dashboardSaveSessionButton, nameof(dashboardSaveSessionButton), ref error);
            valid &= Require(dashboardSyncButton, nameof(dashboardSyncButton), ref error);
            valid &= Require(dashboardHistoryButton, nameof(dashboardHistoryButton), ref error);
            valid &= Require(dashboardSettingsButton, nameof(dashboardSettingsButton), ref error);
            valid &= Require(dashboardBackButton, nameof(dashboardBackButton), ref error);
            valid &= Require(settingsBackButton, nameof(settingsBackButton), ref error);
            valid &= Require(settingsDesktopCompanionStatusLabel, nameof(settingsDesktopCompanionStatusLabel), ref error);
            valid &= Require(settingsDesktopCompanionEnabledToggle, nameof(settingsDesktopCompanionEnabledToggle), ref error);
            valid &= Require(settingsDesktopCompanionClickThroughToggle, nameof(settingsDesktopCompanionClickThroughToggle), ref error);
            valid &= Require(settingsDesktopCompanionMotionModeDropdown, nameof(settingsDesktopCompanionMotionModeDropdown), ref error);
            valid &= Require(settingsResetOverlayPositionButton, nameof(settingsResetOverlayPositionButton), ref error);
            valid &= Require(accountPanel, nameof(accountPanel), ref error);
            valid &= Require(activityAnalysisPanel, nameof(activityAnalysisPanel), ref error);
            valid &= Require(approvedLocationsPanel, nameof(approvedLocationsPanel), ref error);
            valid &= Require(reviewPanel, nameof(reviewPanel), ref error);
            valid &= Require(safeSyncPanel, nameof(safeSyncPanel), ref error);
            valid &= Require(recentSessionsPanel, nameof(recentSessionsPanel), ref error);
            valid &= Require(privacyNoticePanel, nameof(privacyNoticePanel), ref error);

            valid &= ValidatePanel(accountPanel, ref error);
            valid &= ValidatePanel(activityAnalysisPanel, ref error);
            valid &= ValidatePanel(approvedLocationsPanel, ref error);
            valid &= ValidatePanel(reviewPanel, ref error);
            valid &= ValidatePanel(safeSyncPanel, ref error);
            valid &= ValidatePanel(recentSessionsPanel, ref error);
            valid &= ValidatePanel(privacyNoticePanel, ref error);
            valid &= ValidatePanel(companionView, ref error);
            valid &= ValidatePanel(companionStatusPanel, ref error);
            return valid;
        }

        private static bool ValidatePanel(UiBinderBase panel, ref string error)
        {
            if (panel == null)
            {
                return false;
            }

            if (panel.ValidateRequiredReferences(out var panelError))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                error = panel.GetType().Name + ": " + panelError;
            }

            return false;
        }

        private void EnsureDesktopCompanionRuntimeControls()
        {
            if ((dashboardDesktopCompanionLabel == null || dashboardEnableDesktopCompanionButton == null || dashboardDisableDesktopCompanionButton == null) &&
                companionStatusPanel != null)
            {
                var parent = companionStatusPanel.transform.parent;
                dashboardDesktopCompanionLabel = dashboardDesktopCompanionLabel ?? RuntimeStatusText("Dashboard Desktop Companion Runtime", parent, "Desktop Companion\nState: disabled", 108f);
                var row = RuntimeRow("Dashboard Desktop Companion Runtime Actions", parent, 48f);
                dashboardEnableDesktopCompanionButton = dashboardEnableDesktopCompanionButton ?? RuntimeButton("Enable Desktop Companion", row.transform, 230f);
                dashboardDisableDesktopCompanionButton = dashboardDisableDesktopCompanionButton ?? RuntimeButton("Disable Desktop Companion", row.transform, 230f);
            }

            if ((settingsDesktopCompanionStatusLabel == null ||
                 settingsDesktopCompanionEnabledToggle == null ||
                 settingsDesktopCompanionClickThroughToggle == null ||
                 settingsDesktopCompanionMotionModeDropdown == null ||
                 settingsResetOverlayPositionButton == null) &&
                settingsAdvancedRoot != null)
            {
                var panel = RuntimePanel("Settings Desktop Companion Runtime", settingsAdvancedRoot.transform, 260f);
                RuntimeText("Settings Desktop Companion Runtime Title", panel.transform, "Desktop Companion", 22, FontStyle.Bold, new Color(0.42f, 0.82f, 0.95f, 1f), 32f);
                settingsDesktopCompanionStatusLabel = settingsDesktopCompanionStatusLabel ?? RuntimeStatusText("Settings Desktop Companion Runtime Status", panel.transform, "Overlay state: disabled", 86f);
                var toggles = RuntimeRow("Settings Desktop Companion Runtime Toggles", panel.transform, 44f);
                settingsDesktopCompanionEnabledToggle = settingsDesktopCompanionEnabledToggle ?? RuntimeToggle("Enable desktop overlay", toggles.transform, false, 230f);
                settingsDesktopCompanionClickThroughToggle = settingsDesktopCompanionClickThroughToggle ?? RuntimeToggle("Click-through mode", toggles.transform, true, 220f);
                var options = RuntimeRow("Settings Desktop Companion Runtime Options", panel.transform, 48f);
                settingsDesktopCompanionMotionModeDropdown = settingsDesktopCompanionMotionModeDropdown ?? RuntimeDropdown("Motion Mode", options.transform, 180f);
                settingsResetOverlayPositionButton = settingsResetOverlayPositionButton ?? RuntimeButton("Reset Overlay Position", options.transform, 210f);
            }
        }

        private static bool Require(UnityEngine.Object value, string fieldName, ref string error)
        {
            if (value != null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                error = fieldName + " is not assigned.";
            }

            return false;
        }

        private void WireNavigation()
        {
            ReplaceClick(onboardingCreateAccountButton, () => RunViewModelAction(CreateAccountAndContinueAsync));
            ReplaceClick(onboardingLoginButton, () => RunViewModelAction(LoginAndContinueAsync));
            ReplaceClick(onboardingContinueOfflineButton, () =>
            {
                viewModel?.ContinueOffline(onboardingDisplayNameInput != null ? onboardingDisplayNameInput.text : string.Empty);
                Render();
                ResetRootScrollToTop();
            });
            ReplaceClick(onboardingBackButton, PreviousOnboardingStep);
            ReplaceClick(onboardingContinueButton, ContinueOnboardingStep);
            ReplaceClick(startGameButton, ShowDashboard);
            ReplaceClick(continueButton, ContinueOnboardingStep);
            ReplaceAgentToggle(cursorAgentToggle, ConnectedAgentSourceType.Cursor);
            ReplaceAgentToggle(claudeAgentToggle, ConnectedAgentSourceType.ClaudeCode);
            ReplaceAgentToggle(codexAgentToggle, ConnectedAgentSourceType.Codex);
            ReplaceAgentToggle(copilotAgentToggle, ConnectedAgentSourceType.GitHubCopilot);
            ReplaceAgentToggle(otherAgentToggle, ConnectedAgentSourceType.OtherManualLogFolder);
            ReplaceClick(cursorAgentConnectButton, () => ToggleAgentSource(ConnectedAgentSourceType.Cursor));
            ReplaceClick(claudeAgentConnectButton, () => ToggleAgentSource(ConnectedAgentSourceType.ClaudeCode));
            ReplaceClick(codexAgentConnectButton, () => ToggleAgentSource(ConnectedAgentSourceType.Codex));
            ReplaceClick(copilotAgentConnectButton, () => ToggleAgentSource(ConnectedAgentSourceType.GitHubCopilot));
            ReplaceClick(otherAgentSelectLogFolderButton, () =>
            {
                var manual = FindAgentSource(ConnectedAgentSourceType.OtherManualLogFolder);
                if (manual != null && manual.State == AgentSourceSetupState.ReadyToAnalyze)
                {
                    RunViewModelAction(() => viewModel.AnalyzeAgentSourceForOnboardingAsync(ConnectedAgentSourceType.OtherManualLogFolder));
                }
                else
                {
                    RunViewModelAction(() => viewModel.SelectManualAgentLogForOnboardingAsync());
                }
            });
            ReplaceClick(onboardingSelectLocalRepositoryButton, () => RunViewModelAction(() => viewModel.SelectLocalGitRepositoryForOnboardingAsync()));
            ReplaceClick(onboardingConnectGitAccountButton, () =>
            {
                viewModel?.SelectGitAccountPlaceholder();
                Render();
            });
            ReplaceClick(onboardingSkipGitButton, () =>
            {
                viewModel?.SkipGitForOnboarding();
                viewModel?.SetOnboardingStep(OnboardingStep.Ready);
                Render();
                ResetRootScrollToTop();
            });
            ReplaceClick(onboardingClearLocalRepositoryButton, () =>
            {
                viewModel?.ClearGitForOnboarding();
                Render();
            });
            ReplaceClick(startAnalyzeRepositoryButton, ShowRunAnalysis);
            ReplaceClick(startAnalyzeAgentLogsButton, ShowRunAnalysis);
            ReplaceClick(startReviewAnalysisButton, ShowRunAnalysis);
            ReplaceClick(startSaveRunButton, () => RunViewModelAction(SavePendingRunAndShowDashboardAsync));
            ReplaceClick(startCreateSyncAccountButton, ShowSettings);
            ReplaceClick(startLoginButton, ShowSettings);
            ReplaceClick(startCheckServerButton, () =>
            {
                if (viewModel != null && viewModel.HasSafeSyncService)
                {
                    RunViewModelAction(() => viewModel.CheckSyncHealthAsync());
                }
                else
                {
                    ShowSettings();
                }
            });
            ReplaceClick(startSyncProgressButton, () =>
            {
                if (viewModel != null && viewModel.CanUseAuthenticatedSafeSync)
                {
                    RunViewModelAction(() => viewModel.SyncSafeSessionsAsync());
                }
                else
                {
                    ShowSettings();
                }
            });
            ReplaceClick(dashboardAnalyzeRepositoryButton, ShowRunAnalysis);
            ReplaceClick(dashboardSaveSessionButton, () =>
            {
                if (viewModel == null)
                {
                    return;
                }

                if (viewModel.GitFlow.HasPendingReview)
                {
                    RunViewModelAction(SavePendingRunAndShowDashboardAsync);
                }
                else if (viewModel.AgentFlow.HasPendingReview)
                {
                    RunViewModelAction(SavePendingRunAndShowDashboardAsync);
                }
                else if (!viewModel.CharacterDashboard.HasSavedRun)
                {
                    RunViewModelAction(StartAgentRunAsync);
                }
                else
                {
                    ShowRunAnalysis();
                }
            });
            ReplaceClick(dashboardSyncButton, () =>
            {
                if (viewModel != null && viewModel.CharacterDashboard.HasSavedRun && viewModel.CanUseAuthenticatedSafeSync)
                {
                    RunViewModelAction(() => viewModel.SyncSafeSessionsAsync());
                }
                else
                {
                    ShowSettings();
                }
            });
            ReplaceClick(dashboardEnableDesktopCompanionButton, () => RunViewModelAction(() => viewModel.SetDesktopCompanionEnabledAsync(true)));
            ReplaceClick(dashboardDisableDesktopCompanionButton, () => RunViewModelAction(() => viewModel.SetDesktopCompanionEnabledAsync(false)));
            ReplaceClick(dashboardHistoryButton, ShowRunAnalysis);
            ReplaceClick(dashboardSettingsButton, ShowSettings);
            ReplaceClick(dashboardBackButton, ShowRunAnalysis);
            ReplaceClick(settingsBackButton, ShowDashboard);
            ReplaceClick(runAnalysisBackButton, ShowDashboard);
            ReplaceClick(runAnalysisSettingsButton, ShowSettings);
            ReplaceClick(settingsDeveloperDiagnosticsButton, ShowDeveloperDiagnostics);
            ReplaceClick(diagnosticsBackButton, ShowSettings);
            ReplaceClick(settingsResetOverlayPositionButton, () =>
            {
                desktopCompanionOverlayController?.ResetPosition();
                RunViewModelAction(() => viewModel.ResetDesktopCompanionPositionAsync());
            });
            ReplaceToggle(settingsDesktopCompanionEnabledToggle, enabled => RunViewModelAction(() => viewModel.SetDesktopCompanionEnabledAsync(enabled)));
            ReplaceToggle(settingsDesktopCompanionClickThroughToggle, clickThrough => RunViewModelAction(() => viewModel.SetDesktopCompanionClickThroughAsync(clickThrough)));
            if (settingsDesktopCompanionMotionModeDropdown != null)
            {
                settingsDesktopCompanionMotionModeDropdown.onValueChanged.RemoveAllListeners();
                settingsDesktopCompanionMotionModeDropdown.onValueChanged.AddListener(index =>
                {
                    RunViewModelAction(() => viewModel.SetDesktopCompanionMotionModeAsync(MotionModeFromIndex(index)));
                });
            }
        }

        public void ShowStart()
        {
            currentScreen = ScreenMode.Start;
            RenderGameShell();
            ResetRootScrollToTop();
        }

        public void ShowDashboard()
        {
            currentScreen = ScreenMode.Dashboard;
            RenderGameShell();
            ResetRootScrollToTop();
        }

        public void ShowRunAnalysis()
        {
            currentScreen = ScreenMode.RunAnalysis;
            RenderGameShell();
            ResetRootScrollToTop();
        }

        public void ShowSettings()
        {
            currentScreen = ScreenMode.Settings;
            RenderGameShell();
            ResetRootScrollToTop();
        }

        public void ShowDeveloperDiagnostics()
        {
            currentScreen = ScreenMode.DeveloperDiagnostics;
            RenderGameShell();
            ResetRootScrollToTop();
        }

        private async System.Threading.Tasks.Task CreateAccountAndContinueAsync()
        {
            if (viewModel == null)
            {
                return;
            }

            if (viewModel.HasAuthSessionService)
            {
                var validation = ValidateSignupFields();
                if (!string.IsNullOrWhiteSpace(validation))
                {
                    viewModel.SetLocalAuthMessage(validation);
                    return;
                }

                var result = await viewModel.SignupAsync(
                    onboardingEmailInput != null ? onboardingEmailInput.text : string.Empty,
                    onboardingPasswordInput != null ? onboardingPasswordInput.text : string.Empty,
                    onboardingDisplayNameInput != null ? onboardingDisplayNameInput.text : string.Empty);
                if (!result.IsSuccess)
                {
                    return;
                }
            }
            else
            {
                viewModel.SetLocalAuthMessage("Server account creation is not available in this build. Continue Offline is available.");
                return;
            }

            if (onboardingPasswordInput != null)
            {
                onboardingPasswordInput.text = string.Empty;
            }

            viewModel.SetOnboardingStep(OnboardingStep.AiAgents);
            ResetRootScrollToTop();
        }

        private async System.Threading.Tasks.Task LoginAndContinueAsync()
        {
            if (viewModel == null)
            {
                return;
            }

            if (viewModel.HasAuthSessionService)
            {
                var validation = ValidateLoginFields();
                if (!string.IsNullOrWhiteSpace(validation))
                {
                    viewModel.SetLocalAuthMessage(validation);
                    return;
                }

                var result = await viewModel.LoginAsync(
                    onboardingEmailInput != null ? onboardingEmailInput.text : string.Empty,
                    onboardingPasswordInput != null ? onboardingPasswordInput.text : string.Empty);
                if (!result.IsSuccess)
                {
                    return;
                }
            }
            else
            {
                viewModel.SetLocalAuthMessage("Server login is not available in this build. Continue Offline is available.");
                return;
            }

            if (onboardingPasswordInput != null)
            {
                onboardingPasswordInput.text = string.Empty;
            }

            viewModel.SetOnboardingStep(OnboardingStep.AiAgents);
            ResetRootScrollToTop();
        }

        private void PreviousOnboardingStep()
        {
            if (viewModel == null)
            {
                return;
            }

            switch (viewModel.Onboarding.CurrentStep)
            {
                case OnboardingStep.AiAgents:
                    viewModel.SetOnboardingStep(OnboardingStep.Account);
                    break;
                case OnboardingStep.Git:
                    viewModel.SetOnboardingStep(OnboardingStep.AiAgents);
                    break;
                case OnboardingStep.Ready:
                    viewModel.SetOnboardingStep(OnboardingStep.Git);
                    break;
            }

            Render();
            ResetRootScrollToTop();
        }

        private void ContinueOnboardingStep()
        {
            if (currentScreen == ScreenMode.Start)
            {
                ShowDashboard();
                return;
            }

            if (viewModel == null)
            {
                return;
            }

            switch (viewModel.Onboarding.CurrentStep)
            {
                case OnboardingStep.Account:
                    if (viewModel.AuthState == AuthState.LoggedIn || viewModel.Onboarding.OfflineModeSelected)
                    {
                        viewModel.SetOnboardingStep(OnboardingStep.AiAgents);
                        break;
                    }

                    return;
                case OnboardingStep.AiAgents:
                    viewModel.SetOnboardingStep(OnboardingStep.Git);
                    break;
                case OnboardingStep.Git:
                    if (!viewModel.Onboarding.GitConnected && !viewModel.Onboarding.GitSkipped)
                    {
                        return;
                    }

                    viewModel.SetOnboardingStep(OnboardingStep.Ready);
                    break;
                case OnboardingStep.Ready:
                    return;
            }

            Render();
            ResetRootScrollToTop();
        }

        private void RenderGameShell()
        {
            SetActive(startScreenRoot, currentScreen == ScreenMode.Start);
            SetActive(gameDashboardRoot, currentScreen == ScreenMode.Dashboard);
            SetActive(runAnalysisRoot, currentScreen == ScreenMode.RunAnalysis);
            SetActive(settingsAdvancedRoot, currentScreen == ScreenMode.Settings);
            SetActive(developerDiagnosticsRoot, currentScreen == ScreenMode.DeveloperDiagnostics);
            UpdateResponsiveStartLayout();
            RefreshRootScrollContentSize();
            var dashboard = viewModel?.CharacterDashboard ?? new CharacterDashboardSummary();
            var hasPendingReview = viewModel != null && (viewModel.GitFlow.HasPendingReview || viewModel.AgentFlow.HasPendingReview);
            var hasSavedRun = dashboard.HasSavedRun;
            SetText(loginStatusSmallLabel, viewModel != null && viewModel.AuthState == TokenForge.Client.Auth.AuthState.LoggedIn
                ? "Safe Sync connected"
                : "Local mode · Sync optional");
            RenderOnboarding(dashboard);
            SetText(startCharacterLabel, BuildStartCharacterText(dashboard));
            SetText(startStatsLabel, BuildStartStatsText(dashboard));
            SetText(startGrowthLabel, BuildStartGrowthText(dashboard, hasPendingReview));
            SetText(startRecentSessionsLabel, BuildStartRecentText(dashboard));
            SetText(startPrivacySummaryLabel, PrivacySummaryText);
            SetText(startRunStatusLabel, "Turn local development activity into RPG growth.");
            SetButtonLabel(startGameButton, dashboard.HasSavedRun ? "Continue" : "Start Game");
            SetButtonLabel(continueButton, "Continue");
            SetButtonLabel(startReviewAnalysisButton, "Save Review");
            SetButtonLabel(startSaveRunButton, "Save Review");
            SetButton(startGameButton, true);
            SetButton(continueButton, currentScreen == ScreenMode.Start || CanContinueOnboarding(viewModel?.Onboarding.CurrentStep ?? OnboardingStep.Account));
            SetButton(startAnalyzeRepositoryButton, viewModel != null);
            SetButton(startAnalyzeAgentLogsButton, viewModel != null);
            SetButton(startReviewAnalysisButton, hasPendingReview);
            SetButton(startSaveRunButton, hasPendingReview);
            SetButton(startCreateSyncAccountButton, true);
            SetButton(startLoginButton, true);
            SetButton(startCheckServerButton, true);
            SetButtonLabel(startAnalyzeRepositoryButton, "Run Analysis");
            SetButtonLabel(startSyncProgressButton, "Safe Sync");
            SetButton(startSyncProgressButton, true);
            SetActive(startAnalyzeRepositoryButton != null ? startAnalyzeRepositoryButton.gameObject : null, true);
            SetActive(startAnalyzeAgentLogsButton != null ? startAnalyzeAgentLogsButton.gameObject : null, false);
            SetActive(startReviewAnalysisButton != null ? startReviewAnalysisButton.gameObject : null, hasPendingReview);
            SetActive(startSaveRunButton != null ? startSaveRunButton.gameObject : null, false);
            SetActive(startCreateSyncAccountButton != null ? startCreateSyncAccountButton.gameObject : null, false);
            SetActive(startLoginButton != null ? startLoginButton.gameObject : null, false);
            SetActive(startCheckServerButton != null ? startCheckServerButton.gameObject : null, false);
            SetActive(startSyncProgressButton != null ? startSyncProgressButton.gameObject : null, true);

            SetText(dashboardCharacterStatusLabel, BuildDashboardCharacterText(dashboard));
            companionView?.Render(dashboard.CompanionState, currentScreen != ScreenMode.Dashboard || hasPendingReview);
            companionStatusPanel?.Render();
            SetText(dashboardQuestLabel, BuildDashboardStatsText(dashboard));
            SetText(dashboardActivityLogLabel, BuildDashboardSourcesAndActionText(dashboard, hasPendingReview));
            var syncReason = viewModel != null && viewModel.CanUseAuthenticatedSafeSync
                ? BootstrapUiTextFormatter.SafeSyncStatus(viewModel)
                : "Connection: " + (viewModel == null ? "Local only" : viewModel.SafeSyncConnection.StatusLabel)
                  + "\nLocal gameplay works without login or server."
                  + "\nSafe Sync is optional.";
            SetText(dashboardSyncReasonLabel, syncReason);
            RenderDesktopCompanionControls(dashboard);
            var unresolved = viewModel?.ConflictSummary?.UnresolvedCount ?? 0;
            SetActive(conflictBanner, unresolved > 0 && currentScreen == ScreenMode.Dashboard);
            SetText(conflictBannerLabel, unresolved > 0 ? BootstrapUiTextFormatter.CompactConflictBanner(viewModel) : string.Empty);
            SetButtonLabel(dashboardAnalyzeRepositoryButton, hasSavedRun ? "Analyze Now" : "Analyze Local Sources");
            SetButtonLabel(dashboardSaveSessionButton, hasPendingReview ? "Save Review" : "Save Review");
            SetButtonLabel(dashboardSyncButton, "Sync Later");
            SetButton(dashboardAnalyzeRepositoryButton, viewModel != null);
            SetButton(dashboardSaveSessionButton, viewModel != null && hasPendingReview);
            SetButton(dashboardSyncButton, viewModel != null && CanSyncLater(dashboard));
            SetButtonLabel(dashboardHistoryButton, "Run Analysis");
            SetButtonLabel(dashboardSettingsButton, "Settings");
            SetButton(dashboardHistoryButton, true);
            SetButton(dashboardSettingsButton, true);
            SetButton(dashboardBackButton, true);
            SetButton(settingsBackButton, true);
            SetButton(runAnalysisBackButton, true);
            SetButton(runAnalysisSettingsButton, true);
            SetButton(settingsDeveloperDiagnosticsButton, true);
            SetButton(diagnosticsBackButton, true);
            SetText(runAnalysisTitleLabel, "Run Analysis");
            SetText(settingsSummaryLabel, BuildSettingsSummary());
            SetText(settingsSyncConflictLabel, BuildSettingsSyncConflictSummary());
        }

        private void RenderDesktopCompanionControls(CharacterDashboardSummary dashboard)
        {
            var settings = dashboard?.DesktopCompanionSettings ?? DesktopCompanionSettings.CreateDefault();
            var actualState = desktopCompanionOverlayController?.OverlayState ?? dashboard?.DesktopOverlayState ?? CompanionDesktopOverlayState.Unavailable;
            if (!settings.IsDesktopCompanionEnabled && actualState != CompanionDesktopOverlayState.Unavailable)
            {
                actualState = CompanionDesktopOverlayState.Disabled;
            }

            SetText(dashboardDesktopCompanionLabel,
                "Desktop Companion"
                + "\nState: " + actualState.ToString().ToLowerInvariant()
                + "\nMode: " + settings.MotionMode
                + "\nInteraction: " + (settings.IsClickThroughEnabled ? "Click-through" : "Interactive")
                + "\nPreview remains in-app; the desktop pet uses the native overlay when available.");
            SetButton(dashboardEnableDesktopCompanionButton, viewModel != null && !settings.IsDesktopCompanionEnabled);
            SetButton(dashboardDisableDesktopCompanionButton, viewModel != null && settings.IsDesktopCompanionEnabled);
            SetButtonLabel(dashboardEnableDesktopCompanionButton, "Enable Desktop Companion");
            SetButtonLabel(dashboardDisableDesktopCompanionButton, "Disable Desktop Companion");

            SetText(settingsDesktopCompanionStatusLabel,
                "Desktop Companion"
                + "\nOverlay state: " + actualState.ToString().ToLowerInvariant()
                + "\nMotion mode: " + settings.MotionMode
                + "\nClick-through: " + (settings.IsClickThroughEnabled ? "enabled" : "disabled")
                + "\nIf unsupported, TokenForge keeps the in-app preview and status controls available.");
            if (settingsDesktopCompanionEnabledToggle != null)
            {
                settingsDesktopCompanionEnabledToggle.SetIsOnWithoutNotify(settings.IsDesktopCompanionEnabled);
            }

            if (settingsDesktopCompanionClickThroughToggle != null)
            {
                settingsDesktopCompanionClickThroughToggle.SetIsOnWithoutNotify(settings.IsClickThroughEnabled);
            }

            if (settingsDesktopCompanionMotionModeDropdown != null)
            {
                EnsureMotionDropdownOptions(settingsDesktopCompanionMotionModeDropdown);
                settingsDesktopCompanionMotionModeDropdown.SetValueWithoutNotify(MotionModeToIndex(settings.MotionMode));
            }

            SetButton(settingsResetOverlayPositionButton, viewModel != null);
        }

        private void RenderOnboarding(CharacterDashboardSummary dashboard)
        {
            var step = currentScreen == ScreenMode.Start
                ? OnboardingStep.Ready
                : viewModel?.Onboarding.CurrentStep ?? OnboardingStep.Account;
            SetActive(onboardingAccountStepRoot, step == OnboardingStep.Account);
            SetActive(onboardingAiAgentsStepRoot, step == OnboardingStep.AiAgents);
            SetActive(onboardingGitStepRoot, step == OnboardingStep.Git);
            SetActive(onboardingReadyStepRoot, step == OnboardingStep.Ready);
            SetText(onboardingStepIndicatorLabel, currentScreen == ScreenMode.Start
                ? "Start Game  >  Run Analysis  >  Review Safe Summary  >  Save Progress"
                : BuildStepIndicator(step));
            SetText(onboardingPrimaryTitleLabel, "TokenForge");
            SetText(onboardingAccountStatusLabel, BuildAccountStepStatus(dashboard));
            SetText(onboardingAiAgentsStatusLabel, "Selected: " + BootstrapUiTextFormatter.SelectedAgentSummary(viewModel));
            SetText(onboardingGitStatusLabel, BuildGitOnboardingStatus());
            SetText(onboardingReadySummaryLabel, currentScreen == ScreenMode.Start ? BuildStartReadySummary() : BuildReadySummary());
            SetButton(onboardingBackButton, step != OnboardingStep.Account);
            SetButtonLabel(onboardingContinueButton, step == OnboardingStep.Ready ? "Start Game" : "Continue");
            SetButton(onboardingContinueButton, currentScreen == ScreenMode.Start || CanContinueOnboarding(step));
            SetButton(onboardingCreateAccountButton, viewModel != null && !viewModel.IsAuthRequestInProgress);
            SetButton(onboardingLoginButton, viewModel != null && !viewModel.IsAuthRequestInProgress);
            SetButton(onboardingContinueOfflineButton, viewModel != null && !viewModel.IsAuthRequestInProgress);
            SetButtonLabel(onboardingCreateAccountButton, viewModel != null && viewModel.AuthState == AuthState.SigningUp ? "Creating..." : "Create Account");
            SetButtonLabel(onboardingLoginButton, viewModel != null && viewModel.AuthState == AuthState.LoggingIn ? "Logging in..." : "Log In");
            RenderAgentSource(ConnectedAgentSourceType.Cursor, cursorAgentToggle, cursorAgentStatusLabel);
            RenderAgentSource(ConnectedAgentSourceType.ClaudeCode, claudeAgentToggle, claudeAgentStatusLabel);
            RenderAgentSource(ConnectedAgentSourceType.Codex, codexAgentToggle, codexAgentStatusLabel);
            RenderAgentSource(ConnectedAgentSourceType.GitHubCopilot, copilotAgentToggle, copilotAgentStatusLabel);
            RenderAgentSource(ConnectedAgentSourceType.OtherManualLogFolder, otherAgentToggle, otherAgentStatusLabel);
            SetButtonLabel(cursorAgentConnectButton, AgentButtonLabel(ConnectedAgentSourceType.Cursor));
            SetButtonLabel(claudeAgentConnectButton, AgentButtonLabel(ConnectedAgentSourceType.ClaudeCode));
            SetButtonLabel(codexAgentConnectButton, AgentButtonLabel(ConnectedAgentSourceType.Codex));
            SetButtonLabel(copilotAgentConnectButton, AgentButtonLabel(ConnectedAgentSourceType.GitHubCopilot));
            SetButtonLabel(otherAgentSelectLogFolderButton, ManualAgentButtonLabel());
            SetButtonLabel(onboardingSelectLocalRepositoryButton, viewModel != null && viewModel.Onboarding.GitConnected ? "Change Repository" : "Select Local Repository");
            SetButtonLabel(onboardingConnectGitAccountButton, "Git Account Coming Later");
            SetActive(onboardingConnectGitAccountButton != null ? onboardingConnectGitAccountButton.gameObject : null, false);
            SetButton(onboardingConnectGitAccountButton, false);
            SetButton(onboardingClearLocalRepositoryButton, viewModel != null && (viewModel.Onboarding.GitConnected || viewModel.Onboarding.GitSkipped || viewModel.Onboarding.GitAccountPlaceholderSelected));
        }

        private bool CanContinueOnboarding(OnboardingStep step)
        {
            if (viewModel == null)
            {
                return false;
            }

            switch (step)
            {
                case OnboardingStep.Account:
                    return viewModel.AuthState == AuthState.LoggedIn || viewModel.Onboarding.OfflineModeSelected;
                case OnboardingStep.Git:
                    return viewModel.Onboarding.GitConnected || viewModel.Onboarding.GitSkipped;
                case OnboardingStep.Ready:
                    return false;
                default:
                    return true;
            }
        }

        private void RenderAgentSource(ConnectedAgentSourceType sourceType, Toggle toggle, Text statusLabel)
        {
            var source = FindAgentSource(sourceType);
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(source != null && source.Selected);
            }

            SetText(statusLabel, source == null ? "Not selected" : source.StatusLabel);
        }

        private string AgentButtonLabel(ConnectedAgentSourceType sourceType)
        {
            var source = FindAgentSource(sourceType);
            if (source == null || !source.Selected)
            {
                return "Select";
            }

            switch (source.State)
            {
                case AgentSourceSetupState.Selected: return "Detect";
                case AgentSourceSetupState.DetectingLocalSource: return "Detecting";
                case AgentSourceSetupState.ReadyToAnalyze: return "Analyze";
                case AgentSourceSetupState.AnalysisComplete: return "Remove";
                case AgentSourceSetupState.PermissionRequired:
                case AgentSourceSetupState.ManualImportRequired:
                case AgentSourceSetupState.AnalysisFailedSafely:
                    return "Choose Folder";
                default:
                    return "Detect";
            }
        }

        private string ManualAgentButtonLabel()
        {
            var source = FindAgentSource(ConnectedAgentSourceType.OtherManualLogFolder);
            return source != null && source.State == AgentSourceSetupState.ReadyToAnalyze ? "Analyze" : "Choose Folder";
        }

        private ConnectedAgentSource FindAgentSource(ConnectedAgentSourceType sourceType)
        {
            if (viewModel == null)
            {
                return null;
            }

            for (var i = 0; i < viewModel.Onboarding.AgentSources.Count; i++)
            {
                if (viewModel.Onboarding.AgentSources[i].SourceType == sourceType)
                {
                    return viewModel.Onboarding.AgentSources[i];
                }
            }

            return null;
        }

        private static string BuildStepIndicator(OnboardingStep step)
        {
            return StepMark(step, OnboardingStep.Account, "1. Account") + "  >  "
                   + StepMark(step, OnboardingStep.AiAgents, "2. AI Agents") + "  >  "
                   + StepMark(step, OnboardingStep.Git, "3. Git") + "  >  "
                   + StepMark(step, OnboardingStep.Ready, "4. Start Game");
        }

        private static string StepMark(OnboardingStep current, OnboardingStep step, string label)
        {
            if (current == step)
            {
                return "[" + label + "]";
            }

            return label;
        }

        private string BuildAccountStepStatus(CharacterDashboardSummary dashboard)
        {
            if (viewModel != null && viewModel.AuthState == AuthState.LoggedIn)
            {
                return "Online account ready.";
            }

            if (viewModel != null && viewModel.HasExplicitAuthMessage && !string.IsNullOrWhiteSpace(viewModel.AuthMessage))
            {
                return viewModel.AuthMessage;
            }

            if (viewModel != null && viewModel.Onboarding.OfflineModeSelected)
            {
                return "Local-only progress. Sync can be enabled later.";
            }

            return "Create an account to save your progress and enable Safe Sync.";
        }

        private string BuildGitOnboardingStatus()
        {
            if (viewModel == null)
            {
                return "No Git source selected.";
            }

            if (viewModel.Onboarding.GitConnected)
            {
                return "Local repository selected: " + BootstrapUiTextFormatter.GitSafeAlias(viewModel);
            }

            if (viewModel.Onboarding.GitAccountPlaceholderSelected)
            {
                return "Git account connection is not available in this MVP. Select a local repository or skip for now.";
            }

            if (viewModel.Onboarding.GitSkipped)
            {
                return "Git skipped for now.";
            }

            return "No Git source selected.";
        }

        private string BuildReadySummary()
        {
            var account = BuildAccountModeSummary();
            var agentCount = BootstrapUiTextFormatter.SelectedAgentCount(viewModel);
            return "Current step: " + OnboardingStepLabel(viewModel?.Onboarding.CurrentStep ?? OnboardingStep.Account)
                   + "\nAccount mode: " + account
                   + "\nAI Agents: " + agentCount + " selected"
                   + "\nSources: " + BootstrapUiTextFormatter.SelectedAgentSummary(viewModel)
                   + "\nGit: " + BuildGitOnboardingStatus()
                   + "\nSafe Sync: " + BuildSafeSyncSummary()
                   + "\nPrivacy: Only safe aggregate metadata is saved. Raw paths and logs are hidden.";
        }

        private string BuildStartReadySummary()
        {
            var dashboard = viewModel?.CharacterDashboard ?? new CharacterDashboardSummary();
            if (dashboard.HasSavedRun)
            {
                return "Continue your local game.\nRecent Growth: " + dashboard.RecentGrowthSummary
                       + "\nLast Saved: " + dashboard.LatestSafeSessionSummary
                       + "\nSafe Sync: optional and never required for local growth.\nPrivacy: " + PrivacySummaryText;
            }

            return "No saved run yet.\nStart Game opens the dashboard without login or sync.\nRun Analysis creates a pending safe review.\nSaving the review is the only step that grants XP.\nPrivacy: " + PrivacySummaryText;
        }

        private string BuildStartCharacterText(CharacterDashboardSummary dashboard)
        {
            if (dashboard != null && dashboard.HasSavedRun)
            {
                return "Continue\n" + dashboard.CharacterName + " | Level " + dashboard.Level
                       + "\n" + dashboard.RankTitle
                       + "\n" + dashboard.RecentGrowthSummary;
            }

            return "Friendly empty state\nNo saved run yet.\nStart Game opens the dashboard.\nRun Analysis prepares your first safe review.";
        }

        private static string BuildStartStatsText(CharacterDashboardSummary dashboard)
        {
            if (dashboard != null && dashboard.HasSavedRun)
            {
                return "Recent Growth\nXP " + dashboard.CurrentLevelExp + " / " + dashboard.ExpForNextLevel
                       + "\nCode " + dashboard.Code
                       + "\nFocus " + dashboard.Focus
                       + "\nDebug " + dashboard.Debug
                       + "\nDesign " + dashboard.Design
                       + "\nSync " + dashboard.Sync;
            }

            return "Game Loop\n1. Select a local source\n2. Analyze safe aggregates\n3. Review the summary\n4. Save progress for XP";
        }

        private string BuildStartGrowthText(CharacterDashboardSummary dashboard, bool hasPendingReview)
        {
            if (hasPendingReview)
            {
                return "Review ready\nA safe aggregate summary is pending. Save Review to apply XP and stats.";
            }

            if (dashboard != null && dashboard.HasSavedRun)
            {
                return "Last saved summary\n" + dashboard.LatestSafeSessionSummary;
            }

            return "Growth\nSelecting providers and detecting sources do not grant XP. Saving a reviewed aggregate does.";
        }

        private static string BuildStartRecentText(CharacterDashboardSummary dashboard)
        {
            if (dashboard != null && dashboard.HasSavedRun)
            {
                return "Recent Runs\n" + dashboard.ActivityLogSummary;
            }

            return "Recent Runs\nNo saved safe sessions yet.";
        }

        private string BuildDashboardCharacterText(CharacterDashboardSummary dashboard)
        {
            var player = dashboard.CharacterName;
            if (viewModel == null || (viewModel.AuthState != TokenForge.Client.Auth.AuthState.LoggedIn && !viewModel.Onboarding.OfflineModeSelected))
            {
                player = "Local Player";
            }

            return player + "\nLevel " + dashboard.Level + " | " + dashboard.RankTitle
                   + "\nXP " + dashboard.CurrentLevelExp + " / " + dashboard.ExpForNextLevel
                   + "\nToday's Growth: " + dashboard.RecentGrowthSummary;
        }

        private static string BuildDashboardStatsText(CharacterDashboardSummary dashboard)
        {
            return "Stats"
                   + "\nCode " + dashboard.Code
                   + "\nFocus " + dashboard.Focus
                   + "\nDebug " + dashboard.Debug
                   + "\nDesign " + dashboard.Design
                   + "\nSync " + dashboard.Sync;
        }

        private string BuildDashboardSourcesAndActionText(CharacterDashboardSummary dashboard, bool hasPendingReview)
        {
            var git = viewModel != null && viewModel.Onboarding.GitConnected
                ? BootstrapUiTextFormatter.GitSafeAlias(viewModel)
                : (viewModel != null && viewModel.Onboarding.GitSkipped ? "Skipped" : "No source selected");
            var action = BuildNextAction(hasPendingReview);

            if (dashboard != null && dashboard.HasSavedRun)
            {
                return "Recent Run"
                       + "\n" + dashboard.LatestSafeSessionSummary
                       + "\n\nSaved Activity"
                       + "\n" + dashboard.ActivityLogSummary
                       + "\n\nNext Action"
                       + "\n" + action;
            }

            return "Selected Providers"
                   + "\nAI Agents: " + BootstrapUiTextFormatter.SelectedAgentCount(viewModel)
                   + "\nDetected Local Sources: " + BootstrapUiTextFormatter.DetectedLocalSourceCount(viewModel)
                   + "\nReady to Analyze: " + BootstrapUiTextFormatter.ReadyToAnalyzeCount(viewModel)
                   + "\nPending Reviews: " + (hasPendingReview ? 1 : 0)
                   + "\nSaved Runs: " + (dashboard != null && dashboard.HasSavedRun ? "available" : "0")
                   + "\nToday's Growth: " + (dashboard?.RecentGrowthSummary ?? "No growth recorded yet.")
                   + "\nGit: " + git
                   + "\n\nNext Action"
                   + "\n" + action;
        }

        private string BuildNextAction(bool hasPendingReview)
        {
            if ((viewModel?.ConflictSummary?.UnresolvedCount ?? 0) > 0)
            {
                return "Resolve sync conflict in Settings.";
            }

            if (hasPendingReview)
            {
                return "Review and save safe summary.";
            }

            if (viewModel == null || (!viewModel.Onboarding.GitConnected && BootstrapUiTextFormatter.SelectedAgentCount(viewModel) == 0))
            {
                return "Select a repository or agent source.";
            }

            if (viewModel.GitFlow.State == GitAnalysisFlowState.Selected || viewModel.AgentFlow.State == AgentAnalysisFlowState.Selected ||
                BootstrapUiTextFormatter.ReadyToAnalyzeCount(viewModel) > 0)
            {
                return "Run local analysis.";
            }

            if (viewModel.CharacterDashboard.HasSavedRun && !viewModel.CanUseAuthenticatedSafeSync)
            {
                return "Optional: sync later.";
            }

            return "Run local analysis.";
        }

        private bool CanSyncLater(CharacterDashboardSummary dashboard)
        {
            return viewModel != null &&
                   dashboard != null &&
                   dashboard.HasSavedRun &&
                   viewModel.CanUseAuthenticatedSafeSync &&
                   !viewModel.IsSafeSyncRequestInProgress;
        }

        private string BuildSettingsSummary()
        {
            return "Selected Providers: " + BootstrapUiTextFormatter.SelectedAgentSummary(viewModel)
                   + "\nGit Source: " + BuildGitOnboardingStatus()
                   + "\nDetected Local Sources: " + BootstrapUiTextFormatter.DetectedLocalSourceCount(viewModel)
                   + "\nReady to Analyze: " + BootstrapUiTextFormatter.ReadyToAnalyzeCount(viewModel)
                   + "\nManual Folder: " + ManualFolderStatus()
                   + "\nGit Alias: " + BootstrapUiTextFormatter.GitSafeAlias(viewModel)
                   + "\nNo raw absolute paths are shown."
                   + "\n" + BootstrapUiTextFormatter.LocalSourcesSettings(viewModel)
                   + "\nAdvanced / Developer Diagnostics is collapsed by default.";
        }

        private string BuildSettingsSyncConflictSummary()
        {
            var retry = viewModel?.RetryQueueSummary ?? new TokenForge.Client.Sync.SafeSyncRetryQueueSummary();
            var conflicts = viewModel?.ConflictSummary ?? new TokenForge.Client.Sync.SafeSyncConflictSummary();
            var tombstones = viewModel?.TombstoneSummary ?? new TokenForge.Client.Sync.SafeSyncTombstoneSummary();
            return "Sync State: " + (viewModel == null ? "Unavailable" : SafeUserMessageMapper.SafeSyncStatusLabel(viewModel.SafeSyncStatus))
                   + "\nAccount: " + BuildAccountModeSummary()
                   + "\nConnection: " + (viewModel == null ? "Local only" : viewModel.SafeSyncConnection.StatusLabel)
                   + "\nServer: " + (viewModel == null ? SafeSyncApiConfig.DefaultBaseUrl : viewModel.SafeSyncBaseUrl)
                   + "\nSafe Sync: optional. Only saved aggregate summaries can sync."
                   + "\nLocal gameplay: available without login or server."
                   + "\nPending uploads: " + retry.PendingCount
                   + "\nPending deletes / tombstones: " + tombstones.PendingDeleteCount
                   + "\nLast sync result: " + (viewModel == null ? "No sync attempted." : viewModel.SafeSyncConnection.LastSyncResult)
                   + "\nConflicts: " + conflicts.UnresolvedCount
                   + "\nResolve conflict CTA: " + (conflicts.UnresolvedCount > 0 ? "Open Developer Diagnostics" : "Hidden until a real conflict exists.");
        }

        private string ManualFolderStatus()
        {
            var manual = FindAgentSource(ConnectedAgentSourceType.OtherManualLogFolder);
            if (manual == null || !manual.Selected)
            {
                return "Not selected";
            }

            if (!string.IsNullOrWhiteSpace(manual.SafeLabel))
            {
                return manual.SafeLabel + " | " + BootstrapUiTextFormatter.SourceStateLabel(manual.State);
            }

            return BootstrapUiTextFormatter.SourceStateLabel(manual.State);
        }

        private string BuildAccountModeSummary()
        {
            if (viewModel == null)
            {
                return "Not set";
            }

            if (viewModel.AuthState == AuthState.LoggedIn)
            {
                return "Signed in";
            }

            return viewModel.Onboarding.OfflineModeSelected ? "Local only" : "Not set";
        }

        private string BuildSafeSyncSummary()
        {
            if (viewModel == null)
            {
                return "Optional";
            }

            if (viewModel.AuthState == AuthState.LoggedIn && viewModel.CanUseAuthenticatedSafeSync)
            {
                return "Connected";
            }

            return viewModel.Onboarding.OfflineModeSelected ? "Optional / not enabled" : "Requires login";
        }

        private static string OnboardingStepLabel(OnboardingStep step)
        {
            switch (step)
            {
                case OnboardingStep.AiAgents:
                    return "AI Agents";
                case OnboardingStep.Git:
                    return "Git";
                case OnboardingStep.Ready:
                    return "Start Game";
                default:
                    return "Account";
            }
        }

        private void OnStartPrimaryAction()
        {
            if (viewModel == null)
            {
                ShowDashboard();
                return;
            }

            if (viewModel.CharacterDashboard.HasSavedRun)
            {
                ShowDashboard();
            }
            else if (viewModel.GitFlow.HasPendingReview || viewModel.AgentFlow.HasPendingReview)
            {
                RunViewModelAction(SavePendingRunAndShowDashboardAsync);
            }
            else
            {
                RunViewModelAction(StartGitRunAsync);
            }
        }

        private async System.Threading.Tasks.Task StartGitRunAsync()
        {
            if (viewModel == null)
            {
                return;
            }

            if (viewModel.ApprovedGitLocations.Count > 0)
            {
                var selectResult = await viewModel.SelectApprovedGitLocationAsync(viewModel.ApprovedGitLocations[0].LocalId);
                if (selectResult.IsSuccess)
                {
                    await viewModel.GitFlow.AnalyzeAsync();
                }
            }
            else
            {
                await viewModel.GitFlow.SelectRepositoryAsync();
                ShowSettings();
            }
        }

        private async System.Threading.Tasks.Task StartAgentRunAsync()
        {
            if (viewModel == null)
            {
                return;
            }

            if (viewModel.ApprovedAgentLocations.Count > 0)
            {
                var selectResult = await viewModel.SelectApprovedAgentLocationAsync(viewModel.ApprovedAgentLocations[0].LocalId);
                if (selectResult.IsSuccess)
                {
                    await viewModel.AnalyzeAgentActivityAsync();
                }
            }
            else
            {
                await viewModel.SelectAgentLogLocationAsync();
                ShowSettings();
            }
        }

        private async System.Threading.Tasks.Task SavePendingRunAndShowDashboardAsync()
        {
            if (viewModel == null)
            {
                return;
            }

            if (viewModel.GitFlow.HasPendingReview)
            {
                await viewModel.SaveGitSessionAsync();
            }
            else if (viewModel.AgentFlow.HasPendingReview)
            {
                await viewModel.SaveAgentSessionAsync();
            }

            ShowDashboard();
        }

        private static string BuildStartRunStatus(bool hasPendingReview)
        {
            if (hasPendingReview)
            {
                return "Step 1 complete. Review buckets, confidence, warnings, and stat deltas before saving.\nStep 3 ready. Saving applies XP and stat growth to your local character.";
            }

            return "Step 1. Connect activity source: choose a local repo or approved AI agent log folder. TokenForge only analyzes safe aggregates.\nStep 2. Review safe aggregate: Run an analysis first.\nStep 3. Save and gain XP: Saving applies XP and stat growth to your local character.";
        }

        private void RunViewModelAction(Func<System.Threading.Tasks.Task> action)
        {
            if (action == null)
            {
                return;
            }

            _ = RunAndRenderAsync(action);
        }

        private void ToggleAgentSource(ConnectedAgentSourceType sourceType)
        {
            if (viewModel == null)
            {
                return;
            }

            var source = FindAgentSource(sourceType);
            if (source == null || !source.Selected)
            {
                viewModel.ToggleAgentSourceForOnboarding(sourceType);
                Render();
                return;
            }

            if (source.State == AgentSourceSetupState.Selected)
            {
                RunViewModelAction(() => viewModel.DetectAgentSourceForOnboardingAsync(sourceType));
                return;
            }

            if (source.State == AgentSourceSetupState.ReadyToAnalyze)
            {
                RunViewModelAction(() => viewModel.AnalyzeAgentSourceForOnboardingAsync(sourceType));
                return;
            }

            if (source.State == AgentSourceSetupState.ManualImportRequired ||
                source.State == AgentSourceSetupState.PermissionRequired ||
                source.State == AgentSourceSetupState.AnalysisFailedSafely)
            {
                RunViewModelAction(() => viewModel.SelectManualAgentLogForOnboardingAsync(sourceType));
                return;
            }

            viewModel.SetAgentSourceSelected(sourceType, false);
            Render();
        }

        private void ResetRootScrollToTop()
        {
            if (rootScrollRect == null || rootScrollRect.content == null)
            {
                return;
            }

            RefreshRootScrollContentSize();
            rootScrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        private void RefreshRootScrollContentSize()
        {
            if (rootScrollRect == null || rootScrollRect.content == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootScrollRect.content);
            var fitter = rootScrollRect.content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }

            var layoutPreferredHeight = LayoutUtility.GetPreferredHeight(rootScrollRect.content);
            var manualPreferredHeight = CalculateRootScrollContentHeight(rootScrollRect.content);
            var preferredHeight = Mathf.Max(layoutPreferredHeight, manualPreferredHeight);
            if (rootScrollRect.viewport != null)
            {
                preferredHeight = Mathf.Max(preferredHeight, rootScrollRect.viewport.rect.height + 1f);
            }

            if (preferredHeight > 0f)
            {
                rootScrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
            }

#if DEBUG || UNITY_EDITOR
            Debug.Log("INFO [TokenForgeScroll] refresh viewportHeight="
                + (rootScrollRect.viewport != null ? rootScrollRect.viewport.rect.height.ToString("0.##") : "<missing>")
                + " contentHeight=" + rootScrollRect.content.rect.height.ToString("0.##")
                + " preferredHeight=" + preferredHeight.ToString("0.##")
                + " layoutPreferredHeight=" + layoutPreferredHeight.ToString("0.##")
                + " manualPreferredHeight=" + manualPreferredHeight.ToString("0.##")
                + " contentOverflow=" + (rootScrollRect.viewport != null && rootScrollRect.content.rect.height > rootScrollRect.viewport.rect.height + 0.5f));
#endif
        }

        private static float CalculateRootScrollContentHeight(RectTransform content)
        {
            if (content == null)
            {
                return 0f;
            }

            var group = content.GetComponent<VerticalLayoutGroup>();
            var height = group != null ? group.padding.top + group.padding.bottom : 0f;
            var activeChildCount = 0;
            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                {
                    continue;
                }

                var element = child.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout)
                {
                    continue;
                }

                var childHeight = 0f;
                if (element != null)
                {
                    childHeight = Mathf.Max(childHeight, element.preferredHeight, element.minHeight);
                }

                childHeight = Mathf.Max(childHeight, LayoutUtility.GetPreferredHeight(child), child.rect.height);
                if (childHeight <= 0f)
                {
                    continue;
                }

                if (activeChildCount > 0 && group != null)
                {
                    height += group.spacing;
                }

                height += childHeight;
                activeChildCount++;
            }

            return height;
        }

        private async System.Threading.Tasks.Task RunAndRenderAsync(Func<System.Threading.Tasks.Task> action)
        {
            try
            {
                var task = action();
                Render();
                if (task != null)
                {
                    await task;
                }
            }
            finally
            {
                Render();
            }
        }

        private string ValidateSignupFields()
        {
            var email = onboardingEmailInput != null ? onboardingEmailInput.text : string.Empty;
            var displayName = onboardingDisplayNameInput != null ? onboardingDisplayNameInput.text : string.Empty;
            var password = onboardingPasswordInput != null ? onboardingPasswordInput.text : string.Empty;
            if (!LooksLikeEmail(email))
            {
                return "Enter a valid email address.";
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "Enter a display name.";
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return "Enter a password.";
            }

            return string.Empty;
        }

        private string ValidateLoginFields()
        {
            var email = onboardingEmailInput != null ? onboardingEmailInput.text : string.Empty;
            var password = onboardingPasswordInput != null ? onboardingPasswordInput.text : string.Empty;
            if (!LooksLikeEmail(email))
            {
                return "Enter a valid email address.";
            }

            return string.IsNullOrWhiteSpace(password) ? "Enter your password." : string.Empty;
        }

        private static bool LooksLikeEmail(string email)
        {
            email = email ?? string.Empty;
            return email.Contains("@") && email.LastIndexOf('.') > email.IndexOf('@') + 1;
        }

        private static void ReplaceClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private void ReplaceAgentToggle(Toggle toggle, ConnectedAgentSourceType sourceType)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(value =>
            {
                viewModel?.SetAgentSourceSelected(sourceType, value);
                Render();
            });
        }

        private static void ReplaceToggle(Toggle toggle, UnityEngine.Events.UnityAction<bool> action)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.onValueChanged.RemoveAllListeners();
            if (action != null)
            {
                toggle.onValueChanged.AddListener(action);
            }
        }

        private static void EnsureMotionDropdownOptions(Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options.Count == 3)
            {
                return;
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string> { "Calm", "Normal", "Playful" });
        }

        private static int MotionModeToIndex(CompanionDesktopMotionMode mode)
        {
            switch (mode)
            {
                case CompanionDesktopMotionMode.Calm: return 0;
                case CompanionDesktopMotionMode.Playful: return 2;
                default: return 1;
            }
        }

        private static CompanionDesktopMotionMode MotionModeFromIndex(int index)
        {
            if (index <= 0) return CompanionDesktopMotionMode.Calm;
            if (index >= 2) return CompanionDesktopMotionMode.Playful;
            return CompanionDesktopMotionMode.Normal;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void SetButtonLabel(Button button, string value)
        {
            var label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject gameObject, bool active)
        {
            if (gameObject != null && gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }

        private static GameObject RuntimePanel(string name, Transform parent, float height)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = new Color(0.055f, 0.071f, 0.094f, 1f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var element = panel.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return panel;
        }

        private static GameObject RuntimeRow(string name, Transform parent, float height)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return row;
        }

        private static Text RuntimeStatusText(string name, Transform parent, string value, float height)
        {
            var panel = RuntimePanel(name + " Block", parent, height);
            return RuntimeText(name, panel.transform, value, 14, FontStyle.Bold, new Color(0.86f, 0.90f, 0.95f, 1f), height - 8f);
        }

        private static Text RuntimeText(string name, Transform parent, string value, int size, FontStyle style, Color color, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = value ?? string.Empty;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return text;
        }

        private static Button RuntimeButton(string label, Transform parent, float width)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.18f, 0.39f, 0.52f, 1f);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minHeight = 42f;
            element.preferredHeight = 42f;
            var text = RuntimeText("Label", go.transform, label, 13, FontStyle.Bold, Color.white, 38f);
            text.alignment = TextAnchor.MiddleCenter;
            StretchRuntime(text.rectTransform, 6f, 0f, 6f, 0f);
            return go.GetComponent<Button>();
        }

        private static Toggle RuntimeToggle(string label, Transform parent, bool isOn, float width)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Toggle), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minHeight = 38f;
            element.preferredHeight = 38f;
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            box.transform.SetParent(go.transform, false);
            box.GetComponent<Image>().color = new Color(0.08f, 0.095f, 0.12f, 1f);
            box.GetComponent<LayoutElement>().preferredWidth = 28f;
            var mark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            mark.transform.SetParent(box.transform, false);
            mark.GetComponent<Image>().color = new Color(0.42f, 0.82f, 0.95f, 1f);
            StretchRuntime(mark.GetComponent<RectTransform>(), 7f, 7f, 7f, 7f);
            RuntimeText("Label", go.transform, label, 13, FontStyle.Normal, new Color(0.86f, 0.90f, 0.95f, 1f), 30f);
            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = mark.GetComponent<Image>();
            toggle.isOn = isOn;
            return toggle;
        }

        private static Dropdown RuntimeDropdown(string name, Transform parent, float width)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.082f, 0.105f, 0.138f, 1f);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minHeight = 38f;
            element.preferredHeight = 38f;
            var label = RuntimeText("Label", go.transform, name, 13, FontStyle.Normal, new Color(0.86f, 0.90f, 0.95f, 1f), 30f);
            StretchRuntime(label.rectTransform, 8f, 0f, 26f, 0f);
            var dropdown = go.GetComponent<Dropdown>();
            dropdown.captionText = label;
            var template = RuntimeDropdownTemplate(go.transform);
            dropdown.template = template;
            dropdown.itemText = template.Find("Viewport/Content/Item/Item Label").GetComponent<Text>();
            dropdown.options = new System.Collections.Generic.List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("Calm"),
                new Dropdown.OptionData("Normal"),
                new Dropdown.OptionData("Playful")
            };
            dropdown.value = 1;
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static RectTransform RuntimeDropdownTemplate(Transform parent)
        {
            var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(parent, false);
            template.SetActive(false);
            template.GetComponent<Image>().color = new Color(0.08f, 0.095f, 0.12f, 1f);
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -36f);
            templateRect.sizeDelta = new Vector2(0f, 108f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            viewport.GetComponent<Image>().color = Color.clear;
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            StretchRuntime(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            content.transform.SetParent(viewport.transform, false);
            StretchRuntime(content.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(LayoutElement));
            item.transform.SetParent(content.transform, false);
            item.GetComponent<Image>().color = new Color(0.10f, 0.125f, 0.16f, 1f);
            item.GetComponent<LayoutElement>().preferredHeight = 34f;
            var itemLabel = RuntimeText("Item Label", item.transform, "Option", 13, FontStyle.Normal, Color.white, 32f);
            StretchRuntime(itemLabel.rectTransform, 8f, 0f, 8f, 0f);

            var scroll = template.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            return templateRect;
        }

        private static void StretchRuntime(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private void UpdateResponsiveStartLayout()
        {
            if (startScreenRoot == null)
            {
                return;
            }

            var main = startScreenRoot.transform.Find("Start Main");
            if (main == null)
            {
                return;
            }

            var rect = GetComponent<RectTransform>();
            var availableWidth = rect != null ? rect.rect.width : Screen.width;
            var shouldStack = availableWidth < 1180f;
            var horizontal = main.GetComponent<HorizontalLayoutGroup>();
            var vertical = main.GetComponent<VerticalLayoutGroup>();
            if (shouldStack)
            {
                if (horizontal != null)
                {
                    DestroyLayoutComponent(horizontal);
                    horizontal = null;
                }

                if (vertical == null)
                {
                    vertical = main.gameObject.AddComponent<VerticalLayoutGroup>();
                }

                ConfigureVerticalMainLayout(vertical);
            }
            else
            {
                if (vertical != null)
                {
                    DestroyLayoutComponent(vertical);
                    vertical = null;
                }

                if (horizontal == null)
                {
                    horizontal = main.gameObject.AddComponent<HorizontalLayoutGroup>();
                }

                ConfigureHorizontalMainLayout(horizontal);
            }

            var layoutElement = main.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.minHeight = shouldStack ? 1620f : 870f;
                layoutElement.preferredHeight = shouldStack ? 1680f : 920f;
            }

            var screenElement = startScreenRoot.GetComponent<LayoutElement>();
            if (screenElement != null)
            {
                screenElement.minHeight = shouldStack ? 1880f : 1180f;
                screenElement.preferredHeight = shouldStack ? 1940f : 1240f;
            }
        }

        private static void ConfigureHorizontalMainLayout(HorizontalLayoutGroup layout)
        {
            if (layout == null)
            {
                return;
            }

            layout.spacing = 20f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void ConfigureVerticalMainLayout(VerticalLayoutGroup layout)
        {
            if (layout == null)
            {
                return;
            }

            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void DestroyLayoutComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            DestroyImmediate(component);
        }
    }
}
