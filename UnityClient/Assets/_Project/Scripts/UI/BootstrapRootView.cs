using System;
using System.Linq;
using System.Threading.Tasks;
using TokenForge.Client.Auth;
using TokenForge.Client.Domain;
using TokenForge.Client.Platform;
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
        [SerializeField] public Text dashboardGrowthStepConnectLabel;
        [SerializeField] public Text dashboardGrowthStepAnalyzeLabel;
        [SerializeField] public Text dashboardGrowthStepReviewLabel;
        [SerializeField] public Text dashboardGrowthStepSaveLabel;
        [SerializeField] public Text dashboardConnectedSourcesLabel;
        [SerializeField] public Text dashboardPendingReviewLabel;
        [SerializeField] public Text dashboardSyncReasonLabel;
        [SerializeField] public Text dashboardDesktopCompanionLabel;
        [SerializeField] public Button dashboardEnableDesktopCompanionButton;
        [SerializeField] public Button dashboardDisableDesktopCompanionButton;
        [SerializeField] public Button dashboardResetDesktopCompanionButton;
        [SerializeField] public Button dashboardOpenDashboardButton;
        [SerializeField] public Text conflictBannerLabel;
        [SerializeField] public Button dashboardAnalyzeRepositoryButton;
        [SerializeField] public Button dashboardSourcesAddRepositoryButton;
        [SerializeField] public Button dashboardSourcesConnectAgentButton;
        [SerializeField] public Button dashboardDiscardReviewButton;
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

        private const string PrivacyCopy = "Local-first. Private details stay on this device; syncing is optional.";

        private LocalClientStatus status;
        private ApprovedActivityAnalysisViewModel viewModel;
        private IApplicationLifecycleService lifecycleService;
        private ScreenMode currentScreen = ScreenMode.Start;

        private enum ScreenMode
        {
            Start,
            Dashboard,
            AddRepository,
            ConnectCodex,
            Settings
        }

        public void Bind(LocalClientStatus status, ApprovedActivityAnalysisViewModel viewModel)
        {
            this.status = status;
            this.viewModel = viewModel;
            lifecycleService = lifecycleService ?? new MacApplicationLifecycleService();
            lifecycleService.MenuActionRequested -= HandleApplicationMenuAction;
            lifecycleService.MenuActionRequested += HandleApplicationMenuAction;

            if (desktopCompanionOverlayController == null)
            {
                desktopCompanionOverlayController = gameObject.AddComponent<DesktopCompanionOverlayController>();
            }

            desktopCompanionOverlayController.Initialize();
            desktopCompanionOverlayController.PositionChanged -= HandleDesktopCompanionPositionChanged;
            desktopCompanionOverlayController.PositionChanged += HandleDesktopCompanionPositionChanged;
            desktopCompanionOverlayController.DashboardRestoreRequested -= HandleDesktopCompanionDashboardRestoreRequested;
            desktopCompanionOverlayController.DashboardRestoreRequested += HandleDesktopCompanionDashboardRestoreRequested;

            WireNavigation();
            Render();
        }

        private void OnDestroy()
        {
            if (lifecycleService != null)
            {
                lifecycleService.MenuActionRequested -= HandleApplicationMenuAction;
            }

            if (desktopCompanionOverlayController != null)
            {
                desktopCompanionOverlayController.PositionChanged -= HandleDesktopCompanionPositionChanged;
                desktopCompanionOverlayController.DashboardRestoreRequested -= HandleDesktopCompanionDashboardRestoreRequested;
            }
        }

        public void Render()
        {
            if (!ValidateReferences(out _))
            {
                return;
            }

            var dashboard = viewModel?.CharacterDashboard ?? new CharacterDashboardSummary();
            var hasPendingReview = viewModel != null && (viewModel.GitFlow.HasPendingReview || viewModel.AgentFlow.HasPendingReview);
            desktopCompanionOverlayController?.ApplySettings(
                dashboard.DesktopCompanionSettings ?? DesktopCompanionSettings.CreateDefault(),
                dashboard.CompanionState ?? CompanionState.CreateDefault());
            SetText(headerStatusLabel, "TokenForge");
            SetText(validationStatusLabel, PrivacyCopy);
            SetText(loginStatusSmallLabel, viewModel != null && viewModel.AuthState == AuthState.LoggedIn ? "Synced" : "Local mode");
            RenderScreens();
            RenderStart(dashboard, hasPendingReview);
            RenderDashboard(dashboard, hasPendingReview);
            RenderAddRepository(dashboard, hasPendingReview);
            RenderConnectCodex();
            RenderSettings(dashboard);
            UpdateNativeStatusItem(dashboard, hasPendingReview);
            RefreshRootScrollContentSize();
        }

        public bool ValidateReferences(out string error)
        {
            error = string.Empty;
            var valid = true;
            valid &= Require(rootScrollRect, nameof(rootScrollRect), ref error);
            valid &= Require(rootScrollRect != null ? rootScrollRect.viewport : null, "rootScrollRect.viewport", ref error);
            valid &= Require(rootScrollRect != null ? rootScrollRect.content : null, "rootScrollRect.content", ref error);
            valid &= Require(startScreenRoot, nameof(startScreenRoot), ref error);
            valid &= Require(gameDashboardRoot, nameof(gameDashboardRoot), ref error);
            valid &= Require(runAnalysisRoot, nameof(runAnalysisRoot), ref error);
            valid &= Require(settingsAdvancedRoot, nameof(settingsAdvancedRoot), ref error);
            valid &= Require(startGameButton, nameof(startGameButton), ref error);
            valid &= Require(startAnalyzeRepositoryButton, nameof(startAnalyzeRepositoryButton), ref error);
            valid &= Require(startAnalyzeAgentLogsButton, nameof(startAnalyzeAgentLogsButton), ref error);
            valid &= Require(dashboardAnalyzeRepositoryButton, nameof(dashboardAnalyzeRepositoryButton), ref error);
            valid &= Require(dashboardSourcesAddRepositoryButton, nameof(dashboardSourcesAddRepositoryButton), ref error);
            valid &= Require(dashboardSourcesConnectAgentButton, nameof(dashboardSourcesConnectAgentButton), ref error);
            valid &= Require(dashboardSaveSessionButton, nameof(dashboardSaveSessionButton), ref error);
            valid &= Require(dashboardSettingsButton, nameof(dashboardSettingsButton), ref error);
            valid &= Require(dashboardBackButton, nameof(dashboardBackButton), ref error);
            valid &= Require(runAnalysisBackButton, nameof(runAnalysisBackButton), ref error);
            valid &= Require(runAnalysisSettingsButton, nameof(runAnalysisSettingsButton), ref error);
            valid &= Require(settingsBackButton, nameof(settingsBackButton), ref error);
            valid &= Require(dashboardCharacterStatusLabel, nameof(dashboardCharacterStatusLabel), ref error);
            valid &= Require(dashboardQuestLabel, nameof(dashboardQuestLabel), ref error);
            valid &= Require(dashboardConnectedSourcesLabel, nameof(dashboardConnectedSourcesLabel), ref error);
            valid &= Require(dashboardPendingReviewLabel, nameof(dashboardPendingReviewLabel), ref error);
            valid &= Require(dashboardActivityLogLabel, nameof(dashboardActivityLogLabel), ref error);
            return valid;
        }

        public void ShowStart()
        {
            currentScreen = ScreenMode.Start;
            Render();
            ResetRootScrollToTop();
        }

        public void ShowDashboard()
        {
            currentScreen = ScreenMode.Dashboard;
            Render();
            ResetRootScrollToTop();
        }

        public void ShowRunAnalysis()
        {
            currentScreen = ScreenMode.AddRepository;
            Render();
            ResetRootScrollToTop();
        }

        public void ShowSettings()
        {
            currentScreen = ScreenMode.Settings;
            Render();
            ResetRootScrollToTop();
        }

        public void ShowDeveloperDiagnostics()
        {
            ShowSettings();
        }

        private void WireNavigation()
        {
            ReplaceClick(startGameButton, ShowDashboard);
            ReplaceClick(continueButton, ShowDashboard);
            ReplaceClick(startAnalyzeRepositoryButton, ShowAddRepository);
            ReplaceClick(startAnalyzeAgentLogsButton, ShowConnectCodex);
            ReplaceClick(startReviewAnalysisButton, ShowAddRepository);
            ReplaceClick(startSaveRunButton, SavePendingActivity);
            ReplaceClick(startCreateSyncAccountButton, ShowSettings);
            ReplaceClick(startLoginButton, ShowSettings);
            ReplaceClick(startCheckServerButton, () => RunViewModelAction(() => viewModel.CheckSyncHealthAsync()));
            ReplaceClick(startSyncProgressButton, ShowSettings);
            ReplaceClick(onboardingCreateAccountButton, ShowSettings);
            ReplaceClick(onboardingLoginButton, ShowSettings);
            ReplaceClick(onboardingContinueOfflineButton, () =>
            {
                viewModel?.ContinueOffline(onboardingDisplayNameInput != null ? onboardingDisplayNameInput.text : string.Empty);
                ShowDashboard();
            });
            ReplaceClick(onboardingBackButton, ShowStart);
            ReplaceClick(onboardingContinueButton, ShowDashboard);
            ReplaceClick(onboardingSelectLocalRepositoryButton, SelectRepository);
            ReplaceClick(onboardingConnectGitAccountButton, SelectRepository);
            ReplaceClick(onboardingSkipGitButton, ShowDashboard);
            ReplaceClick(onboardingClearLocalRepositoryButton, () =>
            {
                viewModel?.ClearGitForOnboarding();
                Render();
            });
            ReplaceClick(cursorAgentConnectButton, () => ToggleAgentSource(ConnectedAgentSourceType.Cursor));
            ReplaceClick(claudeAgentConnectButton, () => ToggleAgentSource(ConnectedAgentSourceType.ClaudeCode));
            ReplaceClick(codexAgentConnectButton, ConnectCodex);
            ReplaceClick(copilotAgentConnectButton, () => ToggleAgentSource(ConnectedAgentSourceType.GitHubCopilot));
            ReplaceClick(otherAgentSelectLogFolderButton, () => RunViewModelAction(() => viewModel.SelectManualAgentLogForOnboardingAsync()));
            ReplaceToggle(cursorAgentToggle, ConnectedAgentSourceType.Cursor);
            ReplaceToggle(claudeAgentToggle, ConnectedAgentSourceType.ClaudeCode);
            ReplaceToggle(codexAgentToggle, ConnectedAgentSourceType.Codex);
            ReplaceToggle(copilotAgentToggle, ConnectedAgentSourceType.GitHubCopilot);
            ReplaceToggle(otherAgentToggle, ConnectedAgentSourceType.OtherManualLogFolder);
            ReplaceClick(dashboardAnalyzeRepositoryButton, ShowAddRepository);
            ReplaceClick(dashboardSourcesAddRepositoryButton, ShowAddRepository);
            ReplaceClick(dashboardSourcesConnectAgentButton, ShowConnectCodex);
            ReplaceClick(dashboardDiscardReviewButton, DiscardPendingActivity);
            ReplaceClick(dashboardSaveSessionButton, SaveOrAnalyze);
            ReplaceClick(dashboardSyncButton, ShowSettings);
            ReplaceClick(dashboardHistoryButton, ShowConnectCodex);
            ReplaceClick(dashboardSettingsButton, ShowSettings);
            ReplaceClick(dashboardBackButton, ShowStart);
            ReplaceClick(settingsBackButton, ShowDashboard);
            ReplaceClick(runAnalysisBackButton, ShowDashboard);
            ReplaceClick(runAnalysisSettingsButton, ShowSettings);
            ReplaceClick(settingsDeveloperDiagnosticsButton, ShowSettings);
            ReplaceClick(diagnosticsBackButton, ShowSettings);
            ReplaceClick(dashboardEnableDesktopCompanionButton, () => RunViewModelAction(() => viewModel.SetDesktopCompanionEnabledAsync(true)));
            ReplaceClick(dashboardDisableDesktopCompanionButton, () => RunViewModelAction(() => viewModel.SetDesktopCompanionEnabledAsync(false)));
            ReplaceClick(dashboardResetDesktopCompanionButton, ResetCompanionPosition);
            ReplaceClick(dashboardOpenDashboardButton, ShowDashboard);
            ReplaceClick(settingsResetOverlayPositionButton, ResetCompanionPosition);
            ReplaceToggle(settingsDesktopCompanionEnabledToggle, enabled => RunViewModelAction(() => viewModel.SetDesktopCompanionEnabledAsync(enabled)));
            ReplaceToggle(settingsDesktopCompanionClickThroughToggle, enabled => RunViewModelAction(() => viewModel.SetDesktopCompanionClickThroughAsync(enabled)));
        }

        private void RenderScreens()
        {
            SetActive(startScreenRoot, currentScreen == ScreenMode.Start);
            SetActive(gameDashboardRoot, currentScreen == ScreenMode.Dashboard);
            SetActive(runAnalysisRoot, currentScreen == ScreenMode.AddRepository || currentScreen == ScreenMode.ConnectCodex);
            SetActive(settingsAdvancedRoot, currentScreen == ScreenMode.Settings);
            SetActive(developerDiagnosticsRoot, false);
            SetActive(conflictBanner, false);
        }

        private void RenderStart(CharacterDashboardSummary dashboard, bool hasPendingReview)
        {
            SetText(onboardingPrimaryTitleLabel, "TokenForge");
            SetText(onboardingStepIndicatorLabel, "Turn your coding activity into a growing desktop companion.");
            SetText(startCharacterLabel, CompanionStatusLine(dashboard) + "\n" + ProgressLine(dashboard));
            SetText(startStatsLabel, "Repository\n" + RepositoryStatus());
            SetText(startGrowthLabel, hasPendingReview ? "Review Activity\nA summary is ready for approval." : "Activity\nAdd a repository or connect an AI agent to begin.");
            SetText(startRecentSessionsLabel, "AI Agents\n" + CodexStatus());
            SetText(startPrivacySummaryLabel, PrivacyCopy);
            SetText(startRunStatusLabel, "Sync optional");
            SetButtonLabel(startGameButton, "Run Analysis");
            SetButtonLabel(startAnalyzeRepositoryButton, "Add Repository");
            SetButtonLabel(startAnalyzeAgentLogsButton, "Connect AI Agent");
            SetButtonLabel(startReviewAnalysisButton, "Review Activity");
            SetButtonLabel(startSyncProgressButton, "Sync optional");
            SetButton(startGameButton, true);
            SetButton(startAnalyzeRepositoryButton, true);
            SetButton(startAnalyzeAgentLogsButton, true);
            SetButton(startReviewAnalysisButton, hasPendingReview);
            SetButton(startSyncProgressButton, true);
        }

        private void RenderDashboard(CharacterDashboardSummary dashboard, bool hasPendingReview)
        {
            SetText(dashboardCharacterStatusLabel, CompanionStatusLine(dashboard) + "\n" + ProgressLine(dashboard));
            companionView?.Render(dashboard.CompanionState, currentScreen != ScreenMode.Dashboard || hasPendingReview);
            SetText(dashboardQuestLabel, "Progress\n" + ProgressLine(dashboard));
            SetText(dashboardActivityLogLabel, "Activity\n" + (dashboard.HasSavedRun ? dashboard.ActivityLogSummary : "No activity yet."));
            SetText(dashboardGrowthStepConnectLabel, "Progress\nRepository: " + RepositoryStatus());
            SetText(dashboardGrowthStepAnalyzeLabel, "Activity\n" + (HasAnalysisSourceReady() ? "Ready to analyze" : "Waiting for a source"));
            SetText(dashboardGrowthStepReviewLabel, "Review Activity\n" + (hasPendingReview ? "Ready" : "Nothing pending"));
            SetText(dashboardGrowthStepSaveLabel, "Approve Growth\n" + (hasPendingReview ? "XP can be applied" : "Locked until review"));
            SetText(dashboardConnectedSourcesLabel, "Repository\n" + RepositoryStatus());
            SetText(dashboardPendingReviewLabel, "AI Agents\n" + CodexStatus());
            SetText(dashboardSyncReasonLabel, "Settings\n" + PrivacyCopy);
            RenderCompanionControls(dashboard);
            SetButtonLabel(dashboardAnalyzeRepositoryButton, "Add Repository");
            SetButtonLabel(dashboardSourcesAddRepositoryButton, "Add Repository");
            SetButtonLabel(dashboardSourcesConnectAgentButton, "Connect AI Agent");
            SetButtonLabel(dashboardSaveSessionButton, hasPendingReview ? "Approve Growth" : "Review Activity");
            SetButtonLabel(dashboardDiscardReviewButton, "Discard");
            SetButtonLabel(dashboardSyncButton, "Sync optional");
            SetButtonLabel(dashboardHistoryButton, "Connect AI Agent");
            SetButtonLabel(dashboardSettingsButton, "Settings");
            SetButtonLabel(dashboardBackButton, "Dashboard");
            SetButton(dashboardAnalyzeRepositoryButton, true);
            SetButton(dashboardSourcesAddRepositoryButton, true);
            SetButton(dashboardSourcesConnectAgentButton, true);
            SetButton(dashboardSaveSessionButton, true);
            SetButton(dashboardDiscardReviewButton, hasPendingReview);
            SetButton(dashboardSyncButton, true);
            SetButton(dashboardHistoryButton, true);
            SetButton(dashboardSettingsButton, true);
            SetButton(dashboardBackButton, true);
        }

        private void RenderAddRepository(CharacterDashboardSummary dashboard, bool hasPendingReview)
        {
            SetText(runAnalysisTitleLabel, currentScreen == ScreenMode.ConnectCodex ? "Connect AI Agent" : "Add Repository");
            SetText(onboardingGitStatusLabel, "Repository\n" + RepositoryStatus() + "\nChoose a local repository, start analysis, then approve the summary.");
            SetText(onboardingReadySummaryLabel, hasPendingReview
                ? "Analysis result\nReview Activity is ready. Approve Growth to apply XP."
                : "Analysis result\nNo approved summary yet.");
            SetText(onboardingAiAgentsStatusLabel, "AI Agents\n" + CodexStatus() + "\nChoose a log folder or detect local activity.");
            SetButtonLabel(onboardingSelectLocalRepositoryButton, "Choose Repository");
            SetButtonLabel(onboardingConnectGitAccountButton, "Start Analysis");
            SetButtonLabel(onboardingSkipGitButton, "Back");
            SetButtonLabel(onboardingClearLocalRepositoryButton, "Clear Repository");
            SetButtonLabel(codexAgentConnectButton, "Connect AI Agent");
            SetButtonLabel(otherAgentSelectLogFolderButton, "Choose Log Folder");
            SetButtonLabel(runAnalysisBackButton, "Back");
            SetButtonLabel(runAnalysisSettingsButton, "Settings");
            SetButton(runAnalysisBackButton, true);
            SetButton(runAnalysisSettingsButton, true);
        }

        private void RenderConnectCodex()
        {
            SetToggle(codexAgentToggle, IsAgentSelected(ConnectedAgentSourceType.Codex));
            SetText(codexAgentStatusLabel, CodexStatus());
        }

        private void RenderSettings(CharacterDashboardSummary dashboard)
        {
            SetText(settingsSummaryLabel, "Settings\n" + PrivacyCopy + "\nRepository: " + RepositoryStatus() + "\nAI Agents: " + CodexStatus());
            SetText(settingsSyncConflictLabel, "Optional sync\n" + (viewModel != null && viewModel.AuthState == AuthState.LoggedIn ? "Connected" : "Local mode"));
            SetText(settingsDesktopCompanionStatusLabel, "Companion window\n" + CompanionOverlayStatus(dashboard));
            SetButtonLabel(settingsBackButton, "Back");
            SetButtonLabel(settingsDeveloperDiagnosticsButton, "Settings");
            SetButtonLabel(settingsResetOverlayPositionButton, "Reset Position");
            if (settingsDesktopCompanionEnabledToggle != null)
            {
                settingsDesktopCompanionEnabledToggle.SetIsOnWithoutNotify(dashboard.DesktopCompanionSettings?.IsDesktopCompanionEnabled ?? false);
            }

            if (settingsDesktopCompanionClickThroughToggle != null)
            {
                settingsDesktopCompanionClickThroughToggle.SetIsOnWithoutNotify(dashboard.DesktopCompanionSettings?.IsClickThroughEnabled ?? false);
            }
        }

        private void RenderCompanionControls(CharacterDashboardSummary dashboard)
        {
            SetText(dashboardDesktopCompanionLabel, "Companion window\n" + CompanionOverlayStatus(dashboard));
            var enabled = dashboard.DesktopCompanionSettings?.IsDesktopCompanionEnabled ?? false;
            SetButtonLabel(dashboardEnableDesktopCompanionButton, "Show Companion");
            SetButtonLabel(dashboardDisableDesktopCompanionButton, "Hide Companion");
            SetButtonLabel(dashboardResetDesktopCompanionButton, "Reset Position");
            SetButtonLabel(dashboardOpenDashboardButton, "Open App");
            SetButton(dashboardEnableDesktopCompanionButton, viewModel != null && !enabled);
            SetButton(dashboardDisableDesktopCompanionButton, viewModel != null && enabled);
            SetButton(dashboardResetDesktopCompanionButton, viewModel != null);
            SetButton(dashboardOpenDashboardButton, true);
        }

        private void ShowAddRepository()
        {
            currentScreen = ScreenMode.AddRepository;
            Render();
            ResetRootScrollToTop();
        }

        private void ShowConnectCodex()
        {
            currentScreen = ScreenMode.ConnectCodex;
            Render();
            ResetRootScrollToTop();
        }

        private void SelectRepository()
        {
            ShowAddRepository();
            RunViewModelAction(() => viewModel.SelectLocalGitRepositoryForOnboardingAsync());
        }

        private void ConnectCodex()
        {
            ShowConnectCodex();
            RunViewModelAction(() => viewModel.DetectAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex));
        }

        private void ToggleAgentSource(ConnectedAgentSourceType sourceType)
        {
            viewModel?.ToggleAgentSourceForOnboarding(sourceType);
            Render();
        }

        private void SaveOrAnalyze()
        {
            if (viewModel == null)
            {
                return;
            }

            if (viewModel.GitFlow.HasPendingReview || viewModel.AgentFlow.HasPendingReview)
            {
                SavePendingActivity();
                return;
            }

            ShowAddRepository();
        }

        private void SavePendingActivity()
        {
            if (viewModel == null)
            {
                return;
            }

            if (viewModel.GitFlow.HasPendingReview)
            {
                RunViewModelAction(() => viewModel.SaveGitSessionAsync());
            }
            else if (viewModel.AgentFlow.HasPendingReview)
            {
                RunViewModelAction(() => viewModel.SaveAgentSessionAsync());
            }
        }

        private void DiscardPendingActivity()
        {
            viewModel?.DiscardGitReview();
            viewModel?.DiscardAgentReview();
            Render();
        }

        private void ResetCompanionPosition()
        {
            desktopCompanionOverlayController?.ResetPosition();
            RunViewModelAction(() => viewModel.ResetDesktopCompanionPositionAsync());
        }

        private string RepositoryStatus()
        {
            if (viewModel == null)
            {
                return "Not selected";
            }

            if (viewModel.Onboarding.GitConnected)
            {
                return string.IsNullOrWhiteSpace(viewModel.Onboarding.GitSafeAlias) ? "Connected" : viewModel.Onboarding.GitSafeAlias;
            }

            return viewModel.Onboarding.GitSkipped ? "Not selected" : "Not selected";
        }

        private string CodexStatus()
        {
            var source = FindAgentSource(ConnectedAgentSourceType.Codex);
            if (source == null || !source.Selected)
            {
                return "Not connected";
            }

            return string.IsNullOrWhiteSpace(source.StatusLabel) ? "Selected" : source.StatusLabel;
        }

        private ConnectedAgentSource FindAgentSource(ConnectedAgentSourceType sourceType)
        {
            return viewModel?.Onboarding.AgentSources.FirstOrDefault(source => source.SourceType == sourceType);
        }

        private bool IsAgentSelected(ConnectedAgentSourceType sourceType)
        {
            return FindAgentSource(sourceType)?.Selected ?? false;
        }

        private bool HasAnalysisSourceReady()
        {
            return viewModel != null && (viewModel.Onboarding.GitConnected || viewModel.Onboarding.AgentSources.Any(IsAgentReady));
        }

        private static string CompanionStatusLine(CharacterDashboardSummary dashboard)
        {
            var state = dashboard?.CompanionState ?? CompanionState.CreateDefault();
            return "Stage: " + state.Stage + "  Level " + state.Level;
        }

        private static string ProgressLine(CharacterDashboardSummary dashboard)
        {
            dashboard = dashboard ?? new CharacterDashboardSummary();
            return "XP " + dashboard.CurrentLevelExp + " / " + Math.Max(1, dashboard.ExpForNextLevel);
        }

        private static string CompanionOverlayStatus(CharacterDashboardSummary dashboard)
        {
            var settings = dashboard?.DesktopCompanionSettings ?? DesktopCompanionSettings.CreateDefault();
            return settings.IsDesktopCompanionEnabled ? "Visible on desktop" : "Hidden";
        }

        private void UpdateNativeStatusItem(CharacterDashboardSummary dashboard, bool hasPendingReview)
        {
            lifecycleService?.UpdateStatusItem(
                string.IsNullOrWhiteSpace(dashboard.CharacterName) ? "Token" : dashboard.CharacterName,
                dashboard.CompanionState.Stage,
                dashboard.CompanionState.Archetype,
                dashboard.CompanionState.Level,
                dashboard.CurrentRepositoryAlias,
                IsAgentReady(FindAgentSource(ConnectedAgentSourceType.Codex)) ? "Codex ready" : "No agent connected",
                viewModel != null && viewModel.AuthState == AuthState.LoggedIn ? "Connected" : "Local only",
                dashboard.DesktopCompanionSettings != null && dashboard.DesktopCompanionSettings.IsDesktopCompanionEnabled,
                dashboard.DesktopCompanionSettings != null && dashboard.DesktopCompanionSettings.IsClickThroughEnabled,
                HasAnalysisSourceReady() || hasPendingReview,
                false);
        }

        private static bool IsAgentReady(ConnectedAgentSource source)
        {
            return source != null &&
                   source.Selected &&
                   (source.State == AgentSourceSetupState.ReadyToAnalyze ||
                    source.State == AgentSourceSetupState.AnalysisComplete ||
                    source.State == AgentSourceSetupState.LocalSourceDetected);
        }

        private void HandleApplicationMenuAction(ApplicationMenuAction action)
        {
            switch (action)
            {
                case ApplicationMenuAction.ShowDashboard:
                    ShowDashboard();
                    lifecycleService?.ShowMainWindow();
                    break;
                case ApplicationMenuAction.HideDashboard:
                    lifecycleService?.HideMainWindow();
                    break;
                case ApplicationMenuAction.EnableDesktopCompanion:
                    RunViewModelAction(() => viewModel.SetDesktopCompanionEnabledAsync(true));
                    break;
                case ApplicationMenuAction.DisableDesktopCompanion:
                    RunViewModelAction(() => viewModel.SetDesktopCompanionEnabledAsync(false));
                    break;
                case ApplicationMenuAction.ToggleCompanionClickThrough:
                    RunViewModelAction(() => viewModel.SetDesktopCompanionClickThroughAsync(!(viewModel.CharacterDashboard?.DesktopCompanionSettings?.IsClickThroughEnabled ?? false)));
                    break;
                case ApplicationMenuAction.ResetCompanionPosition:
                    ResetCompanionPosition();
                    break;
                case ApplicationMenuAction.AddRepository:
                case ApplicationMenuAction.AnalyzeCurrentRepository:
                    ShowAddRepository();
                    lifecycleService?.ShowMainWindow();
                    break;
                case ApplicationMenuAction.ConnectAiAgent:
                    ShowConnectCodex();
                    lifecycleService?.ShowMainWindow();
                    break;
                case ApplicationMenuAction.SyncNow:
                case ApplicationMenuAction.Settings:
                    ShowSettings();
                    lifecycleService?.ShowMainWindow();
                    break;
                case ApplicationMenuAction.Quit:
                    lifecycleService?.Quit();
                    break;
            }
        }

        private void HandleDesktopCompanionPositionChanged(Vector2 position)
        {
            if (viewModel == null)
            {
                return;
            }

            RunViewModelAction(() => viewModel.SaveDesktopCompanionPositionAsync(position.x, position.y));
        }

        private void HandleDesktopCompanionDashboardRestoreRequested()
        {
            ShowDashboard();
            lifecycleService?.ShowMainWindow();
        }

        private async void RunViewModelAction(Func<Task> action)
        {
            if (viewModel == null || action == null)
            {
                return;
            }

            try
            {
                await action();
            }
            finally
            {
                Render();
            }
        }

        private void ReplaceToggle(Toggle toggle, ConnectedAgentSourceType sourceType)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(selected =>
            {
                viewModel?.SetAgentSourceSelected(sourceType, selected);
                Render();
            });
        }

        private void ReplaceToggle(Toggle toggle, Action<bool> action)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(value => action?.Invoke(value));
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

        private static void SetText(Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label ?? string.Empty;
            }
        }

        private static void SetToggle(Toggle toggle, bool value)
        {
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(value);
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
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

        private void ResetRootScrollToTop()
        {
            if (rootScrollRect != null)
            {
                rootScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void RefreshRootScrollContentSize()
        {
            if (rootScrollRect != null && rootScrollRect.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootScrollRect.content);
            }
        }
    }
}
