using System.IO;
using TokenForge.Client.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.Editor
{
    public static class Phase14UiPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/UI";
        private const float ContentHorizontalInset = 72f;

        [MenuItem("Tools/TokenForge/Build Phase 15 UI Prefabs")]
        public static void BuildPrefabs()
        {
            EnsureFolder();
            SavePanelPrefab("AccountPanel.prefab", CreateAccountPanel());
            SavePanelPrefab("ActivityAnalysisPanel.prefab", CreateActivityAnalysisPanel());
            SavePanelPrefab("ApprovedLocationsPanel.prefab", CreateApprovedLocationsPanel());
            SavePanelPrefab("ReviewPanel.prefab", CreateReviewPanel());
            SavePanelPrefab("SafeSyncPanel.prefab", CreateSafeSyncPanel());
            SavePanelPrefab("RecentSessionsPanel.prefab", CreateRecentSessionsPanel());
            SavePanelPrefab("PrivacyNoticePanel.prefab", CreatePrivacyNoticePanel());
            SavePanelPrefab("ReviewAndRecentSessionsPanel.prefab", CreateReviewAndRecentSessionsPanel());
            SavePanelPrefab("BootstrapRoot.prefab", CreateBootstrapRoot());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject CreateBootstrapRoot()
        {
            var root = new GameObject("BootstrapRoot", typeof(RectTransform), typeof(BootstrapRootView));
            Stretch(root.GetComponent<RectTransform>());
            var rootView = root.GetComponent<BootstrapRootView>();

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            Stretch(background.GetComponent<RectTransform>());
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.035f, 0.047f, 0.063f, 1f);
            backgroundImage.raycastTarget = false;

            var content = CreateRootScroll(root.transform, rootView);
            CreateStartScreen(content, rootView);
            CreateGameDashboard(content, rootView);
            CreateRunAnalysis(content, rootView);
            CreateSettingsAdvanced(content, rootView);
            CreateDeveloperDiagnostics(content, rootView);
            rootView.gameDashboardRoot.SetActive(false);
            rootView.runAnalysisRoot.SetActive(false);
            rootView.settingsAdvancedRoot.SetActive(false);
            rootView.developerDiagnosticsRoot.SetActive(false);
            return root;
        }

        private static Transform CreateRootScroll(Transform parent, BootstrapRootView rootView)
        {
            var scroll = new GameObject("Root Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(BootstrapScrollDiagnostics));
            scroll.transform.SetParent(parent, false);
            Stretch(scroll.GetComponent<RectTransform>());

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scroll.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(ContentHorizontalInset, 0f);
            contentRect.offsetMax = new Vector2(-ContentHorizontalInset, 0f);
            contentRect.anchoredPosition = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 30, 42);
            layout.spacing = 22f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scroll.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 80f;
            rootView.rootScrollRect = scrollRect;
            return content.transform;
        }

        private static void CreateStartScreen(Transform parent, BootstrapRootView rootView)
        {
            var screen = new GameObject("Start Screen Root", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            screen.transform.SetParent(parent, false);
            screen.GetComponent<LayoutElement>().minHeight = 1180f;
            screen.GetComponent<LayoutElement>().preferredHeight = 1240f;
            var layout = screen.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(0, 0, 0, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rootView.startScreenRoot = screen;

            var header = new GameObject("Start Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            header.transform.SetParent(screen.transform, false);
            header.GetComponent<LayoutElement>().preferredHeight = 96f;
            var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;
            headerLayout.spacing = 22f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            var titleStack = new GameObject("Title Stack", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            titleStack.transform.SetParent(header.transform, false);
            var titleElement = titleStack.GetComponent<LayoutElement>();
            titleElement.minWidth = 520f;
            titleElement.flexibleWidth = 1f;
            var titleLayout = titleStack.GetComponent<VerticalLayoutGroup>();
            titleLayout.spacing = 10f;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = true;
            titleLayout.childForceExpandHeight = false;
            NoWrapText("TokenForge Title", titleStack.transform, "TokenForge", 44, FontStyle.Bold, Accent(), 56f, 420f);
            Text("TokenForge Subtitle", titleStack.transform, "Turn local development activity into RPG growth.", 17, FontStyle.Bold, Secondary(), 30f);
            Text("Start Privacy Note", titleStack.transform, "Raw paths, prompts, logs, source code, diffs, and file names stay local.", 13, FontStyle.Bold, Muted(), 24f);
            rootView.loginStatusSmallLabel = StatusText("Auth Chip", header.transform, "Local mode · Sync optional", Secondary(), 42f, 260f);

            var primary = CompactPanel("Start Developer RPG Card", screen.transform, 128f);
            rootView.onboardingPrimaryTitleLabel = Text("Start Developer RPG Title", primary.transform, "TokenForge", 22, FontStyle.Bold, Accent(), 30f);
            Text("Start Game Label", primary.transform, "Start Game enters the local dashboard without login or sync.", 13, FontStyle.Bold, Secondary(), 24f);
            rootView.onboardingStepIndicatorLabel = StatusText("Onboarding Step Indicator", primary.transform, "Start Game  >  Run Analysis  >  Review Safe Summary  >  Save Progress", Primary(), 38f);
            var primaryActions = Row("Start Primary Actions", primary.transform, 48f);
            rootView.startGameButton = Button("Start Game", primaryActions.transform, 150f, ButtonStyle.Primary);
            rootView.startAnalyzeRepositoryButton = Button("Run Analysis", primaryActions.transform, 150f, ButtonStyle.Secondary);
            rootView.startSyncProgressButton = Button("Safe Sync", primaryActions.transform, 120f, ButtonStyle.Secondary);
            rootView.startRunStatusLabel = Text("Start Run Status", primary.transform, "Turn local development activity into RPG growth.", 14, FontStyle.Bold, Muted(), 24f);

            var main = new GameObject("Start Main", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            main.transform.SetParent(screen.transform, false);
            main.GetComponent<LayoutElement>().minHeight = 870f;
            main.GetComponent<LayoutElement>().preferredHeight = 920f;
            var mainLayout = main.GetComponent<HorizontalLayoutGroup>();
            mainLayout.spacing = 20f;
            mainLayout.childControlWidth = true;
            mainLayout.childControlHeight = true;
            mainLayout.childForceExpandWidth = true;
            mainLayout.childForceExpandHeight = false;

            var stepCard = CompactPanel("Onboarding Step Card", main.transform, 790f);
            stepCard.GetComponent<LayoutElement>().flexibleWidth = 1.45f;
            stepCard.GetComponent<LayoutElement>().minWidth = 600f;
            rootView.onboardingAccountStepRoot = OnboardingAccountStep(stepCard.transform, rootView);
            rootView.onboardingAiAgentsStepRoot = OnboardingAgentsStep(stepCard.transform, rootView);
            rootView.onboardingGitStepRoot = OnboardingGitStep(stepCard.transform, rootView);
            rootView.onboardingReadyStepRoot = OnboardingReadyStep(stepCard.transform, rootView);
            var navRow = Row("Onboarding Navigation", stepCard.transform, 48f);
            rootView.onboardingBackButton = Button("Back", navRow.transform, 100f, ButtonStyle.Secondary);
            rootView.onboardingContinueButton = Button("Continue", navRow.transform, 150f, ButtonStyle.Primary);
            rootView.continueButton = rootView.onboardingContinueButton;

            var summaryColumn = new GameObject("Onboarding Summary Column", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            summaryColumn.transform.SetParent(main.transform, false);
            summaryColumn.GetComponent<LayoutElement>().flexibleWidth = 1f;
            summaryColumn.GetComponent<LayoutElement>().minWidth = 330f;
            summaryColumn.GetComponent<LayoutElement>().preferredHeight = 790f;
            var summaryLayout = summaryColumn.GetComponent<VerticalLayoutGroup>();
            summaryLayout.spacing = 12f;
            summaryLayout.childControlWidth = true;
            summaryLayout.childControlHeight = true;
            summaryLayout.childForceExpandWidth = true;
            summaryLayout.childForceExpandHeight = false;
            Text("Summary Card Title", summaryColumn.transform, "Run Summary", 20, FontStyle.Bold, Accent(), 30f);
            rootView.startCharacterLabel = StatusText("Summary Current Step", summaryColumn.transform, "Current step: Account", Primary(), 58f);
            rootView.startRecentSessionsLabel = StatusText("Summary AI Agents", summaryColumn.transform, "AI agents: none selected", Secondary(), 82f);
            rootView.startStatsLabel = StatusText("Summary Git", summaryColumn.transform, "Git: no source selected", Secondary(), 72f);
            rootView.startGrowthLabel = StatusText("Summary Safe Sync", summaryColumn.transform, "Safe Sync: optional", Secondary(), 58f);
            rootView.startPrivacySummaryLabel = StatusText("Summary Privacy", summaryColumn.transform, PrivacySummaryText(), Muted(), 128f);
            var summaryActions = Row("Summary Actions", summaryColumn.transform, 48f);
            Button("Set Up Git", summaryActions.transform, 130f, ButtonStyle.Secondary);
            rootView.startAnalyzeAgentLogsButton = Button("Set Up Agents", summaryActions.transform, 146f, ButtonStyle.Secondary);
            rootView.startReviewAnalysisButton = Button("Save Review", summaryActions.transform, 138f, ButtonStyle.Primary);
            summaryActions.SetActive(false);
            var summarySyncActions = Row("Summary Sync Actions", summaryColumn.transform, 48f);
            rootView.startSaveRunButton = Button("Save Run", summarySyncActions.transform, 120f, ButtonStyle.Primary);
            rootView.startCreateSyncAccountButton = Button("Sync Account", summarySyncActions.transform, 138f, ButtonStyle.Secondary);
            rootView.startLoginButton = Button("Log In", summarySyncActions.transform, 92f, ButtonStyle.Secondary);
            summarySyncActions.SetActive(false);
            var summaryServerActions = Row("Summary Server Actions", summaryColumn.transform, 48f);
            rootView.startCheckServerButton = Button("Check Server", summaryServerActions.transform, 132f, ButtonStyle.Secondary);
            Button("Sync Later", summaryServerActions.transform, 118f, ButtonStyle.Secondary);
            summaryServerActions.SetActive(false);

            rootView.headerStatusLabel = rootView.loginStatusSmallLabel;
            rootView.validationStatusLabel = rootView.startPrivacySummaryLabel;
        }

        private static GameObject OnboardingAccountStep(Transform parent, BootstrapRootView rootView)
        {
            var step = StepContainer("Step 1 Account", parent);
            Text("Account Step Title", step.transform, "Step 1. Account", 22, FontStyle.Bold, Accent(), 34f);
            Text("Account Step Copy", step.transform, "Create an account to save your progress and enable Safe Sync.", 15, FontStyle.Bold, Primary(), 32f);
            rootView.onboardingAccountStatusLabel = StatusText("Account Step Status", step.transform, "Create an account to save your progress and enable Safe Sync.", Secondary(), 42f);
            var fields = Row("Account Step Fields", step.transform, 58f);
            rootView.onboardingEmailInput = InputFieldGroup("Email", fields.transform, string.Empty, 220f);
            rootView.onboardingDisplayNameInput = InputFieldGroup("Display name", fields.transform, string.Empty, 220f);
            rootView.onboardingPasswordInput = InputFieldGroup("Password", fields.transform, string.Empty, 180f, InputField.ContentType.Password);
            var actions = Row("Account Step Actions", step.transform, 48f);
            rootView.onboardingCreateAccountButton = Button("Create Account", actions.transform, 160f, ButtonStyle.Primary);
            rootView.onboardingLoginButton = Button("Log In", actions.transform, 110f, ButtonStyle.Secondary);
            rootView.onboardingContinueOfflineButton = Button("Continue Offline", actions.transform, 170f, ButtonStyle.Secondary);
            Text("Offline Copy", step.transform, "Local-only progress. Sync can be enabled later.", 13, FontStyle.Normal, Muted(), 24f);
            return step;
        }

        private static GameObject OnboardingAgentsStep(Transform parent, BootstrapRootView rootView)
        {
            var step = StepContainer("Step 2 AI Agents", parent);
            Text("AI Agents Step Title", step.transform, "Step 2. AI Agents", 22, FontStyle.Bold, Accent(), 34f);
            Text("AI Agents Step Copy", step.transform, "Select sources for this run. Local detection requires explicit approval before analysis.", 15, FontStyle.Bold, Primary(), 44f);
            rootView.onboardingAiAgentsStatusLabel = StatusText("AI Agents Step Status", step.transform, "No AI agents selected yet.", Secondary(), 40f);
            CreateAgentRow(step.transform, "Cursor", out rootView.cursorAgentToggle, out rootView.cursorAgentStatusLabel, out rootView.cursorAgentConnectButton, "Select");
            CreateAgentRow(step.transform, "Claude Code", out rootView.claudeAgentToggle, out rootView.claudeAgentStatusLabel, out rootView.claudeAgentConnectButton, "Select");
            CreateAgentRow(step.transform, "Codex", out rootView.codexAgentToggle, out rootView.codexAgentStatusLabel, out rootView.codexAgentConnectButton, "Select");
            CreateAgentRow(step.transform, "GitHub Copilot", out rootView.copilotAgentToggle, out rootView.copilotAgentStatusLabel, out rootView.copilotAgentConnectButton, "Select");
            CreateAgentRow(step.transform, "Other / Manual Log Folder", out rootView.otherAgentToggle, out rootView.otherAgentStatusLabel, out rootView.otherAgentSelectLogFolderButton, "Choose Log Folder");
            Text("AI Agents Privacy Copy", step.transform, "Raw log text and raw paths are never shown here.", 13, FontStyle.Normal, Muted(), 24f);
            return step;
        }

        private static GameObject OnboardingGitStep(Transform parent, BootstrapRootView rootView)
        {
            var step = StepContainer("Step 3 Git", parent);
            Text("Git Step Title", step.transform, "Step 3. Git", 22, FontStyle.Bold, Accent(), 34f);
            Text("Git Step Copy", step.transform, "Select a local repository, or skip Git for now.", 15, FontStyle.Bold, Primary(), 32f);
            rootView.onboardingGitStatusLabel = StatusText("Git Step Status", step.transform, "No Git source selected.", Secondary(), 44f);
            var actions = Row("Git Step Actions", step.transform, 52f);
            rootView.onboardingSelectLocalRepositoryButton = Button("Select Local Repository", actions.transform, 210f, ButtonStyle.Primary);
            rootView.onboardingConnectGitAccountButton = Button("Git Account Coming Later", actions.transform, 214f, ButtonStyle.Secondary);
            rootView.onboardingConnectGitAccountButton.gameObject.SetActive(false);
            rootView.onboardingSkipGitButton = Button("Skip for now", actions.transform, 140f, ButtonStyle.Secondary);
            rootView.onboardingClearLocalRepositoryButton = Button("Clear Selection", step.transform, 160f, ButtonStyle.Secondary);
            Text("Git Placeholder Copy", step.transform, "Local repository selection uses a safe alias. Git account connection is not available in this MVP.", 13, FontStyle.Normal, Muted(), 42f);
            return step;
        }

        private static GameObject OnboardingReadyStep(Transform parent, BootstrapRootView rootView)
        {
            var step = StepContainer("Step 4 Ready", parent);
            Text("Ready Step Title", step.transform, "Welcome to TokenForge", 22, FontStyle.Bold, Accent(), 34f);
            Text("Ready Step Copy", step.transform, "Turn local development activity into RPG growth.", 15, FontStyle.Bold, Primary(), 32f);
            rootView.onboardingReadySummaryLabel = StatusText("Ready Summary", step.transform, "No saved run yet.\nStart Game opens the dashboard without login or sync.\nRun Analysis creates a pending safe review.\nSaving the review is the only step that grants XP.", Secondary(), 170f);
            Text("Ready Step Hint", step.transform, "Use Start Game above to enter the dashboard, or Run Analysis to prepare a safe review.", 13, FontStyle.Bold, Muted(), 32f);
            return step;
        }

        private static GameObject StepContainer(string name, Transform parent)
        {
            var step = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            step.transform.SetParent(parent, false);
            var layout = step.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            step.GetComponent<LayoutElement>().preferredHeight = 640f;
            step.GetComponent<LayoutElement>().minHeight = 640f;
            return step;
        }

        private static void CreateAgentRow(Transform parent, string label, out Toggle toggle, out Text status, out Button button, string buttonLabel)
        {
            var row = Row(label + " Agent Row", parent, 48f);
            toggle = Toggle(label, row.transform, false, 210f);
            status = StatusText(label + " Agent Status", row.transform, label == "Other / Manual Log Folder" ? "Needs review" : "Not selected", Secondary(), 42f, 250f);
            button = Button(buttonLabel, row.transform, buttonLabel == "Choose Log Folder" ? 170f : 110f, ButtonStyle.Secondary);
        }

        private static void CreateGameDashboard(Transform parent, BootstrapRootView rootView)
        {
            var screen = new GameObject("Game Dashboard Root", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            screen.transform.SetParent(parent, false);
            screen.GetComponent<LayoutElement>().minHeight = 920f;
            screen.GetComponent<LayoutElement>().preferredHeight = 980f;
            var layout = screen.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rootView.gameDashboardRoot = screen;

            var top = new GameObject("Dashboard Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            top.transform.SetParent(screen.transform, false);
            top.GetComponent<LayoutElement>().preferredHeight = 60f;
            var topLayout = top.GetComponent<HorizontalLayoutGroup>();
            topLayout.spacing = 14f;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = false;
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            var dashboardTitle = Text("Dashboard Title", top.transform, "Game Dashboard", 30, FontStyle.Bold, Accent(), 52f);
            dashboardTitle.GetComponent<LayoutElement>().flexibleWidth = 1f;
            rootView.dashboardBackButton = Button("Run Analysis", top.transform, 146f, ButtonStyle.Secondary);

            rootView.conflictBanner = StatusText("Conflict Banner", screen.transform, "Conflicts: none unresolved.", new Color(1f, 0.62f, 0.34f, 1f), 42f).transform.parent.gameObject;
            rootView.conflictBannerLabel = rootView.conflictBanner.GetComponentInChildren<Text>();

            var grid = new GameObject("Dashboard Grid", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            grid.transform.SetParent(screen.transform, false);
            grid.GetComponent<LayoutElement>().flexibleHeight = 1f;
            grid.GetComponent<LayoutElement>().minHeight = 760f;
            var gridLayout = grid.GetComponent<HorizontalLayoutGroup>();
            gridLayout.spacing = 16f;
            gridLayout.childControlWidth = true;
            gridLayout.childControlHeight = true;
            gridLayout.childForceExpandWidth = true;
            var status = Panel("Character Status", grid.transform, 760f);
            status.GetComponent<LayoutElement>().flexibleWidth = 1f;
            status.GetComponent<LayoutElement>().minWidth = 430f;
            Text("Character Status Title", status.transform, "Companion", 20, FontStyle.Bold, Accent(), 30f);
            rootView.dashboardCharacterStatusLabel = StatusText("Dashboard Character Text", status.transform, "Local Player\nLevel 1 | Local Apprentice\nXP 0 / 1000\nToday's Growth: No growth recorded yet.", Primary(), 104f);
            rootView.companionView = CreateCompanionView(status.transform);
            rootView.companionStatusPanel = CreateCompanionStatus(status.transform);
            rootView.dashboardDesktopCompanionLabel = StatusText("Dashboard Desktop Companion", status.transform, "Desktop Companion\nState: disabled\nMode: Normal\nInteraction: Click-through", Secondary(), 108f);
            var desktopActions = Row("Dashboard Desktop Companion Actions", status.transform, 48f);
            rootView.dashboardEnableDesktopCompanionButton = Button("Enable Desktop Companion", desktopActions.transform, 230f, ButtonStyle.Primary);
            rootView.dashboardDisableDesktopCompanionButton = Button("Disable Desktop Companion", desktopActions.transform, 230f, ButtonStyle.Secondary);
            rootView.dashboardQuestLabel = StatusText("Dashboard Stats", status.transform, "Stats\nCode 0\nFocus 0\nDebug 0\nDesign 0\nSync 0", Secondary(), 112f);
            var actions = Row("Dashboard Action Buttons", status.transform, 92f);
            rootView.dashboardAnalyzeRepositoryButton = Button("Analyze Local Sources", actions.transform, 226f, ButtonStyle.Primary);
            rootView.dashboardSaveSessionButton = Button("Save Review", actions.transform, 140f, ButtonStyle.Secondary);
            rootView.dashboardSyncButton = Button("Sync Later", actions.transform, 128f, ButtonStyle.Secondary);

            var log = Panel("Activity Log", grid.transform, 760f);
            log.GetComponent<LayoutElement>().flexibleWidth = 1f;
            log.GetComponent<LayoutElement>().minWidth = 430f;
            Text("Activity Log Title", log.transform, "Today's Growth", 20, FontStyle.Bold, Accent(), 30f);
            rootView.dashboardActivityLogLabel = StatusText("Activity Log Summary", log.transform, "Local Sources\nAI Agents: 0\nGit: No source selected\n\nNext Action\nReady for your first run.", Secondary(), 150f);
            rootView.dashboardSyncReasonLabel = StatusText("Dashboard Sync Reason", log.transform, "Create an account only if you want Safe Sync. Local gameplay works offline.", Secondary(), 72f);
            var logActions = Row("Dashboard Secondary Actions", log.transform, 48f);
            rootView.dashboardHistoryButton = Button("Run Analysis", logActions.transform, 140f, ButtonStyle.Secondary);
            rootView.dashboardSettingsButton = Button("Settings", logActions.transform, 110f, ButtonStyle.Secondary);
        }

        private static CompanionView CreateCompanionView(Transform parent)
        {
            var area = new GameObject("Companion Movement Area", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(CompanionView), typeof(CompanionMovementController));
            area.transform.SetParent(parent, false);
            area.GetComponent<Image>().color = new Color(0.073f, 0.093f, 0.123f, 1f);
            area.GetComponent<Image>().raycastTarget = false;
            var element = area.GetComponent<LayoutElement>();
            element.minHeight = 172f;
            element.preferredHeight = 172f;

            var actor = new GameObject("Companion Actor", typeof(RectTransform));
            actor.transform.SetParent(area.transform, false);
            var actorRect = actor.GetComponent<RectTransform>();
            actorRect.anchorMin = new Vector2(0.5f, 0.5f);
            actorRect.anchorMax = new Vector2(0.5f, 0.5f);
            actorRect.pivot = new Vector2(0.5f, 0.5f);
            actorRect.sizeDelta = new Vector2(72f, 72f);
            actorRect.anchoredPosition = Vector2.zero;

            var body = new GameObject("Companion Body", typeof(RectTransform), typeof(Image));
            body.transform.SetParent(actor.transform, false);
            Stretch(body.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            var bodyImage = body.GetComponent<Image>();
            bodyImage.color = new Color(0.96f, 0.88f, 0.70f, 1f);
            bodyImage.raycastTarget = false;

            var marker = Text("Companion Marker", actor.transform, string.Empty, 13, FontStyle.Bold, new Color(0.035f, 0.047f, 0.063f, 1f), 32f);
            marker.alignment = TextAnchor.MiddleCenter;
            marker.gameObject.SetActive(false);
            Stretch(marker.rectTransform, 4f, 12f, 4f, 20f);

            var stage = Text("Companion Stage Label", area.transform, "Egg | no approved growth yet", 13, FontStyle.Bold, Secondary(), 28f);
            stage.alignment = TextAnchor.LowerCenter;
            var stageRect = stage.GetComponent<RectTransform>();
            stageRect.anchorMin = new Vector2(0f, 0f);
            stageRect.anchorMax = new Vector2(1f, 0f);
            stageRect.pivot = new Vector2(0.5f, 0f);
            stageRect.offsetMin = new Vector2(8f, 4f);
            stageRect.offsetMax = new Vector2(-8f, 32f);

            var view = area.GetComponent<CompanionView>();
            var movement = area.GetComponent<CompanionMovementController>();
            view.movementArea = area.GetComponent<RectTransform>();
            view.characterRoot = actorRect;
            view.bodyImage = bodyImage;
            view.stageLabel = stage;
            view.markerLabel = marker;
            view.movementController = movement;
            movement.movementArea = view.movementArea;
            movement.targetRoot = actorRect;
            movement.bodyImage = bodyImage;
            return view;
        }

        private static CompanionStatusPanelView CreateCompanionStatus(Transform parent)
        {
            var label = StatusText("Companion Status", parent, "Companion Status\nStage: Egg\nArchetype: Not assigned\nLevel: 1\nXP / next: 0 / 250\nNo approved growth yet.", Primary(), 124f);
            var panel = label.GetComponentInParent<Image>().gameObject;
            var view = panel.AddComponent<CompanionStatusPanelView>();
            view.statusLabel = label;
            return view;
        }

        private static void CreateSettingsAdvanced(Transform parent, BootstrapRootView rootView)
        {
            var settings = new GameObject("Settings Advanced Root", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            settings.transform.SetParent(parent, false);
            settings.GetComponent<LayoutElement>().minHeight = 1220f;
            settings.GetComponent<LayoutElement>().preferredHeight = 1280f;
            var settingsLayout = settings.GetComponent<VerticalLayoutGroup>();
            settingsLayout.spacing = 12f;
            settingsLayout.padding = new RectOffset(0, 0, 0, 0);
            settingsLayout.childControlWidth = true;
            settingsLayout.childControlHeight = true;
            settingsLayout.childForceExpandWidth = true;
            settingsLayout.childForceExpandHeight = false;
            rootView.settingsAdvancedRoot = settings;

            var nav = new GameObject("Settings Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            nav.transform.SetParent(settings.transform, false);
            nav.GetComponent<LayoutElement>().preferredHeight = 58f;
            var navLayout = nav.GetComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 14f;
            navLayout.childControlWidth = true;
            navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = false;
            navLayout.childForceExpandHeight = false;
            navLayout.childAlignment = TextAnchor.MiddleLeft;
            var settingsTitle = Text("Settings Title", nav.transform, "Settings", 26, FontStyle.Bold, Accent(), 46f);
            settingsTitle.GetComponent<LayoutElement>().flexibleWidth = 1f;
            rootView.settingsBackButton = Button("Back to Game", nav.transform, 150f, ButtonStyle.Secondary);

            rootView.accountPanel = CreateAccountPanel().GetComponent<AccountPanelView>();
            rootView.accountPanel.transform.SetParent(settings.transform, false);
            rootView.privacyNoticePanel = CreatePrivacyNoticePanel().GetComponent<PrivacyNoticePanelView>();
            rootView.privacyNoticePanel.transform.SetParent(settings.transform, false);
            var desktopCompanion = CompactPanel("Settings Desktop Companion", settings.transform, 260f);
            Text("Settings Desktop Companion Title", desktopCompanion.transform, "Desktop Companion", 22, FontStyle.Bold, Accent(), 32f);
            rootView.settingsDesktopCompanionStatusLabel = StatusText("Settings Desktop Companion Status", desktopCompanion.transform, "Overlay state: disabled\nMotion mode: Normal\nClick-through: enabled", Primary(), 86f);
            var desktopToggles = Row("Settings Desktop Companion Toggles", desktopCompanion.transform, 44f);
            rootView.settingsDesktopCompanionEnabledToggle = Toggle("Enable desktop overlay", desktopToggles.transform, false, 230f);
            rootView.settingsDesktopCompanionClickThroughToggle = Toggle("Click-through mode", desktopToggles.transform, true, 220f);
            var desktopOptions = Row("Settings Desktop Companion Options", desktopCompanion.transform, 48f);
            rootView.settingsDesktopCompanionMotionModeDropdown = Dropdown("Motion Mode", desktopOptions.transform, 180f);
            rootView.settingsDesktopCompanionMotionModeDropdown.options = new System.Collections.Generic.List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("Calm"),
                new Dropdown.OptionData("Normal"),
                new Dropdown.OptionData("Playful")
            };
            rootView.settingsDesktopCompanionMotionModeDropdown.value = 1;
            rootView.settingsDesktopCompanionMotionModeDropdown.RefreshShownValue();
            rootView.settingsResetOverlayPositionButton = Button("Reset Overlay Position", desktopOptions.transform, 210f, ButtonStyle.Secondary);
            var localSources = CompactPanel("Settings Local Sources", settings.transform, 240f);
            Text("Settings Local Sources Title", localSources.transform, "Local Sources", 22, FontStyle.Bold, Accent(), 32f);
            rootView.settingsSummaryLabel = StatusText("Settings Local Sources Text", localSources.transform, "Selected Providers: none\nDetected Local Sources: 0\nReady to Analyze: 0\nManual Folder: Not selected\nGit Alias: No source selected", Primary(), 154f);
            var syncConflict = CompactPanel("Settings Sync Conflict", settings.transform, 210f);
            Text("Settings Sync Conflict Title", syncConflict.transform, "Sync / Conflict", 22, FontStyle.Bold, Accent(), 32f);
            rootView.settingsSyncConflictLabel = StatusText("Settings Sync Conflict Text", syncConflict.transform, "Sync State: Idle\nPending uploads: 0\nPending deletes / tombstones: 0\nConflicts: 0\nResolve conflict CTA: Hidden until a real conflict exists.", Primary(), 130f);
            rootView.settingsDeveloperDiagnosticsButton = Button("Advanced / Developer Diagnostics", syncConflict.transform, 260f, ButtonStyle.Secondary);
        }

        private static void CreateRunAnalysis(Transform parent, BootstrapRootView rootView)
        {
            var screen = new GameObject("Run Analysis Root", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            screen.transform.SetParent(parent, false);
            screen.GetComponent<LayoutElement>().minHeight = 920f;
            screen.GetComponent<LayoutElement>().preferredHeight = 980f;
            var layout = screen.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rootView.runAnalysisRoot = screen;

            var header = new GameObject("Run Analysis Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            header.transform.SetParent(screen.transform, false);
            header.GetComponent<LayoutElement>().preferredHeight = 58f;
            var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 14f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            rootView.runAnalysisTitleLabel = Text("Run Analysis Title", header.transform, "Run Analysis", 28, FontStyle.Bold, Accent(), 48f);
            rootView.runAnalysisTitleLabel.GetComponent<LayoutElement>().flexibleWidth = 1f;
            rootView.runAnalysisBackButton = Button("Dashboard", header.transform, 128f, ButtonStyle.Secondary);
            rootView.runAnalysisSettingsButton = Button("Settings", header.transform, 116f, ButtonStyle.Secondary);
            StatusText("Run Analysis Steps", screen.transform, "1. Select Source  >  2. Analyze  >  3. Review Safe Summary  >  4. Save Progress", Primary(), 42f);

            var grid = new GameObject("Run Analysis Grid", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            grid.transform.SetParent(screen.transform, false);
            grid.GetComponent<LayoutElement>().flexibleHeight = 1f;
            grid.GetComponent<LayoutElement>().minHeight = 760f;
            var gridLayout = grid.GetComponent<HorizontalLayoutGroup>();
            gridLayout.spacing = 16f;
            gridLayout.childControlWidth = true;
            gridLayout.childControlHeight = true;
            gridLayout.childForceExpandWidth = true;

            rootView.activityAnalysisPanel = CreateActivityAnalysisPanel().GetComponent<ActivityAnalysisPanelView>();
            rootView.activityAnalysisPanel.transform.SetParent(grid.transform, false);
            rootView.activityAnalysisPanel.GetComponent<LayoutElement>().flexibleWidth = 1f;
            rootView.reviewPanel = CreateReviewPanel().GetComponent<ReviewPanelView>();
            rootView.reviewPanel.transform.SetParent(grid.transform, false);
            rootView.reviewPanel.GetComponent<LayoutElement>().preferredHeight = 700f;
            rootView.reviewPanel.GetComponent<LayoutElement>().minHeight = 700f;
            rootView.reviewPanel.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private static void CreateDeveloperDiagnostics(Transform parent, BootstrapRootView rootView)
        {
            var diagnostics = new GameObject("Developer Diagnostics Root", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            diagnostics.transform.SetParent(parent, false);
            diagnostics.GetComponent<LayoutElement>().minHeight = 2500f;
            diagnostics.GetComponent<LayoutElement>().preferredHeight = 2600f;
            var diagnosticsLayout = diagnostics.GetComponent<VerticalLayoutGroup>();
            diagnosticsLayout.spacing = 12f;
            diagnosticsLayout.padding = new RectOffset(0, 0, 0, 0);
            diagnosticsLayout.childControlWidth = true;
            diagnosticsLayout.childControlHeight = true;
            diagnosticsLayout.childForceExpandWidth = true;
            diagnosticsLayout.childForceExpandHeight = false;
            rootView.developerDiagnosticsRoot = diagnostics;

            var nav = new GameObject("Developer Diagnostics Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            nav.transform.SetParent(diagnostics.transform, false);
            nav.GetComponent<LayoutElement>().preferredHeight = 58f;
            var navLayout = nav.GetComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 14f;
            navLayout.childControlWidth = true;
            navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = false;
            navLayout.childForceExpandHeight = false;
            navLayout.childAlignment = TextAnchor.MiddleLeft;
            var title = Text("Developer Diagnostics Title", nav.transform, "Developer Diagnostics", 26, FontStyle.Bold, Accent(), 46f);
            title.GetComponent<LayoutElement>().flexibleWidth = 1f;
            rootView.diagnosticsBackButton = Button("Back to Settings", nav.transform, 170f, ButtonStyle.Secondary);

            rootView.approvedLocationsPanel = CreateApprovedLocationsPanel().GetComponent<ApprovedLocationsPanelView>();
            rootView.approvedLocationsPanel.transform.SetParent(diagnostics.transform, false);
            rootView.recentSessionsPanel = CreateRecentSessionsPanel().GetComponent<RecentSessionsPanelView>();
            rootView.recentSessionsPanel.transform.SetParent(diagnostics.transform, false);
            rootView.safeSyncPanel = CreateSafeSyncPanel().GetComponent<SafeSyncPanelView>();
            rootView.safeSyncPanel.transform.SetParent(diagnostics.transform, false);
        }

        private static GameObject CreateAccountPanel()
        {
            var panel = Panel("AccountPanel", null, 430f);
            var view = panel.AddComponent<AccountPanelView>();
            Title(panel, "Sync Account");
            Text("Account Helper", panel.transform, "Optional. Log in only when you want server sync.", 15, FontStyle.Bold, Primary(), 30f);
            view.statusLabel = StatusText("Account Status", panel.transform, "Logged out. Local analysis still works offline.", Secondary(), 44f);
            Text("Account Disabled Help", panel.transform, "No login starts automatically. Passwords are masked and cleared after submit.", 13, FontStyle.Normal, Muted(), 24f);
            var serverRow = Row("Account Server Fields", panel.transform, 54f);
            view.baseUrlInput = InputFieldGroup("Server base URL", serverRow.transform, "http://localhost:3000/api/v1", 360f);
            view.emailInput = InputFieldGroup("Email", serverRow.transform, string.Empty, 280f);
            var profileRow = Row("Account Profile Fields", panel.transform, 54f);
            view.displayNameInput = InputFieldGroup("Display name", profileRow.transform, string.Empty, 280f);
            view.passwordInput = InputFieldGroup("Password", profileRow.transform, string.Empty, 240f, InputField.ContentType.Password);
            var buttonRow = Row("Account Actions", panel.transform, 44f);
            view.loginButton = Button("Log In for Safe Sync", buttonRow.transform, 178f, ButtonStyle.Primary);
            view.signupButton = Button("Create Sync Account", buttonRow.transform, 178f, ButtonStyle.Secondary);
            view.logoutButton = Button("Log Out of Safe Sync", buttonRow.transform, 178f, ButtonStyle.Secondary);
            view.refreshUserButton = Button("Refresh Account Status", buttonRow.transform, 198f, ButtonStyle.Secondary);
            return panel;
        }

        private static GameObject CreateActivityAnalysisPanel()
        {
            var panel = Panel("ActivityAnalysisPanel", null, 700f);
            var view = panel.AddComponent<ActivityAnalysisPanelView>();
            Title(panel, "Run Analysis");
            Text("Local Analysis Helper", panel.transform, "Analyze safe local aggregates, then review before saving. Automatic account connection is not supported.", 15, FontStyle.Bold, Primary(), 42f);
            Text("Git Activity Title", panel.transform, "Git Activity", 16, FontStyle.Bold, Accent(), 24f);
            view.gitStatusLabel = StatusText("Git Status", panel.transform, "No Git source selected.", Secondary(), 52f);
            var gitInputRow = Row("Git Input Controls", panel.transform, 58f);
            view.gitWindowDaysInput = InputFieldGroup("Git window days", gitInputRow.transform, "7", 150f);
            view.gitMaxCommitsInput = InputFieldGroup("Max Git commits", gitInputRow.transform, "200", 170f);
            var gitOptionRow = Row("Git Option Controls", panel.transform, 48f);
            view.gitIncludeUncommittedToggle = Toggle("Uncommitted", gitOptionRow.transform, true, 170f);
            view.gitIncludeCommitsToggle = Toggle("Commits", gitOptionRow.transform, true, 132f);
            view.selectGitButton = Button("Select Repository", gitOptionRow.transform, 176f, ButtonStyle.Secondary);
            view.analyzeGitButton = Button("Analyze Git", gitOptionRow.transform, 146f, ButtonStyle.Primary);
            Text("Git Disabled Help", panel.transform, "Select a repository before analyzing. The UI shows only safe aliases such as Local Repository 1.", 13, FontStyle.Normal, Muted(), 30f);
            Text("AI Agent Activity Title", panel.transform, "AI Agent Activity", 16, FontStyle.Bold, Accent(), 24f);
            view.agentStatusLabel = StatusText("Agent Status", panel.transform, "No AI agents selected yet.", Secondary(), 52f);
            var agentInputRow = Row("Agent Input Controls", panel.transform, 58f);
            view.agentProviderDropdown = DropdownGroup("Agent provider", agentInputRow.transform, 180f);
            view.agentWindowDaysInput = InputFieldGroup("Agent window days", agentInputRow.transform, "7", 156f);
            view.agentMaxFilesInput = InputFieldGroup("Max files", agentInputRow.transform, "500", 132f);
            view.agentMaxEntriesInput = InputFieldGroup("Max entries", agentInputRow.transform, "5000", 142f);
            var agentActionRow = Row("Agent Action Controls", panel.transform, 48f);
            view.selectAgentButton = Button("Select Agent Log", agentActionRow.transform, 166f, ButtonStyle.Secondary);
            view.analyzeAgentButton = Button("Analyze Agent", agentActionRow.transform, 154f, ButtonStyle.Primary);
            Text("Agent Disabled Help", panel.transform, "Local source detection or manual import only. Raw log text is never rendered. Only aggregate review data reaches the save step.", 13, FontStyle.Normal, Muted(), 36f);
            return panel;
        }

        private static GameObject CreateApprovedLocationsPanel()
        {
            var panel = Panel("ApprovedLocationsPanel", null, 540f);
            var view = panel.AddComponent<ApprovedLocationsPanelView>();
            Title(panel, "Approved Local Locations");
            Text("Approved Locations Helper", panel.transform, "Approvals are local-only safety gates. Aliases may be shown; raw paths stay hidden.", 15, FontStyle.Bold, Primary(), 36f);
            view.statusLabel = StatusText("Approved Locations Status", panel.transform, ApprovedActivityAnalysisViewModel.ApprovedLocationsNotice + " No approved locations yet. Add one to reuse it without exposing the raw path.", Primary(), 60f);
            var gitRow = Row("Git Approved Controls", panel.transform, 58f);
            view.gitAliasInput = InputFieldGroup("Git alias", gitRow.transform, "Git repository", 170f);
            view.gitLocationsDropdown = DropdownGroup("Approved Git locations", gitRow.transform, 240f);
            view.approveGitButton = Button("Approve Git Location", gitRow.transform, 174f, ButtonStyle.Primary);
            view.useGitButton = Button("Use Git Location", gitRow.transform, 152f, ButtonStyle.Secondary);
            var gitManageRow = Row("Git Approved Management", panel.transform, 44f);
            view.disableGitButton = Button("Disable Git Location", gitManageRow.transform, 168f, ButtonStyle.Secondary);
            view.removeGitButton = Button("Remove Git Location", gitManageRow.transform, 164f, ButtonStyle.Danger);
            var agentRow = Row("Agent Approved Controls", panel.transform, 58f);
            view.agentAliasInput = InputFieldGroup("Agent alias", agentRow.transform, "Agent logs", 170f);
            view.agentLocationsDropdown = DropdownGroup("Approved agent locations", agentRow.transform, 250f);
            view.approveAgentButton = Button("Approve Agent Location", agentRow.transform, 190f, ButtonStyle.Primary);
            view.useAgentButton = Button("Use Agent Location", agentRow.transform, 166f, ButtonStyle.Secondary);
            var agentManageRow = Row("Agent Approved Management", panel.transform, 44f);
            view.disableAgentButton = Button("Disable Agent Location", agentManageRow.transform, 184f, ButtonStyle.Secondary);
            view.removeAgentButton = Button("Remove Agent Location", agentManageRow.transform, 180f, ButtonStyle.Danger);
            Text("Approved Locations Empty Help", panel.transform, "Use approve for new trusted folders. Disable or remove entries from this panel at any time.", 13, FontStyle.Normal, Muted(), 24f);
            return panel;
        }

        private static GameObject CreateReviewPanel()
        {
            var panel = Panel("ReviewPanel", null, 324f);
            var view = panel.AddComponent<ReviewPanelView>();
            Title(panel, "Review Safe Summary");
            view.noticeLabel = StatusText("Review Notice", panel.transform, "Only safe aggregate data can be saved. Raw paths and source content stay local.", Primary(), 48f);
            view.reviewLabel = StatusText("Review Summary", panel.transform, "No review yet. Run a local analysis first.", Secondary(), 58f);
            Text("Review Action Help", panel.transform, "Save Progress writes the approved aggregate locally. Discard leaves local data unchanged.", 13, FontStyle.Normal, Muted(), 24f);
            var row = Row("Review Controls", panel.transform, 44f);
            view.saveGitButton = Button("Save Git Review", row.transform, 146f, ButtonStyle.Primary);
            view.discardGitButton = Button("Discard Git Review", row.transform, 164f, ButtonStyle.Secondary);
            view.saveAgentButton = Button("Save Agent Review", row.transform, 160f, ButtonStyle.Primary);
            view.discardAgentButton = Button("Discard Agent Review", row.transform, 180f, ButtonStyle.Secondary);
            return panel;
        }

        private static GameObject CreateSafeSyncPanel()
        {
            var panel = Panel("SafeSyncPanel", null, 1370f);
            var view = panel.AddComponent<SafeSyncPanelView>();
            Title(panel, "Safe Sync");
            view.noticeLabel = StatusText("Safe Sync Notice", panel.transform, "Optional server sync for saved aggregate summaries only.", Primary(), 48f);
            view.statusLabel = StatusText("Safe Sync Status", panel.transform, "Not connected. Local analysis is still available.", Secondary(), 72f);
            Text("Safe Sync Disabled Help", panel.transform, "Log in to enable server sync. Check Server is always explicit.", 13, FontStyle.Normal, Muted(), 24f);
            var row = Row("Safe Sync Controls", panel.transform, 58f);
            view.baseUrlInput = InputFieldGroup("Server base URL", row.transform, "http://localhost:3000/api/v1", 360f);
            view.healthButton = Button("Check Server", row.transform, 134f, ButtonStyle.Secondary);
            view.syncButton = Button("Sync Safe Sessions", row.transform, 180f, ButtonStyle.Primary);
            var remoteRow = Row("Safe Sync Remote Controls", panel.transform, 58f);
            view.fetchButton = Button("Fetch Remote", remoteRow.transform, 144f, ButtonStyle.Secondary);
            view.remoteSessionsDropdown = DropdownGroup("Remote sessions", remoteRow.transform, 320f);
            view.deleteRemoteButton = Button("Delete Remote Session", remoteRow.transform, 202f, ButtonStyle.Danger);

            var advanced = SubPanel("Advanced Safe Sync Diagnostics", panel.transform, 940f);
            Text("Advanced Safe Sync Title", advanced.transform, "Advanced Safe Sync Diagnostics", 18, FontStyle.Bold, Accent(), 30f);
            Text("Advanced Safe Sync Helper", advanced.transform, "Retry queues, delete tombstones, and conflict resolution are operational controls for saved safe sessions.", 13, FontStyle.Normal, Muted(), 34f);
            view.retryQueueLabel = StatusText("Retry Queue Status", advanced.transform, "Retry Queue: pending 0 | failed 0 | paused 0 | succeeded 0", Secondary(), 60f);
            view.conflictLabel = StatusText("Conflict Status", advanced.transform, "Conflicts: none unresolved.", Secondary(), 174f);
            view.tombstoneLabel = StatusText("Tombstone Status", advanced.transform, "Deletes: pending 0 | synced 0 | failed 0", Secondary(), 58f);
            var retryRow = Row("Retry Queue Controls", advanced.transform, 58f);
            view.retryEntriesDropdown = DropdownGroup("Retry entries", retryRow.transform, 300f);
            view.retryPendingButton = Button("Process Eligible Retries", retryRow.transform, 202f, ButtonStyle.Secondary);
            view.forceRetryButton = Button("Force Retry Selected", retryRow.transform, 190f, ButtonStyle.Secondary);
            var retryManageRow = Row("Retry Queue Management", advanced.transform, 44f);
            view.cancelRetryButton = Button("Cancel Selected Retry", retryManageRow.transform, 188f, ButtonStyle.Danger);
            view.cancelAllFailedRetryButton = Button("Cancel Failed Retries", retryManageRow.transform, 188f, ButtonStyle.Danger);
            view.clearSucceededButton = Button("Clear Succeeded", retryManageRow.transform, 154f, ButtonStyle.Secondary);
            var retryBatchRow = Row("Retry Batch Controls", advanced.transform, 44f);
            view.pauseAllPendingRetryButton = Button("Pause Pending Retries", retryBatchRow.transform, 188f, ButtonStyle.Secondary);
            view.resumeAllPausedRetryButton = Button("Resume Paused Retries", retryBatchRow.transform, 194f, ButtonStyle.Secondary);
            var tombstoneRow = Row("Tombstone Controls", advanced.transform, 58f);
            view.tombstonesDropdown = DropdownGroup("Delete tombstones", tombstoneRow.transform, 300f);
            view.enqueueTombstoneDeletesButton = Button("Enqueue Pending Deletes", tombstoneRow.transform, 214f, ButtonStyle.Secondary);
            view.processTombstoneDeletesButton = Button("Process Deletes Once", tombstoneRow.transform, 194f, ButtonStyle.Secondary);
            var tombstoneManageRow = Row("Tombstone Management Controls", advanced.transform, 44f);
            view.cancelTombstoneButton = Button("Cancel Selected Tombstone", tombstoneManageRow.transform, 224f, ButtonStyle.Danger);
            view.cancelAllFailedTombstonesButton = Button("Cancel Failed Tombstones", tombstoneManageRow.transform, 226f, ButtonStyle.Danger);
            view.clearResolvedTombstonesButton = Button("Clear Resolved Tombstones", tombstoneManageRow.transform, 230f, ButtonStyle.Secondary);
            Text("Conflict Help", advanced.transform, "Conflict decisions use safe aggregate fields only. Keep Local queues re-upload; Keep Remote applies a safe remote aggregate when possible.", 13, FontStyle.Normal, Muted(), 48f);
            var conflictRow = Row("Conflict Controls", advanced.transform, 58f);
            view.conflictsDropdown = DropdownGroup("Conflicts", conflictRow.transform, 300f);
            view.keepLocalButton = Button("Keep Local", conflictRow.transform, 132f, ButtonStyle.Danger);
            view.keepRemoteButton = Button("Keep Remote", conflictRow.transform, 142f, ButtonStyle.Danger);
            view.markConflictResolvedButton = Button("Mark Resolved", conflictRow.transform, 150f, ButtonStyle.Secondary);
            view.applyMergePolicyButton = Button("Apply Merge", conflictRow.transform, 144f, ButtonStyle.Secondary);
            var conflictManageRow = Row("Conflict Management Controls", advanced.transform, 44f);
            view.clearConflictAuditButton = Button("Clear Resolved History", conflictManageRow.transform, 200f, ButtonStyle.Danger);
            view.cancelConflictResolutionButton = Button("Cancel Resolution", conflictManageRow.transform, 180f, ButtonStyle.Secondary);
            return panel;
        }

        private static GameObject CreateRecentSessionsPanel()
        {
            var panel = Panel("RecentSessionsPanel", null, 410f);
            var view = panel.AddComponent<RecentSessionsPanelView>();
            Title(panel, "Recent Sessions");
            Text("Recent Sessions Helper", panel.transform, "Saved local sessions appear here. Remote summaries appear only after explicit fetch.", 14, FontStyle.Bold, Primary(), 36f);
            view.localRecentSessionsLabel = StatusText("Local Recent Sessions", panel.transform, "Local: no saved safe sessions yet. Save an approved aggregate review to populate this list.", Secondary(), 58f);
            view.remoteSessionsLabel = StatusText("Remote Sessions", panel.transform, "Remote: no fetched remote safe sessions. Log in, then explicitly fetch remote summaries.", Secondary(), 58f);
            view.localDeleteStatusLabel = Text("Local Delete Status", panel.transform, "Danger area: deleting a synced local session creates a local tombstone for explicit remote delete sync.", 13, FontStyle.Normal, Muted(), 32f);
            var deleteRow = Row("Local Delete Controls", panel.transform, 58f);
            view.localSessionsDropdown = DropdownGroup("Local sessions", deleteRow.transform, 320f);
            view.deleteLocalSessionButton = Button("Delete Local Session", deleteRow.transform, 190f, ButtonStyle.Danger);
            return panel;
        }

        private static GameObject CreatePrivacyNoticePanel()
        {
            var panel = Panel("PrivacyNoticePanel", null, 220f);
            var view = panel.AddComponent<PrivacyNoticePanelView>();
            Title(panel, "Privacy");
            view.bodyLabel = Text("Privacy Body", panel.transform, "Only saved aggregate summaries can sync. Raw paths and source data stay local.\n\nTokenForge does not render prompts, responses, commands, filenames, repo names, branch names, source snippets, tokens, or payload bodies.", 14, FontStyle.Bold, Primary(), 108f);
            return panel;
        }

        private static GameObject CreateReviewAndRecentSessionsPanel()
        {
            var panel = Panel("ReviewAndRecentSessionsPanel", null, 430f);
            CreateReviewPanel().transform.SetParent(panel.transform, false);
            CreateRecentSessionsPanel().transform.SetParent(panel.transform, false);
            return panel;
        }

        private static GameObject Panel(string name, Transform parent, float preferredHeight)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(Outline));
            if (parent != null)
            {
                panel.transform.SetParent(parent, false);
            }

            panel.GetComponent<Image>().color = new Color(0.055f, 0.071f, 0.096f, 1f);
            panel.GetComponent<Image>().raycastTarget = false;
            var outline = panel.GetComponent<Outline>();
            outline.effectColor = new Color(0.20f, 0.27f, 0.34f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 24, 26);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var element = panel.GetComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;
            return panel;
        }

        private static GameObject CompactPanel(string name, Transform parent, float preferredHeight)
        {
            var panel = Panel(name, parent, preferredHeight);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 8f;
            return panel;
        }

        private static GameObject SubPanel(string name, Transform parent, float preferredHeight)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(Outline));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = new Color(0.046f, 0.060f, 0.082f, 1f);
            panel.GetComponent<Image>().raycastTarget = false;
            var outline = panel.GetComponent<Outline>();
            outline.effectColor = new Color(0.17f, 0.23f, 0.29f, 0.90f);
            outline.effectDistance = new Vector2(1f, -1f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 20);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var element = panel.GetComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;
            return panel;
        }

        private static void Title(GameObject panel, string title)
        {
            Text("Title", panel.transform, title, 22, FontStyle.Bold, Accent(), 32f);
        }

        private static GameObject Row(string name, Transform parent, float height)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = row.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return row;
        }

        private static Text Text(string name, Transform parent, string value, int size, FontStyle style, Color color, float minHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var element = go.GetComponent<LayoutElement>();
            element.minHeight = minHeight;
            element.preferredHeight = minHeight;
            element.flexibleWidth = 1f;
            return text;
        }

        private static Text NoWrapText(string name, Transform parent, string value, int size, FontStyle style, Color color, float minHeight, float minWidth)
        {
            var text = Text(name, parent, value, size, style, color, minHeight);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            var element = text.GetComponent<LayoutElement>();
            element.minWidth = minWidth;
            element.preferredWidth = minWidth;
            element.flexibleWidth = 1f;
            return text;
        }

        private static Text StatusText(string name, Transform parent, string value, Color textColor, float minHeight)
        {
            return StatusText(name, parent, value, textColor, minHeight, 0f);
        }

        private static Text StatusText(string name, Transform parent, string value, Color textColor, float minHeight, float preferredWidth)
        {
            var block = new GameObject(name + " Block", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            block.transform.SetParent(parent, false);
            block.GetComponent<Image>().color = new Color(0.073f, 0.093f, 0.123f, 1f);
            block.GetComponent<Image>().raycastTarget = false;
            var blockElement = block.GetComponent<LayoutElement>();
            blockElement.minHeight = minHeight;
            blockElement.preferredHeight = minHeight;
            if (preferredWidth > 0f)
            {
                blockElement.minWidth = preferredWidth;
                blockElement.preferredWidth = preferredWidth;
            }

            var text = Text(name, block.transform, value, 14, FontStyle.Bold, textColor, minHeight - 6f);
            text.alignment = TextAnchor.MiddleLeft;
            Stretch(text.rectTransform, 10f, 3f, 10f, 3f);
            return text;
        }

        private static InputField InputFieldGroup(string label, Transform parent, string value, float width, InputField.ContentType contentType = InputField.ContentType.Standard)
        {
            var group = FieldGroup(label + " Field", parent, width);
            Text(label + " Label", group.transform, label, 13, FontStyle.Bold, Secondary(), 18f);
            return Input(label, group.transform, value, width, contentType);
        }

        private static Dropdown DropdownGroup(string label, Transform parent, float width)
        {
            var group = FieldGroup(label + " Field", parent, width);
            Text(label + " Label", group.transform, label, 13, FontStyle.Bold, Secondary(), 18f);
            return Dropdown(label, group.transform, width);
        }

        private static GameObject FieldGroup(string name, Transform parent, float width)
        {
            var group = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            group.transform.SetParent(parent, false);
            var layout = group.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var element = group.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minHeight = 54f;
            element.preferredHeight = 54f;
            return group;
        }

        private static InputField Input(string name, Transform parent, string value, float width, InputField.ContentType contentType = InputField.ContentType.Standard)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.082f, 0.105f, 0.138f, 1f);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minHeight = 38f;
            element.preferredHeight = 38f;
            var text = Text("Text", go.transform, value, 14, FontStyle.Normal, Primary(), 32f);
            Stretch(text.rectTransform, 10f, 2f, 10f, 2f);
            var placeholder = Text("Placeholder", go.transform, name, 13, FontStyle.Italic, Placeholder(), 32f);
            Stretch(placeholder.rectTransform, 10f, 2f, 10f, 2f);
            var input = go.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.contentType = contentType;
            input.text = value;
            return input;
        }

        private static Button Button(string label, Transform parent, float width, ButtonStyle style = ButtonStyle.Primary)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var normal = ButtonNormal(style);
            go.GetComponent<Image>().color = normal;
            var colors = go.GetComponent<Button>().colors;
            colors.normalColor = normal;
            colors.highlightedColor = ButtonHighlighted(style);
            colors.pressedColor = ButtonPressed(style);
            colors.disabledColor = new Color(0.30f, 0.34f, 0.39f, 0.90f);
            colors.colorMultiplier = 1f;
            go.GetComponent<Button>().colors = colors;
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minHeight = 42f;
            element.preferredHeight = 42f;
            var text = Text("Label", go.transform, label, 13, FontStyle.Bold, Primary(), 38f);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform, 6f, 0f, 6f, 0f);
            return go.GetComponent<Button>();
        }

        private static Toggle Toggle(string label, Transform parent, bool isOn, float width)
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
            mark.GetComponent<Image>().color = Accent();
            Stretch(mark.GetComponent<RectTransform>(), 7f, 7f, 7f, 7f);
            var labelText = Text("Label", go.transform, label, 13, FontStyle.Normal, Primary(), 30f);
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.resizeTextForBestFit = false;
            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = mark.GetComponent<Image>();
            toggle.isOn = isOn;
            return toggle;
        }

        private static Dropdown Dropdown(string name, Transform parent, float width)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.082f, 0.105f, 0.138f, 1f);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minHeight = 38f;
            element.preferredHeight = 38f;
            var label = Text("Label", go.transform, name, 13, FontStyle.Normal, Primary(), 30f);
            Stretch(label.rectTransform, 8f, 0f, 26f, 0f);
            var arrow = Text("Arrow", go.transform, "v", 13, FontStyle.Bold, Secondary(), 28f);
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(26f, 0f);
            arrow.rectTransform.anchoredPosition = Vector2.zero;
            var template = DropdownTemplate(go.transform);
            var dropdown = go.GetComponent<Dropdown>();
            dropdown.captionText = label;
            dropdown.itemText = template.Find("Viewport/Content/Item/Item Label").GetComponent<Text>();
            dropdown.template = template;
            dropdown.options = new System.Collections.Generic.List<Dropdown.OptionData> { new Dropdown.OptionData(name) };
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static RectTransform DropdownTemplate(Transform parent)
        {
            var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(parent, false);
            template.SetActive(false);
            template.GetComponent<Image>().color = new Color(0.08f, 0.095f, 0.12f, 1f);
            var rect = template.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -36f);
            rect.sizeDelta = new Vector2(0f, 108f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(template.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 108f);
            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            item.transform.SetParent(content.transform, false);
            item.GetComponent<LayoutElement>().preferredHeight = 32f;
            var background = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(item.transform, false);
            background.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.17f, 1f);
            Stretch(background.GetComponent<RectTransform>());
            var itemLabel = Text("Item Label", item.transform, "Option", 13, FontStyle.Normal, Primary(), 28f);
            Stretch(itemLabel.rectTransform, 8f, 0f, 8f, 0f);
            var toggle = item.GetComponent<Toggle>();
            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = background.GetComponent<Image>();
            var scroll = template.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            return rect;
        }

        private static void SavePanelPrefab(string fileName, GameObject root)
        {
            var path = PrefabFolder + "/" + fileName;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
            }
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, 0f, 0f, 0f, 0f);
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Color Accent() => new Color(0.94f, 0.67f, 0.30f, 1f);
        private static Color ButtonNormal(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Secondary:
                    return new Color(0.16f, 0.20f, 0.25f, 1f);
                case ButtonStyle.Danger:
                    return new Color(0.54f, 0.22f, 0.18f, 1f);
                default:
                    return Accent();
            }
        }

        private static Color ButtonHighlighted(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Secondary:
                    return new Color(0.22f, 0.27f, 0.33f, 1f);
                case ButtonStyle.Danger:
                    return new Color(0.68f, 0.28f, 0.22f, 1f);
                default:
                    return new Color(0.98f, 0.72f, 0.36f, 1f);
            }
        }

        private static Color ButtonPressed(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Secondary:
                    return new Color(0.11f, 0.14f, 0.18f, 1f);
                case ButtonStyle.Danger:
                    return new Color(0.40f, 0.16f, 0.13f, 1f);
                default:
                    return new Color(0.76f, 0.48f, 0.2f, 1f);
            }
        }

        private static Color Primary() => new Color(0.92f, 0.94f, 0.96f, 1f);
        private static Color Secondary() => new Color(0.66f, 0.72f, 0.78f, 1f);
        private static Color Muted() => new Color(0.54f, 0.62f, 0.70f, 1f);
        private static Color Placeholder() => new Color(0.72f, 0.78f, 0.84f, 1f);

        private static string PrivacySummaryText()
        {
            return "Privacy-safe aggregate mode. Only safe aggregate metadata is saved. Raw paths and logs are hidden. Raw paths, source code, commit messages, prompts, and agent logs stay local.";
        }

        private enum ButtonStyle
        {
            Primary,
            Secondary,
            Danger
        }
    }
}
