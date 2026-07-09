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

        [MenuItem("Tools/TokenForge/Build Product UI Prefab")]
        public static void BuildPrefabs()
        {
            EnsureFolder();
            SavePrefab("BootstrapRoot.prefab", CreateBootstrapRoot());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject CreateBootstrapRoot()
        {
            var root = new GameObject("BootstrapRoot", typeof(RectTransform), typeof(BootstrapRootView), typeof(BootstrapRootSourceMarker));
            Stretch(root.GetComponent<RectTransform>());
            var rootView = root.GetComponent<BootstrapRootView>();
            root.GetComponent<BootstrapRootSourceMarker>().SetSource("prefab", BootstrapRootSourceMarker.PrefabPath, BootstrapRootSourceMarker.CurrentUiVersion);

            var background = CreateImageFrame("Background", root.transform, CreamBackground());
            Stretch(background.GetComponent<RectTransform>());
            background.GetComponent<Image>().raycastTarget = false;

            var shell = CreateSoftFrame("WindowShell", root.transform, Shell(), 32f, true);
            var shellRect = shell.GetComponent<RectTransform>();
            shellRect.anchorMin = new Vector2(0.035f, 0.06f);
            shellRect.anchorMax = new Vector2(0.965f, 0.94f);
            shellRect.offsetMin = Vector2.zero;
            shellRect.offsetMax = Vector2.zero;
            AddSoftShadow(shell, new Color(0.22f, 0.18f, 0.12f, 0.16f), new Vector2(0f, -8f));

            var shellLayout = shell.AddComponent<VerticalLayoutGroup>();
            shellLayout.padding = new RectOffset(0, 0, 0, 0);
            shellLayout.spacing = 0f;
            shellLayout.childControlWidth = true;
            shellLayout.childControlHeight = true;
            shellLayout.childForceExpandWidth = true;
            shellLayout.childForceExpandHeight = false;

            CreateTopBar(shell.transform, rootView);
            var body = CreateBody(shell.transform);
            CreateSidebar(body.transform, rootView);
            var mainContent = CreateSoftFrame("MainContent", body.transform, MainSurface(), 0f, false);
            mainContent.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var content = CreateRootScroll(mainContent.transform, rootView);
            CreateStartScreen(content, rootView);
            CreateDashboard(content, rootView);
            CreateAddRepositoryFlow(content, rootView);
            CreateSettings(content, rootView);
            rootView.developerDiagnosticsRoot = CreateScreen("Support Tools Root", content, 360f);
            rootView.developerDiagnosticsRoot.SetActive(false);

            rootView.startScreenRoot.SetActive(true);
            rootView.gameDashboardRoot.SetActive(false);
            rootView.runAnalysisRoot.SetActive(false);
            rootView.settingsAdvancedRoot.SetActive(false);
            return root;
        }

        private static void CreateTopBar(Transform parent, BootstrapRootView rootView)
        {
            var topBar = new GameObject("TopBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            topBar.transform.SetParent(parent, false);
            topBar.GetComponent<LayoutElement>().preferredHeight = 58f;
            var layout = topBar.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 12, 10);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            TrafficLight("Close Dot", topBar.transform, new Color(1f, 0.36f, 0.32f, 1f));
            TrafficLight("Minimize Dot", topBar.transform, new Color(1f, 0.76f, 0.25f, 1f));
            TrafficLight("Zoom Dot", topBar.transform, new Color(0.22f, 0.78f, 0.36f, 1f));
            rootView.headerStatusLabel = Text("TokenForge Title", topBar.transform, "TokenForge", 20, FontStyle.Bold, PrimaryText(), 34f);
            rootView.headerStatusLabel.GetComponent<LayoutElement>().preferredWidth = 300f;

            var spacer = new GameObject("TopBar Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(topBar.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleWidth = 1f;

            rootView.loginStatusSmallLabel = Chip("TopBar Status Pill", topBar.transform, "Local mode", Mint(), 132f);
            Chip("TopBar Sync Pill", topBar.transform, "Sync optional", new Color(0.96f, 0.72f, 0.48f, 1f), 142f);
        }

        private static GameObject CreateBody(Transform parent)
        {
            var body = new GameObject("AppBody", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            body.transform.SetParent(parent, false);
            body.GetComponent<LayoutElement>().flexibleHeight = 1f;
            var layout = body.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return body;
        }

        private static void CreateSidebar(Transform parent, BootstrapRootView rootView)
        {
            var sidebar = CreateSoftFrame("Sidebar", parent, SidebarSurface(), 0f, false);
            var element = sidebar.AddComponent<LayoutElement>();
            element.preferredWidth = 216f;
            element.minWidth = 216f;
            var layout = sidebar.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 20, 18);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var identity = CreateHorizontal("Sidebar Identity", sidebar.transform, 66f, 12f);
            var icon = CreateSoftFrame("Companion App Icon", identity.transform, Coral(), 14f, false);
            icon.AddComponent<LayoutElement>().preferredWidth = 46f;
            Text("Companion Icon Glyph", icon.transform, "TF", 14, FontStyle.Bold, Color.white, 42f).alignment = TextAnchor.MiddleCenter;
            var identityText = CreateVertical("Sidebar Identity Text", identity.transform, 120f, 2f);
            Text("Sidebar App Title", identityText.transform, "TokenForge", 17, FontStyle.Bold, PrimaryText(), 26f);
            Text("Sidebar App Status", identityText.transform, "Local mode", 12, FontStyle.Bold, MutedText(), 22f);

            SidebarItem("Sidebar Dashboard Item", sidebar.transform, "Dashboard", true);
            SidebarItem("Sidebar Repository Item", sidebar.transform, "Repository", false);
            SidebarItem("Sidebar Codex Agent Item", sidebar.transform, "Codex Agent", false);
            SidebarItem("Sidebar Activity Item", sidebar.transform, "Activity", false);
            rootView.dashboardSettingsButton = SidebarItem("Sidebar Settings Item", sidebar.transform, "Settings", false);

            var spacer = new GameObject("Sidebar Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(sidebar.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleHeight = 1f;

            Text("Sidebar Local Mode", sidebar.transform, "Local mode", 12, FontStyle.Bold, SecondaryText(), 22f);
            Text("Sidebar Sync Optional", sidebar.transform, "Sync optional", 12, FontStyle.Bold, MutedText(), 22f);
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
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);
            contentRect.anchoredPosition = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 28);
            layout.spacing = 18f;
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
            var screen = CreateScreen("Start Screen Root", parent, 620f);
            rootView.startScreenRoot = screen;
            CreateProductDashboard(screen.transform, rootView, true);
            CreateHiddenCompatibilityControls(screen.transform, rootView);
        }

        private static void CreateDashboard(Transform parent, BootstrapRootView rootView)
        {
            var screen = CreateScreen("Game Dashboard Root", parent, 620f);
            rootView.gameDashboardRoot = screen;
            CreateProductDashboard(screen.transform, rootView, false);
        }

        private static void CreateProductDashboard(Transform parent, BootstrapRootView rootView, bool bindStartFields)
        {
            var hero = CreateSoftFrame("HeroCompanionCard", parent, CardSurface(), 24f, true);
            hero.AddComponent<LayoutElement>().preferredHeight = 268f;
            var heroLayout = hero.AddComponent<HorizontalLayoutGroup>();
            heroLayout.padding = new RectOffset(28, 28, 24, 22);
            heroLayout.spacing = 24f;
            heroLayout.childAlignment = TextAnchor.MiddleLeft;
            heroLayout.childControlWidth = true;
            heroLayout.childControlHeight = true;
            heroLayout.childForceExpandWidth = false;
            heroLayout.childForceExpandHeight = true;

            var visual = CreateSoftFrame("CompanionVisualArea", hero.transform, new Color(1f, 0.92f, 0.82f, 1f), 24f, false);
            var visualElement = visual.AddComponent<LayoutElement>();
            visualElement.preferredWidth = 320f;
            visualElement.minWidth = 300f;
            CreateEggVisual(visual.transform);

            var copy = CreateVertical("HeroCopyStack", hero.transform, 420f, 12f);
            copy.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Text("Hero Eyebrow", copy.transform, "Level 1 companion", 13, FontStyle.Bold, Coral(), 24f);
            var title = Text("Hero Title", copy.transform, "Your companion is ready to grow", 31, FontStyle.Bold, PrimaryText(), 76f);
            title.alignment = TextAnchor.MiddleLeft;
            var summary = Text("Hero Summary", copy.transform, "Connect a repository or Codex Agent when you want growth from local activity.", 16, FontStyle.Normal, SecondaryText(), 52f);
            summary.verticalOverflow = VerticalWrapMode.Overflow;
            var stats = CreateHorizontal("Hero Stat Row", copy.transform, 54f, 10f);
            StatPill("Hero Level Pill", stats.transform, "Level 1", Coral());
            StatPill("Hero XP Pill", stats.transform, "XP 0 / 1000", Mint());
            StatPill("Hero Growth Pill", stats.transform, "Today +0 XP", new Color(0.96f, 0.72f, 0.48f, 1f));
            var actions = CreateHorizontal("Hero Action Row", copy.transform, 52f, 10f);
            var startButton = Button("Run Analysis", actions.transform, 140f, ButtonStyle.Primary);
            var addButton = Button("Add Repository", actions.transform, 154f, ButtonStyle.Secondary);
            var codexButton = Button("Connect AI Agent", actions.transform, 190f, ButtonStyle.Secondary);

            var grid = new GameObject("StatusCardGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            grid.transform.SetParent(parent, false);
            grid.GetComponent<LayoutElement>().preferredHeight = 232f;
            var gridLayout = grid.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(214f, 214f);
            gridLayout.spacing = new Vector2(14f, 14f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;

            var repositoryCard = StatusCard("RepositoryStatusCard", grid.transform, "Repository", "Not selected", "Choose a local codebase for growth analysis.", "Add Repository", "R", Coral(), ButtonStyle.Secondary);
            var codexCard = StatusCard("CodexAgentStatusCard", grid.transform, "Codex Agent", "Not connected", "Connect local Codex activity when you are ready.", "Connect Codex Agent", "</>", Mint(), ButtonStyle.Secondary);
            var activityCard = StatusCard("ReviewActivityCard", grid.transform, "Activity", "No activity yet", "Run analysis, review the safe summary, then approve XP.", "Review Activity", "A", new Color(0.96f, 0.72f, 0.48f, 1f), ButtonStyle.Ghost);
            var progressCard = StatusCard("ProgressCard", grid.transform, "Progress", "Level 1", "XP 0 / 1000", "Start Game", "XP", Coral(), ButtonStyle.Ghost);

            var activitySection = CreateSoftFrame("ActivitySection", parent, CardSurface(), 20f, true);
            activitySection.AddComponent<LayoutElement>().preferredHeight = 84f;
            var activityLayout = activitySection.AddComponent<HorizontalLayoutGroup>();
            activityLayout.padding = new RectOffset(22, 22, 16, 16);
            activityLayout.spacing = 14f;
            activityLayout.childAlignment = TextAnchor.MiddleLeft;
            Dot("ActivitySection Status Dot", activitySection.transform, Mint(), 10f);
            Text("ActivitySection Text", activitySection.transform, "No activity yet. Run analysis from Repository or Codex Agent when you are ready.", 15, FontStyle.Bold, SecondaryText(), 46f)
                .GetComponent<LayoutElement>().flexibleWidth = 1f;
            Button("Run analysis", activitySection.transform, 140f, ButtonStyle.Ghost);

            if (bindStartFields)
            {
                rootView.onboardingPrimaryTitleLabel = title;
                rootView.onboardingStepIndicatorLabel = summary;
                rootView.startCharacterLabel = Text("Start Companion Runtime Label", visual.transform, string.Empty, 1, FontStyle.Normal, Color.clear, 1f);
                rootView.startStatsLabel = FindStatusBody(repositoryCard);
                rootView.startRecentSessionsLabel = FindStatusBody(codexCard);
                rootView.startGrowthLabel = FindStatusBody(activityCard);
                rootView.startPrivacySummaryLabel = Text("Start Privacy Runtime Label", activitySection.transform, "Local-first. Private details stay on this device; syncing is optional.", 1, FontStyle.Normal, Color.clear, 1f);
                rootView.validationStatusLabel = rootView.startPrivacySummaryLabel;
                rootView.startRunStatusLabel = Text("Start Sync Runtime Label", activitySection.transform, "Sync optional", 1, FontStyle.Normal, Color.clear, 1f);
                rootView.startGameButton = startButton;
                rootView.continueButton = startButton;
                rootView.startAnalyzeRepositoryButton = addButton;
                rootView.startAnalyzeAgentLogsButton = codexButton;
                rootView.startReviewAnalysisButton = FindButton(activityCard);
                rootView.startLoginButton = rootView.dashboardSettingsButton;
                var hiddenStartControls = new GameObject("Start Runtime Controls", typeof(RectTransform));
                hiddenStartControls.transform.SetParent(parent, false);
                hiddenStartControls.SetActive(false);
                rootView.startSaveRunButton = Button("Approve Growth", hiddenStartControls.transform, 150f, ButtonStyle.Ghost);
                rootView.startCreateSyncAccountButton = Button("Sync optional", hiddenStartControls.transform, 142f, ButtonStyle.Ghost);
                rootView.startCheckServerButton = Button("Check", hiddenStartControls.transform, 100f, ButtonStyle.Ghost);
                rootView.startSyncProgressButton = Button("Sync optional", hiddenStartControls.transform, 142f, ButtonStyle.Ghost);
            }
            else
            {
                rootView.dashboardCharacterStatusLabel = Text("Dashboard Companion Runtime Label", visual.transform, "Level 1\nXP 0 / 1000", 1, FontStyle.Normal, Color.clear, 1f);
                rootView.dashboardQuestLabel = FindStatusBody(progressCard);
                rootView.dashboardActivityLogLabel = Text("Dashboard Activity Runtime Label", activitySection.transform, "No activity yet", 1, FontStyle.Normal, Color.clear, 1f);
                rootView.dashboardGrowthStepConnectLabel = Text("Dashboard Connect Runtime Label", repositoryCard.transform, "Repository\nNot selected", 1, FontStyle.Normal, Color.clear, 1f);
                rootView.dashboardGrowthStepAnalyzeLabel = Text("Dashboard Analyze Runtime Label", activityCard.transform, "Activity\nNo activity yet", 1, FontStyle.Normal, Color.clear, 1f);
                rootView.dashboardGrowthStepReviewLabel = FindStatusBody(activityCard);
                rootView.dashboardGrowthStepSaveLabel = Text("Dashboard Save Runtime Label", progressCard.transform, "Progress\nLevel 1", 1, FontStyle.Normal, Color.clear, 1f);
                rootView.dashboardConnectedSourcesLabel = FindStatusBody(repositoryCard);
                rootView.dashboardPendingReviewLabel = FindStatusBody(codexCard);
                rootView.dashboardSyncReasonLabel = Text("Dashboard Settings Runtime Label", activitySection.transform, "Settings", 1, FontStyle.Normal, Color.clear, 1f);
                rootView.dashboardDesktopCompanionLabel = Text("Dashboard Companion Window Runtime Label", activitySection.transform, "Companion window\nHidden", 1, FontStyle.Normal, Color.clear, 1f);
                rootView.dashboardAnalyzeRepositoryButton = addButton;
                rootView.dashboardSourcesAddRepositoryButton = FindButton(repositoryCard);
                rootView.dashboardSourcesConnectAgentButton = FindButton(codexCard);
                rootView.dashboardSaveSessionButton = FindButton(activityCard);
                rootView.dashboardHistoryButton = codexButton;
                rootView.dashboardBackButton = startButton;
                var hiddenDashboardControls = new GameObject("Dashboard Runtime Controls", typeof(RectTransform));
                hiddenDashboardControls.transform.SetParent(parent, false);
                hiddenDashboardControls.SetActive(false);
                rootView.dashboardDiscardReviewButton = Button("Discard", hiddenDashboardControls.transform, 110f, ButtonStyle.Ghost);
                rootView.dashboardSyncButton = Button("Sync optional", hiddenDashboardControls.transform, 142f, ButtonStyle.Ghost);
                rootView.dashboardEnableDesktopCompanionButton = Button("Show Companion", hiddenDashboardControls.transform, 164f, ButtonStyle.Secondary);
                rootView.dashboardDisableDesktopCompanionButton = Button("Hide Companion", hiddenDashboardControls.transform, 154f, ButtonStyle.Ghost);
                rootView.dashboardResetDesktopCompanionButton = Button("Reset Position", hiddenDashboardControls.transform, 158f, ButtonStyle.Ghost);
                rootView.dashboardOpenDashboardButton = Button("Open App", hiddenDashboardControls.transform, 124f, ButtonStyle.Ghost);
            }
        }

        private static GameObject StatusCard(string name, Transform parent, string title, string badge, string description, string cta, string icon, Color accent, ButtonStyle buttonStyle)
        {
            var card = CreateSoftFrame(name, parent, CardSurface(), 18f, true);
            var layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 9f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = CreateHorizontal(name + " Header", card.transform, 34f, 8f);
            Dot(name + " Status Dot", header.transform, accent, 9f);
            Text(name + " Icon", header.transform, icon, 16, FontStyle.Bold, accent, 28f).GetComponent<LayoutElement>().preferredWidth = 30f;
            Text(name + " Title", header.transform, title, 17, FontStyle.Bold, PrimaryText(), 30f).GetComponent<LayoutElement>().flexibleWidth = 1f;
            Chip(name + " Badge", card.transform, badge, accent, 126f);
            Text(name + " Description", card.transform, description, 13, FontStyle.Normal, SecondaryText(), 52f);
            return Button(cta, card.transform, Mathf.Min(220f, Mathf.Max(150f, cta.Length * 10f + 46f)), buttonStyle).transform.parent.gameObject;
        }

        private static void CreateAddRepositoryFlow(Transform parent, BootstrapRootView rootView)
        {
            var screen = CreateScreen("Add Repository Flow Root", parent, 520f);
            rootView.runAnalysisRoot = screen;
            var header = CreateHorizontal("Flow Header", screen.transform, 58f, 12f);
            rootView.runAnalysisTitleLabel = Text("Flow Title", header.transform, "Add Repository", 28, FontStyle.Bold, PrimaryText(), 42f);
            rootView.runAnalysisTitleLabel.GetComponent<LayoutElement>().flexibleWidth = 1f;
            rootView.runAnalysisBackButton = Button("Back", header.transform, 110f, ButtonStyle.Ghost);
            rootView.runAnalysisSettingsButton = Button("Settings", header.transform, 120f, ButtonStyle.Ghost);

            var grid = CreateHorizontal("Flow Grid", screen.transform, 396f, 16f);
            var repository = Card("Repository Flow Card", grid.transform, 460f, 396f);
            Text("Repository Flow Title", repository.transform, "Repository", 23, FontStyle.Bold, PrimaryText(), 34f);
            rootView.onboardingGitStatusLabel = Text("Repository Flow Status", repository.transform, "Repository\nNot selected\nChoose a local repository, start analysis, then approve the summary.", 16, FontStyle.Normal, SecondaryText(), 118f);
            rootView.onboardingSelectLocalRepositoryButton = Button("Choose Repository", repository.transform, 210f, ButtonStyle.Primary);
            rootView.onboardingConnectGitAccountButton = Button("Start Analysis", repository.transform, 180f, ButtonStyle.Secondary);
            rootView.onboardingClearLocalRepositoryButton = Button("Clear Repository", repository.transform, 190f, ButtonStyle.Ghost);
            rootView.onboardingSkipGitButton = Button("Back", repository.transform, 110f, ButtonStyle.Ghost);
            rootView.onboardingReadySummaryLabel = Text("Analysis Result Summary", repository.transform, "Analysis result\nNo approved summary yet.", 16, FontStyle.Normal, SecondaryText(), 82f);

            var codex = Card("Codex Agent Flow Card", grid.transform, 460f, 396f);
            Text("Codex Flow Title", codex.transform, "Codex Agent", 23, FontStyle.Bold, PrimaryText(), 34f);
            rootView.onboardingAiAgentsStatusLabel = Text("Codex Flow Status", codex.transform, "Codex Agent\nNot connected\nChoose a log folder or detect local activity.", 16, FontStyle.Normal, SecondaryText(), 118f);
            rootView.codexAgentToggle = Toggle("Codex Agent Toggle", codex.transform, "Codex Agent", false, 220f);
            rootView.codexAgentStatusLabel = Text("Codex Agent Detection", codex.transform, "Not connected", 15, FontStyle.Bold, SecondaryText(), 48f);
            rootView.codexAgentConnectButton = Button("Connect Codex", codex.transform, 190f, ButtonStyle.Primary);
            rootView.otherAgentSelectLogFolderButton = Button("Choose Log Folder", codex.transform, 210f, ButtonStyle.Secondary);
            CreateAgentCompatibilityControls(codex.transform, rootView);
        }

        private static void CreateSettings(Transform parent, BootstrapRootView rootView)
        {
            var screen = CreateScreen("Settings Root", parent, 476f);
            rootView.settingsAdvancedRoot = screen;
            var header = CreateHorizontal("Settings Header", screen.transform, 58f, 12f);
            Text("Settings Title", header.transform, "Settings", 28, FontStyle.Bold, PrimaryText(), 42f).GetComponent<LayoutElement>().flexibleWidth = 1f;
            rootView.settingsBackButton = Button("Back", header.transform, 110f, ButtonStyle.Ghost);
            rootView.settingsDeveloperDiagnosticsButton = Button("Settings", header.transform, 120f, ButtonStyle.Ghost);
            var grid = CreateHorizontal("Settings Grid", screen.transform, 330f, 16f);
            var privacy = Card("Local Mode Card", grid.transform, 460f, 330f);
            rootView.settingsSummaryLabel = Text("Settings Summary", privacy.transform, "Settings\nLocal-first. Private details stay on this device; syncing is optional.", 16, FontStyle.Normal, SecondaryText(), 146f);
            rootView.settingsSyncConflictLabel = Text("Optional Sync Status", privacy.transform, "Optional sync\nUnavailable in this build.", 15, FontStyle.Normal, SecondaryText(), 82f);
            var overlay = Card("Companion Window Card", grid.transform, 460f, 330f);
            rootView.settingsDesktopCompanionStatusLabel = Text("Overlay Status", overlay.transform, "Companion window\nHidden", 16, FontStyle.Normal, SecondaryText(), 80f);
            rootView.settingsDesktopCompanionEnabledToggle = Toggle("Show Companion Toggle", overlay.transform, "Show Companion", false, 220f);
            rootView.settingsDesktopCompanionClickThroughToggle = Toggle("Click Through Toggle", overlay.transform, "Click-through", false, 220f);
            rootView.settingsDesktopCompanionMotionModeDropdown = Dropdown("Motion Mode", overlay.transform, 220f);
            rootView.settingsResetOverlayPositionButton = Button("Reset Position", overlay.transform, 170f, ButtonStyle.Secondary);
        }

        private static void CreateEggVisual(Transform parent)
        {
            var glow = CreateSoftFrame("EggSoftGlow", parent, new Color(1f, 0.47f, 0.28f, 0.22f), 100f, false);
            glow.GetComponent<SoftUiGraphic>().ellipse = true;
            var glowRect = glow.GetComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.15f, 0.1f);
            glowRect.anchorMax = new Vector2(0.85f, 0.86f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;

            var egg = CreateSoftFrame("CompanionEggVisual", parent, new Color(1f, 0.82f, 0.56f, 1f), 100f, false);
            var eggGraphic = egg.GetComponent<SoftUiGraphic>();
            eggGraphic.ellipse = true;
            eggGraphic.verticalGradient = true;
            eggGraphic.topColor = new Color(1f, 0.97f, 0.82f, 1f);
            eggGraphic.bottomColor = new Color(1f, 0.48f, 0.28f, 1f);
            var eggRect = egg.GetComponent<RectTransform>();
            eggRect.anchorMin = new Vector2(0.26f, 0.16f);
            eggRect.anchorMax = new Vector2(0.74f, 0.86f);
            eggRect.offsetMin = Vector2.zero;
            eggRect.offsetMax = Vector2.zero;
            AddSoftShadow(egg, new Color(0.44f, 0.21f, 0.12f, 0.2f), new Vector2(0f, -8f));

            var shine = CreateSoftFrame("EggHighlight", parent, new Color(1f, 1f, 1f, 0.48f), 100f, false);
            shine.GetComponent<SoftUiGraphic>().ellipse = true;
            var shineRect = shine.GetComponent<RectTransform>();
            shineRect.anchorMin = new Vector2(0.4f, 0.56f);
            shineRect.anchorMax = new Vector2(0.52f, 0.78f);
            shineRect.offsetMin = Vector2.zero;
            shineRect.offsetMax = Vector2.zero;
        }

        private static void CreateHiddenCompatibilityControls(Transform parent, BootstrapRootView rootView)
        {
            var hidden = new GameObject("Optional Sync Controls", typeof(RectTransform));
            hidden.transform.SetParent(parent, false);
            hidden.SetActive(false);
            rootView.onboardingAccountStepRoot = hidden;
            rootView.onboardingAiAgentsStepRoot = hidden;
            rootView.onboardingGitStepRoot = hidden;
            rootView.onboardingReadyStepRoot = hidden;
            rootView.onboardingEmailInput = Input("Email", hidden.transform);
            rootView.onboardingDisplayNameInput = Input("Display Name", hidden.transform);
            rootView.onboardingPasswordInput = Input("Password", hidden.transform);
            rootView.onboardingCreateAccountButton = Button("Sync optional", hidden.transform, 140f, ButtonStyle.Ghost);
            rootView.onboardingLoginButton = Button("Settings", hidden.transform, 120f, ButtonStyle.Ghost);
            rootView.onboardingContinueOfflineButton = Button("Local mode", hidden.transform, 130f, ButtonStyle.Ghost);
            rootView.onboardingBackButton = Button("Back", hidden.transform, 100f, ButtonStyle.Ghost);
            rootView.onboardingContinueButton = Button("Start Game", hidden.transform, 140f, ButtonStyle.Ghost);
            rootView.onboardingAccountStatusLabel = Text("Optional Sync Status Hidden", hidden.transform, "Optional sync", 12, FontStyle.Normal, MutedText(), 24f);
        }

        private static void CreateAgentCompatibilityControls(Transform parent, BootstrapRootView rootView)
        {
            var hidden = new GameObject("Other Agent Controls", typeof(RectTransform));
            hidden.transform.SetParent(parent, false);
            hidden.SetActive(false);
            rootView.cursorAgentToggle = Toggle("Cursor Toggle", hidden.transform, "Cursor", false, 120f);
            rootView.claudeAgentToggle = Toggle("Claude Toggle", hidden.transform, "Claude Code", false, 160f);
            rootView.copilotAgentToggle = Toggle("Copilot Toggle", hidden.transform, "GitHub Copilot", false, 180f);
            rootView.otherAgentToggle = Toggle("Manual Toggle", hidden.transform, "Manual Log Folder", false, 180f);
            rootView.cursorAgentStatusLabel = Text("Cursor Status", hidden.transform, "Not connected", 12, FontStyle.Normal, MutedText(), 24f);
            rootView.claudeAgentStatusLabel = Text("Claude Status", hidden.transform, "Not connected", 12, FontStyle.Normal, MutedText(), 24f);
            rootView.copilotAgentStatusLabel = Text("Copilot Status", hidden.transform, "Not connected", 12, FontStyle.Normal, MutedText(), 24f);
            rootView.otherAgentStatusLabel = Text("Manual Status", hidden.transform, "Not connected", 12, FontStyle.Normal, MutedText(), 24f);
            rootView.cursorAgentConnectButton = Button("Select", hidden.transform, 100f, ButtonStyle.Ghost);
            rootView.claudeAgentConnectButton = Button("Select", hidden.transform, 100f, ButtonStyle.Ghost);
            rootView.copilotAgentConnectButton = Button("Select", hidden.transform, 100f, ButtonStyle.Ghost);
        }

        private static GameObject CreateScreen(string name, Transform parent, float height)
        {
            var screen = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            screen.transform.SetParent(parent, false);
            var element = screen.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            var layout = screen.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return screen;
        }

        private static GameObject Card(string name, Transform parent, float width, float height)
        {
            var card = CreateSoftFrame(name, parent, CardSurface(), 18f, true);
            card.AddComponent<VerticalLayoutGroup>();
            card.AddComponent<LayoutElement>();
            card.GetComponent<LayoutElement>().minWidth = width;
            card.GetComponent<LayoutElement>().preferredWidth = width;
            card.GetComponent<LayoutElement>().preferredHeight = height;
            var layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 18, 18);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return card;
        }

        private static GameObject CreateImageFrame(string name, Transform parent, Color color)
        {
            var frame = new GameObject(name, typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(parent, false);
            frame.GetComponent<Image>().color = color;
            return frame;
        }

        private static GameObject CreateSoftFrame(string name, Transform parent, Color color, float radius, bool shadow)
        {
            var frame = new GameObject(name, typeof(RectTransform), typeof(SoftUiGraphic));
            frame.transform.SetParent(parent, false);
            var graphic = frame.GetComponent<SoftUiGraphic>();
            graphic.color = color;
            graphic.cornerRadius = radius;
            graphic.raycastTarget = false;
            if (shadow)
            {
                AddSoftShadow(frame, new Color(0.18f, 0.13f, 0.08f, 0.11f), new Vector2(0f, -4f));
            }

            return frame;
        }

        private static void AddSoftShadow(GameObject target, Color color, Vector2 distance)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static GameObject CreateHorizontal(string name, Transform parent, float height, float spacing)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = height;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        private static GameObject CreateVertical(string name, Transform parent, float width, float spacing)
        {
            var stack = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            stack.transform.SetParent(parent, false);
            stack.GetComponent<LayoutElement>().minWidth = width;
            stack.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var layout = stack.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return stack;
        }

        private static Text Text(string name, Transform parent, string content, int size, FontStyle style, Color color, float height)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            obj.GetComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private static Button Button(string label, Transform parent, float width, ButtonStyle style)
        {
            var button = CreateSoftFrame(label, parent, ButtonColor(style), 22f, false);
            var graphic = button.GetComponent<SoftUiGraphic>();
            graphic.raycastTarget = true;
            var buttonComponent = button.AddComponent<Button>();
            buttonComponent.targetGraphic = graphic;
            button.AddComponent<LayoutElement>();
            var element = button.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = Mathf.Min(width, 110f);
            element.preferredHeight = 44f;
            var text = Text("Label", button.transform, label, 14, FontStyle.Bold, ButtonTextColor(style), 42f);
            Stretch(text.GetComponent<RectTransform>());
            text.alignment = TextAnchor.MiddleCenter;
            var colors = buttonComponent.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.91f, 1f);
            colors.pressedColor = new Color(0.92f, 0.86f, 0.78f, 1f);
            colors.disabledColor = new Color(0.88f, 0.84f, 0.78f, 0.45f);
            buttonComponent.colors = colors;
            return buttonComponent;
        }

        private static Button SidebarItem(string name, Transform parent, string label, bool selected)
        {
            var button = Button(label, parent, 176f, selected ? ButtonStyle.SidebarSelected : ButtonStyle.SidebarGhost);
            button.gameObject.name = name;
            return button;
        }

        private static Text Chip(string name, Transform parent, string label, Color accent, float width)
        {
            var chip = CreateSoftFrame(name, parent, new Color(accent.r, accent.g, accent.b, 0.14f), 15f, false);
            chip.AddComponent<LayoutElement>().preferredWidth = width;
            chip.GetComponent<LayoutElement>().preferredHeight = 30f;
            var row = chip.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(12, 12, 6, 5);
            row.spacing = 6f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childForceExpandWidth = false;
            Dot(name + " Dot", chip.transform, accent, 8f);
            return Text(name + " Label", chip.transform, label, 12, FontStyle.Bold, SecondaryText(), 20f);
        }

        private static void StatPill(string name, Transform parent, string label, Color accent)
        {
            var pill = CreateSoftFrame(name, parent, new Color(accent.r, accent.g, accent.b, 0.14f), 16f, false);
            pill.AddComponent<LayoutElement>().preferredWidth = 126f;
            pill.GetComponent<LayoutElement>().preferredHeight = 42f;
            var text = Text(name + " Label", pill.transform, label, 13, FontStyle.Bold, PrimaryText(), 40f);
            Stretch(text.GetComponent<RectTransform>());
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static void TrafficLight(string name, Transform parent, Color color)
        {
            Dot(name, parent, color, 12f);
        }

        private static void Dot(string name, Transform parent, Color color, float size)
        {
            var dot = CreateSoftFrame(name, parent, color, size, false);
            dot.GetComponent<SoftUiGraphic>().ellipse = true;
            var element = dot.AddComponent<LayoutElement>();
            element.preferredWidth = size;
            element.preferredHeight = size;
        }

        private static Toggle Toggle(string name, Transform parent, string label, bool value, float width)
        {
            var row = CreateHorizontal(name, parent, 36f, 8f);
            row.GetComponent<LayoutElement>().preferredWidth = width;
            var toggleObject = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(row.transform, false);
            toggleObject.AddComponent<LayoutElement>().preferredWidth = 30f;
            var check = CreateSoftFrame("Checkmark", toggleObject.transform, Coral(), 9f, false);
            check.GetComponent<RectTransform>().sizeDelta = new Vector2(18f, 18f);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.graphic = check.GetComponent<SoftUiGraphic>();
            toggle.isOn = value;
            Text("Toggle Label", row.transform, label, 14, FontStyle.Bold, PrimaryText(), 32f);
            return toggle;
        }

        private static Dropdown Dropdown(string name, Transform parent, float width)
        {
            var dropdownObject = CreateSoftFrame(name, parent, new Color(1f, 0.97f, 0.92f, 1f), 14f, false);
            dropdownObject.AddComponent<Dropdown>();
            dropdownObject.AddComponent<LayoutElement>().preferredWidth = width;
            dropdownObject.GetComponent<LayoutElement>().preferredHeight = 42f;
            var label = Text("Label", dropdownObject.transform, "Calm", 14, FontStyle.Bold, PrimaryText(), 40f);
            Stretch(label.GetComponent<RectTransform>());
            dropdownObject.GetComponent<Dropdown>().captionText = label;
            return dropdownObject.GetComponent<Dropdown>();
        }

        private static InputField Input(string name, Transform parent)
        {
            var inputObject = CreateSoftFrame(name, parent, new Color(1f, 0.97f, 0.92f, 1f), 12f, false);
            inputObject.SetActive(false);
            var input = inputObject.AddComponent<InputField>();
            var text = Text("Text", inputObject.transform, string.Empty, 14, FontStyle.Normal, PrimaryText(), 32f);
            input.textComponent = text;
            return input;
        }

        private static Text FindStatusBody(GameObject card)
        {
            return card.transform.Find(card.name + " Description")?.GetComponent<Text>()
                ?? card.GetComponentInChildren<Text>(true);
        }

        private static Button FindButton(GameObject card)
        {
            return card.GetComponentInChildren<Button>(true);
        }

        private static void SavePrefab(string name, GameObject instance)
        {
            var path = Path.Combine(PrefabFolder, name).Replace('\\', '/');
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
                }

                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Color CreamBackground()
        {
            return new Color(0.965f, 0.945f, 0.905f, 1f);
        }

        private static Color Shell()
        {
            return new Color(0.995f, 0.985f, 0.965f, 0.98f);
        }

        private static Color MainSurface()
        {
            return new Color(0.995f, 0.988f, 0.972f, 1f);
        }

        private static Color SidebarSurface()
        {
            return new Color(0.965f, 0.936f, 0.888f, 1f);
        }

        private static Color CardSurface()
        {
            return new Color(1f, 0.995f, 0.982f, 1f);
        }

        private static Color Coral()
        {
            return new Color(0.95f, 0.38f, 0.22f, 1f);
        }

        private static Color Mint()
        {
            return new Color(0.18f, 0.68f, 0.54f, 1f);
        }

        private static Color PrimaryText()
        {
            return new Color(0.13f, 0.115f, 0.095f, 1f);
        }

        private static Color SecondaryText()
        {
            return new Color(0.39f, 0.35f, 0.3f, 1f);
        }

        private static Color MutedText()
        {
            return new Color(0.56f, 0.51f, 0.45f, 1f);
        }

        private static Color ButtonColor(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Primary:
                    return Coral();
                case ButtonStyle.Secondary:
                    return new Color(1f, 0.91f, 0.84f, 1f);
                case ButtonStyle.SidebarSelected:
                    return new Color(1f, 0.995f, 0.982f, 1f);
                case ButtonStyle.SidebarGhost:
                    return new Color(1f, 0.96f, 0.9f, 0.42f);
                default:
                    return new Color(1f, 0.97f, 0.92f, 0.65f);
            }
        }

        private static Color ButtonTextColor(ButtonStyle style)
        {
            return style == ButtonStyle.Primary ? Color.white : PrimaryText();
        }

        private enum ButtonStyle
        {
            Primary,
            Secondary,
            Ghost,
            SidebarSelected,
            SidebarGhost
        }
    }
}
