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
            root.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.06f, 1f);
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
            layout.padding = new RectOffset(0, 0, 28, 28);
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

            var header = Panel("Header", content.transform, 150f);
            Text("TokenForge Title", header.transform, "TokenForge", 36, FontStyle.Bold, Primary(), 44f);
            Text("TokenForge Subtitle", header.transform, "Privacy-safe developer activity companion", 17, FontStyle.Bold, Secondary(), 28f);
            rootView.headerStatusLabel = Text("Header Status", header.transform, "Client status", 15, FontStyle.Bold, Accent(), 28f);
            rootView.validationStatusLabel = Text("Prefab Validation", header.transform, "Prefab UI ready.", 14, FontStyle.Normal, Secondary(), 24f);

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
            var panel = Panel("AccountPanel", null, 252f);
            var view = panel.AddComponent<AccountPanelView>();
            Title(panel, "Account");
            Text("Account Helper", panel.transform, "Login is only required for server sync. Local analysis works without login.", 14, FontStyle.Bold, Primary(), 34f);
            view.statusLabel = Text("Account Status", panel.transform, "Not logged in. Safe Sync actions that change remote data require login.", 13, FontStyle.Normal, Secondary(), 42f);
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
            var panel = Panel("ActivityAnalysisPanel", null, 360f);
            var view = panel.AddComponent<ActivityAnalysisPanelView>();
            Title(panel, "Local Analysis");
            Text("Local Analysis Helper", panel.transform, "Local analysis works without login. Raw local paths and source details stay on this device.", 14, FontStyle.Bold, Primary(), 34f);
            view.gitStatusLabel = Text("Git Status", panel.transform, "No Git repository selected.", 13, FontStyle.Normal, Secondary(), 40f);
            var gitRow = Row("Git Controls", panel.transform, 58f);
            view.gitWindowDaysInput = InputFieldGroup("Git window days", gitRow.transform, "7", 140f);
            view.gitMaxCommitsInput = InputFieldGroup("Max Git commits", gitRow.transform, "200", 150f);
            view.gitIncludeUncommittedToggle = Toggle("Uncommitted", gitRow.transform, true, 150f);
            view.gitIncludeCommitsToggle = Toggle("Commits", gitRow.transform, true, 120f);
            view.selectGitButton = Button("Select Repository", gitRow.transform, 156f);
            view.analyzeGitButton = Button("Analyze Git", gitRow.transform, 136f);
            view.agentStatusLabel = Text("Agent Status", panel.transform, "No agent log location selected.", 13, FontStyle.Normal, Secondary(), 40f);
            var agentRow = Row("Agent Controls", panel.transform, 58f);
            view.agentProviderDropdown = DropdownGroup("Agent provider", agentRow.transform, 170f);
            view.agentWindowDaysInput = InputFieldGroup("Agent window days", agentRow.transform, "7", 150f);
            view.agentMaxFilesInput = InputFieldGroup("Max files", agentRow.transform, "500", 130f);
            view.agentMaxEntriesInput = InputFieldGroup("Max entries", agentRow.transform, "5000", 130f);
            view.selectAgentButton = Button("Select Agent Logs", agentRow.transform, 168f);
            view.analyzeAgentButton = Button("Analyze Agent", agentRow.transform, 148f);
            return panel;
        }

        private static GameObject CreateApprovedLocationsPanel()
        {
            var panel = Panel("ApprovedLocationsPanel", null, 252f);
            var view = panel.AddComponent<ApprovedLocationsPanelView>();
            Title(panel, "Approved Local Locations");
            view.statusLabel = Text("Approved Locations Status", panel.transform, ApprovedActivityAnalysisViewModel.ApprovedLocationsNotice + " No approved locations yet.", 13, FontStyle.Bold, Primary(), 42f);
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
            return panel;
        }

        private static GameObject CreateReviewPanel()
        {
            var panel = Panel("ReviewPanel", null, 216f);
            var view = panel.AddComponent<ReviewPanelView>();
            Title(panel, "Review");
            view.noticeLabel = Text("Review Notice", panel.transform, ApprovedActivityAnalysisViewModel.SafeAggregateNotice, 13, FontStyle.Bold, Primary(), 48f);
            view.reviewLabel = Text("Review Summary", panel.transform, "No review ready.", 13, FontStyle.Normal, Secondary(), 48f);
            var row = Row("Review Controls", panel.transform, 36f);
            view.saveGitButton = Button("Save Git Review", row.transform, 140f);
            view.discardGitButton = Button("Discard Git Review", row.transform, 160f);
            view.saveAgentButton = Button("Save Agent Review", row.transform, 156f);
            view.discardAgentButton = Button("Discard Agent Review", row.transform, 176f);
            return panel;
        }

        private static GameObject CreateSafeSyncPanel()
        {
            var panel = Panel("SafeSyncPanel", null, 250f);
            var view = panel.AddComponent<SafeSyncPanelView>();
            Title(panel, "Safe Sync");
            view.noticeLabel = Text("Safe Sync Notice", panel.transform, "Only aggregate-safe sessions are synced. Health check works while logged out; sync, fetch, and delete require login.", 13, FontStyle.Bold, Primary(), 50f);
            view.statusLabel = Text("Safe Sync Status", panel.transform, "Not logged in. Remote sync actions are disabled until login.", 13, FontStyle.Normal, Secondary(), 44f);
            var row = Row("Safe Sync Controls", panel.transform, 58f);
            view.baseUrlInput = InputFieldGroup("Server base URL", row.transform, "http://localhost:3000/api/v1", 280f);
            view.healthButton = Button("Check Server", row.transform, 132f);
            view.syncButton = Button("Sync Safe Sessions", row.transform, 176f);
            view.fetchButton = Button("Fetch Remote", row.transform, 142f);
            view.remoteSessionsDropdown = DropdownGroup("Remote sessions", row.transform, 250f);
            view.deleteRemoteButton = Button("Delete Remote Session", row.transform, 184f);
            return panel;
        }

        private static GameObject CreateRecentSessionsPanel()
        {
            var panel = Panel("RecentSessionsPanel", null, 204f);
            var view = panel.AddComponent<RecentSessionsPanelView>();
            Title(panel, "Recent Sessions");
            Text("Recent Sessions Helper", panel.transform, "Local sessions are stored on this device. Remote sessions appear only after an explicit fetch.", 13, FontStyle.Bold, Primary(), 34f);
            view.localRecentSessionsLabel = Text("Local Recent Sessions", panel.transform, "Local: no saved safe sessions yet.", 13, FontStyle.Normal, Secondary(), 52f);
            view.remoteSessionsLabel = Text("Remote Sessions", panel.transform, "Remote: no fetched remote safe sessions.", 13, FontStyle.Normal, Secondary(), 52f);
            return panel;
        }

        private static GameObject CreatePrivacyNoticePanel()
        {
            var panel = Panel("PrivacyNoticePanel", null, 138f);
            var view = panel.AddComponent<PrivacyNoticePanelView>();
            Title(panel, "Privacy");
            view.bodyLabel = Text("Privacy Body", panel.transform, "Safe Sync sends aggregate-only session summaries. Approved locations stay on this device and are never synced. Raw local data is never synced.", 14, FontStyle.Bold, Primary(), 70f);
            return panel;
        }

        private static GameObject CreateReviewAndRecentSessionsPanel()
        {
            var panel = Panel("ReviewAndRecentSessionsPanel", null, 384f);
            CreateReviewPanel().transform.SetParent(panel.transform, false);
            CreateRecentSessionsPanel().transform.SetParent(panel.transform, false);
            return panel;
        }

        private static GameObject Panel(string name, Transform parent, float preferredHeight)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            if (parent != null)
            {
                panel.transform.SetParent(parent, false);
            }

            panel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 1f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 8f;
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
            Text("Title", panel.transform, title, 18, FontStyle.Bold, Accent(), 28f);
        }

        private static GameObject Row(string name, Transform parent, float height)
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
            Text(label + " Label", group.transform, label, 12, FontStyle.Bold, Secondary(), 20f);
            return Input(label, group.transform, value, width, contentType);
        }

        private static Dropdown DropdownGroup(string label, Transform parent, float width)
        {
            var group = FieldGroup(label + " Field", parent, width);
            Text(label + " Label", group.transform, label, 12, FontStyle.Bold, Secondary(), 20f);
            return Dropdown(label, group.transform, width);
        }

        private static GameObject FieldGroup(string name, Transform parent, float width)
        {
            var group = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            group.transform.SetParent(parent, false);
            var layout = group.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
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
            go.GetComponent<Image>().color = new Color(0.08f, 0.095f, 0.12f, 1f);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 32f;
            var text = Text("Text", go.transform, value, 13, FontStyle.Normal, Primary(), 28f);
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
            element.preferredHeight = 32f;
            var text = Text("Label", go.transform, label, 13, FontStyle.Bold, Primary(), 28f);
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
            go.GetComponent<Image>().color = new Color(0.08f, 0.095f, 0.12f, 1f);
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

        private static Color Accent() => new Color(0.92f, 0.64f, 0.28f, 1f);
        private static Color Primary() => new Color(0.92f, 0.94f, 0.96f, 1f);
        private static Color Secondary() => new Color(0.66f, 0.72f, 0.78f, 1f);
    }
}
