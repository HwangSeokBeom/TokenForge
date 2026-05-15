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
            var root = new GameObject("BootstrapRoot", typeof(RectTransform), typeof(Image), typeof(BootstrapRootView));
            Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0.035f, 0.047f, 0.063f, 1f);
            var rootView = root.GetComponent<BootstrapRootView>();

            var scroll = new GameObject("Root Scroll", typeof(RectTransform), typeof(ScrollRect));
            scroll.transform.SetParent(root.transform, false);
            Stretch(scroll.GetComponent<RectTransform>());
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scroll.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(24f, 0f);
            contentRect.offsetMax = new Vector2(-24f, 0f);
            contentRect.anchoredPosition = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 32, 32);
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

            var header = Panel("Header", content.transform, 172f);
            Text("TokenForge Title", header.transform, "TokenForge", 36, FontStyle.Bold, Primary(), 44f);
            Text("TokenForge Subtitle", header.transform, "Privacy-safe developer activity companion", 17, FontStyle.Bold, Secondary(), 30f);
            rootView.headerStatusLabel = Text("Header Status", header.transform, "Client status", 15, FontStyle.Bold, Accent(), 30f);
            rootView.validationStatusLabel = Text("Prefab Validation", header.transform, "Prefab UI ready. Local analysis, account, Safe Sync, and privacy panels are available below.", 14, FontStyle.Normal, Secondary(), 34f);

            rootView.accountPanel = CreateAccountPanel().GetComponent<AccountPanelView>();
            rootView.accountPanel.transform.SetParent(content.transform, false);
            rootView.activityAnalysisPanel = CreateActivityAnalysisPanel().GetComponent<ActivityAnalysisPanelView>();
            rootView.activityAnalysisPanel.transform.SetParent(content.transform, false);
            rootView.approvedLocationsPanel = CreateApprovedLocationsPanel().GetComponent<ApprovedLocationsPanelView>();
            rootView.approvedLocationsPanel.transform.SetParent(content.transform, false);
            rootView.reviewPanel = CreateReviewPanel().GetComponent<ReviewPanelView>();
            rootView.reviewPanel.transform.SetParent(content.transform, false);
            rootView.safeSyncPanel = CreateSafeSyncPanel().GetComponent<SafeSyncPanelView>();
            rootView.safeSyncPanel.transform.SetParent(content.transform, false);
            rootView.recentSessionsPanel = CreateRecentSessionsPanel().GetComponent<RecentSessionsPanelView>();
            rootView.recentSessionsPanel.transform.SetParent(content.transform, false);
            rootView.privacyNoticePanel = CreatePrivacyNoticePanel().GetComponent<PrivacyNoticePanelView>();
            rootView.privacyNoticePanel.transform.SetParent(content.transform, false);
            rootView.rootScrollRect = scrollRect;
            return root;
        }

        private static GameObject CreateAccountPanel()
        {
            var panel = Panel("AccountPanel", null, 304f);
            var view = panel.AddComponent<AccountPanelView>();
            Title(panel, "Account");
            Text("Account Helper", panel.transform, "Local analysis is available while logged out. Log in only when you choose to use server Safe Sync.", 14, FontStyle.Bold, Primary(), 38f);
            view.statusLabel = Text("Account Status", panel.transform, "Logged out. Sync, fetch, delete, refresh, and logout stay disabled until a local session is authenticated.", 13, FontStyle.Normal, Secondary(), 44f);
            Text("Account Disabled Help", panel.transform, "No login starts automatically. Passwords are masked and cleared after submit.", 13, FontStyle.Normal, Muted(), 30f);
            var inputRow = Row("Account Fields", panel.transform, 58f);
            view.baseUrlInput = InputFieldGroup("Server base URL", inputRow.transform, "http://localhost:3000/api/v1", 260f);
            view.emailInput = InputFieldGroup("Email", inputRow.transform, string.Empty, 210f);
            view.displayNameInput = InputFieldGroup("Display name", inputRow.transform, string.Empty, 190f);
            view.passwordInput = InputFieldGroup("Password", inputRow.transform, string.Empty, 180f, InputField.ContentType.Password);
            var buttonRow = Row("Account Actions", panel.transform, 36f);
            view.loginButton = Button("Log In for Safe Sync", buttonRow.transform, 164f);
            view.signupButton = Button("Create Sync Account", buttonRow.transform, 174f);
            view.logoutButton = Button("Log Out of Safe Sync", buttonRow.transform, 172f);
            view.refreshUserButton = Button("Refresh Account Status", buttonRow.transform, 190f);
            return panel;
        }

        private static GameObject CreateActivityAnalysisPanel()
        {
            var panel = Panel("ActivityAnalysisPanel", null, 440f);
            var view = panel.AddComponent<ActivityAnalysisPanelView>();
            Title(panel, "Local Analysis");
            Text("Local Analysis Helper", panel.transform, "Local analysis works without login. Raw paths, prompts, responses, commands, filenames, and source snippets stay on this device.", 14, FontStyle.Bold, Primary(), 44f);
            view.gitStatusLabel = Text("Git Status", panel.transform, "Git: no repository selected. Select or use an approved local location before Analyze Git is enabled.", 13, FontStyle.Normal, Secondary(), 42f);
            var gitRow = Row("Git Controls", panel.transform, 58f);
            view.gitWindowDaysInput = InputFieldGroup("Git window days", gitRow.transform, "7", 140f);
            view.gitMaxCommitsInput = InputFieldGroup("Max Git commits", gitRow.transform, "200", 150f);
            view.gitIncludeUncommittedToggle = Toggle("Uncommitted", gitRow.transform, true, 150f);
            view.gitIncludeCommitsToggle = Toggle("Commits", gitRow.transform, true, 120f);
            view.selectGitButton = Button("Select Repository", gitRow.transform, 156f);
            view.analyzeGitButton = Button("Analyze Git", gitRow.transform, 136f);
            Text("Git Disabled Help", panel.transform, "Analyze Git stays disabled until a local repository is selected. Review appears before anything is saved.", 13, FontStyle.Normal, Muted(), 30f);
            view.agentStatusLabel = Text("Agent Status", panel.transform, "Agent: no local log location selected. Select approved local logs before Analyze Agent is enabled.", 13, FontStyle.Normal, Secondary(), 42f);
            var agentRow = Row("Agent Controls", panel.transform, 58f);
            view.agentProviderDropdown = DropdownGroup("Agent provider", agentRow.transform, 170f);
            view.agentWindowDaysInput = InputFieldGroup("Agent window days", agentRow.transform, "7", 150f);
            view.agentMaxFilesInput = InputFieldGroup("Max files", agentRow.transform, "500", 130f);
            view.agentMaxEntriesInput = InputFieldGroup("Max entries", agentRow.transform, "5000", 130f);
            view.selectAgentButton = Button("Select Agent Logs", agentRow.transform, 168f);
            view.analyzeAgentButton = Button("Analyze Agent", agentRow.transform, 148f);
            Text("Agent Disabled Help", panel.transform, "Analyze Agent stays disabled until a local provider location is selected. Raw log text is never rendered.", 13, FontStyle.Normal, Muted(), 30f);
            return panel;
        }

        private static GameObject CreateApprovedLocationsPanel()
        {
            var panel = Panel("ApprovedLocationsPanel", null, 312f);
            var view = panel.AddComponent<ApprovedLocationsPanelView>();
            Title(panel, "Approved Local Locations");
            Text("Approved Locations Helper", panel.transform, "Approvals are local-only safety gates. Aliases may be shown, but raw approved paths are never displayed or synced.", 14, FontStyle.Bold, Primary(), 40f);
            view.statusLabel = Text("Approved Locations Status", panel.transform, ApprovedActivityAnalysisViewModel.ApprovedLocationsNotice + " No approved locations yet. Add one to reuse it without exposing the raw path.", 13, FontStyle.Bold, Primary(), 48f);
            var gitRow = Row("Git Approved Controls", panel.transform, 58f);
            view.gitAliasInput = InputFieldGroup("Git alias", gitRow.transform, "Git repository", 170f);
            view.gitLocationsDropdown = DropdownGroup("Approved Git locations", gitRow.transform, 240f);
            view.approveGitButton = Button("Approve Git Location", gitRow.transform, 170f);
            view.useGitButton = Button("Use Git Location", gitRow.transform, 150f);
            view.disableGitButton = Button("Disable Git Location", gitRow.transform, 164f);
            view.removeGitButton = Button("Remove Git Location", gitRow.transform, 160f);
            var agentRow = Row("Agent Approved Controls", panel.transform, 58f);
            view.agentAliasInput = InputFieldGroup("Agent alias", agentRow.transform, "Agent logs", 170f);
            view.agentLocationsDropdown = DropdownGroup("Approved agent locations", agentRow.transform, 250f);
            view.approveAgentButton = Button("Approve Agent Location", agentRow.transform, 184f);
            view.useAgentButton = Button("Use Agent Location", agentRow.transform, 164f);
            view.disableAgentButton = Button("Disable Agent Location", agentRow.transform, 178f);
            view.removeAgentButton = Button("Remove Agent Location", agentRow.transform, 174f);
            Text("Approved Locations Empty Help", panel.transform, "No approved locations are present on clean install. Disable or remove entries from this panel at any time.", 13, FontStyle.Normal, Muted(), 30f);
            return panel;
        }

        private static GameObject CreateReviewPanel()
        {
            var panel = Panel("ReviewPanel", null, 248f);
            var view = panel.AddComponent<ReviewPanelView>();
            Title(panel, "Review");
            view.noticeLabel = Text("Review Notice", panel.transform, ApprovedActivityAnalysisViewModel.SafeAggregateNotice, 13, FontStyle.Bold, Primary(), 52f);
            view.reviewLabel = Text("Review Summary", panel.transform, "No review ready. Run local analysis, inspect the aggregate summary, then save or discard.", 13, FontStyle.Normal, Secondary(), 58f);
            var row = Row("Review Controls", panel.transform, 36f);
            view.saveGitButton = Button("Save Git Review", row.transform, 140f);
            view.discardGitButton = Button("Discard Git Review", row.transform, 160f);
            view.saveAgentButton = Button("Save Agent Review", row.transform, 156f);
            view.discardAgentButton = Button("Discard Agent Review", row.transform, 176f);
            return panel;
        }

        private static GameObject CreateSafeSyncPanel()
        {
            var panel = Panel("SafeSyncPanel", null, 792f);
            var view = panel.AddComponent<SafeSyncPanelView>();
            Title(panel, "Safe Sync");
            view.noticeLabel = Text("Safe Sync Notice", panel.transform, "Only aggregate-safe sessions are synced. Health check is explicit; sync, fetch, and delete require login.", 13, FontStyle.Bold, Primary(), 52f);
            view.statusLabel = Text("Safe Sync Status", panel.transform, "Logged out. Remote sync actions are disabled until login; no sync starts automatically.", 13, FontStyle.Normal, Secondary(), 46f);
            Text("Safe Sync Disabled Help", panel.transform, "Remote sync actions are disabled until login. No sync starts automatically. Use Check Server for an explicit health check.", 13, FontStyle.Normal, Muted(), 32f);
            var row = Row("Safe Sync Controls", panel.transform, 58f);
            view.baseUrlInput = InputFieldGroup("Server base URL", row.transform, "http://localhost:3000/api/v1", 280f);
            view.healthButton = Button("Check Server", row.transform, 132f);
            view.syncButton = Button("Sync Safe Sessions", row.transform, 176f);
            view.fetchButton = Button("Fetch Remote", row.transform, 142f);
            view.remoteSessionsDropdown = DropdownGroup("Remote sessions", row.transform, 250f);
            view.deleteRemoteButton = Button("Delete Remote Session", row.transform, 184f);
            view.retryQueueLabel = Text("Retry Queue Status", panel.transform, "Retry Queue: pending 0 | failed 0 | paused 0 | succeeded 0", 13, FontStyle.Normal, Secondary(), 34f);
            view.conflictLabel = Text("Conflict Status", panel.transform, "Conflicts: none unresolved.", 13, FontStyle.Normal, Secondary(), 30f);
            view.tombstoneLabel = Text("Tombstone Status", panel.transform, "Deletes: pending 0 | synced 0 | failed 0", 13, FontStyle.Normal, Secondary(), 30f);
            var retryRow = Row("Retry Queue Controls", panel.transform, 58f);
            view.retryEntriesDropdown = DropdownGroup("Retry entries", retryRow.transform, 250f);
            view.retryPendingButton = Button("Process Eligible Retries", retryRow.transform, 190f);
            view.forceRetryButton = Button("Force Retry Selected", retryRow.transform, 180f);
            view.cancelRetryButton = Button("Cancel Selected Retry", retryRow.transform, 184f);
            view.cancelAllFailedRetryButton = Button("Cancel Failed Retries", retryRow.transform, 184f);
            view.clearSucceededButton = Button("Clear Succeeded", retryRow.transform, 150f);
            var retryBatchRow = Row("Retry Batch Controls", panel.transform, 40f);
            view.pauseAllPendingRetryButton = Button("Pause Pending Retries", retryBatchRow.transform, 184f);
            view.resumeAllPausedRetryButton = Button("Resume Paused Retries", retryBatchRow.transform, 190f);
            var tombstoneRow = Row("Tombstone Controls", panel.transform, 58f);
            view.tombstonesDropdown = DropdownGroup("Delete tombstones", tombstoneRow.transform, 240f);
            view.enqueueTombstoneDeletesButton = Button("Enqueue All Pending Deletes", tombstoneRow.transform, 230f);
            view.processTombstoneDeletesButton = Button("Process Pending Deletes Once", tombstoneRow.transform, 236f);
            view.cancelTombstoneButton = Button("Cancel Selected Tombstone", tombstoneRow.transform, 220f);
            view.cancelAllFailedTombstonesButton = Button("Cancel Failed Tombstones", tombstoneRow.transform, 222f);
            view.clearResolvedTombstonesButton = Button("Clear Resolved Tombstones", tombstoneRow.transform, 226f);
            Text("Conflict Help", panel.transform, "Merge policies use safe aggregate fields only. Keep Local queues re-upload, Keep Remote applies a safe remote aggregate, and Mark Resolved records a decision without data changes.", 13, FontStyle.Normal, Muted(), 46f);
            var conflictRow = Row("Conflict Controls", panel.transform, 58f);
            view.conflictsDropdown = DropdownGroup("Conflicts", conflictRow.transform, 250f);
            view.keepLocalButton = Button("Keep Local", conflictRow.transform, 132f);
            view.keepRemoteButton = Button("Keep Remote", conflictRow.transform, 142f);
            view.markConflictResolvedButton = Button("Mark Resolved", conflictRow.transform, 150f);
            view.clearConflictAuditButton = Button("Clear Resolved History", conflictRow.transform, 188f);
            view.cancelConflictResolutionButton = Button("Cancel Resolution", conflictRow.transform, 166f);
            return panel;
        }

        private static GameObject CreateRecentSessionsPanel()
        {
            var panel = Panel("RecentSessionsPanel", null, 330f);
            var view = panel.AddComponent<RecentSessionsPanelView>();
            Title(panel, "Recent Sessions");
            Text("Recent Sessions Helper", panel.transform, "Local sessions are stored on this device. Remote sessions appear only after an explicit fetch. Log in, then explicitly fetch remote summaries.", 13, FontStyle.Bold, Primary(), 36f);
            view.localRecentSessionsLabel = Text("Local Recent Sessions", panel.transform, "Local: no saved safe sessions yet. Save an approved aggregate review to populate this list.", 13, FontStyle.Normal, Secondary(), 58f);
            view.localDeleteStatusLabel = Text("Local Delete Status", panel.transform, "Deleting a synced local session creates a local tombstone so remote delete can be synced explicitly.", 13, FontStyle.Normal, Muted(), 34f);
            var deleteRow = Row("Local Delete Controls", panel.transform, 58f);
            view.localSessionsDropdown = DropdownGroup("Local sessions", deleteRow.transform, 280f);
            view.deleteLocalSessionButton = Button("Delete Local Session", deleteRow.transform, 190f);
            view.remoteSessionsLabel = Text("Remote Sessions", panel.transform, "Remote: no fetched remote safe sessions. Log in, then explicitly fetch remote summaries.", 13, FontStyle.Normal, Secondary(), 58f);
            return panel;
        }

        private static GameObject CreatePrivacyNoticePanel()
        {
            var panel = Panel("PrivacyNoticePanel", null, 166f);
            var view = panel.AddComponent<PrivacyNoticePanelView>();
            Title(panel, "Privacy");
            view.bodyLabel = Text("Privacy Body", panel.transform, "Safe Sync sends aggregate-only session summaries. Approved locations stay on this device and are never synced. Raw local paths, prompts, responses, commands, filenames, repo names, branch names, source snippets, tokens, and payload bodies are not rendered or synced.", 14, FontStyle.Bold, Primary(), 98f);
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

            panel.GetComponent<Image>().color = new Color(0.052f, 0.066f, 0.09f, 1f);
            var outline = panel.GetComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.24f, 0.30f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 10f;
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
            Text("Title", panel.transform, title, 19, FontStyle.Bold, Accent(), 30f);
        }

        private static GameObject Row(string name, Transform parent, float height)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = row.GetComponent<LayoutElement>();
            element.preferredHeight = height;
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
            element.flexibleWidth = 1f;
            return text;
        }

        private static InputField InputFieldGroup(string label, Transform parent, string value, float width, InputField.ContentType contentType = InputField.ContentType.Standard)
        {
            var group = FieldGroup(label + " Field", parent, width);
            Text(label + " Label", group.transform, label, 13, FontStyle.Bold, Secondary(), 20f);
            return Input(label, group.transform, value, width, contentType);
        }

        private static Dropdown DropdownGroup(string label, Transform parent, float width)
        {
            var group = FieldGroup(label + " Field", parent, width);
            Text(label + " Label", group.transform, label, 13, FontStyle.Bold, Secondary(), 20f);
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
            element.preferredHeight = 56f;
            return group;
        }

        private static InputField Input(string name, Transform parent, string value, float width, InputField.ContentType contentType = InputField.ContentType.Standard)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.075f, 0.095f, 0.125f, 1f);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 32f;
            var text = Text("Text", go.transform, value, 14, FontStyle.Normal, Primary(), 28f);
            Stretch(text.rectTransform, 8f, 2f, 8f, 2f);
            var placeholder = Text("Placeholder", go.transform, name, 13, FontStyle.Italic, Secondary(), 28f);
            Stretch(placeholder.rectTransform, 8f, 2f, 8f, 2f);
            var input = go.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.contentType = contentType;
            input.text = value;
            return input;
        }

        private static Button Button(string label, Transform parent, float width)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Accent();
            var colors = go.GetComponent<Button>().colors;
            colors.normalColor = Accent();
            colors.highlightedColor = new Color(0.98f, 0.72f, 0.36f, 1f);
            colors.pressedColor = new Color(0.76f, 0.48f, 0.2f, 1f);
            colors.disabledColor = new Color(0.2f, 0.22f, 0.25f, 1f);
            go.GetComponent<Button>().colors = colors;
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 34f;
            var text = Text("Label", go.transform, label, 13, FontStyle.Bold, Primary(), 30f);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform, 4f, 0f, 4f, 0f);
            return go.GetComponent<Button>();
        }

        private static Toggle Toggle(string label, Transform parent, bool isOn, float width)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Toggle), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 32f;
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
            Text("Label", go.transform, label, 13, FontStyle.Normal, Primary(), 28f);
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
            go.GetComponent<Image>().color = new Color(0.075f, 0.095f, 0.125f, 1f);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 32f;
            var label = Text("Label", go.transform, name, 13, FontStyle.Normal, Primary(), 28f);
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
            rect.anchoredPosition = new Vector2(0f, -34f);
            rect.sizeDelta = new Vector2(0f, 108f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;
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
        private static Color Primary() => new Color(0.92f, 0.94f, 0.96f, 1f);
        private static Color Secondary() => new Color(0.66f, 0.72f, 0.78f, 1f);
        private static Color Muted() => new Color(0.54f, 0.62f, 0.70f, 1f);
    }
}
