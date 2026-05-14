using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TokenForge.Client.Agents;
using TokenForge.Client.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class BootstrapScreenView : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);
        private static readonly Color PanelColor = new Color(0.075f, 0.09f, 0.115f, 0.96f);
        private static readonly Color SectionColor = new Color(0.105f, 0.125f, 0.16f, 0.98f);
        private static readonly Color AccentColor = new Color(0.92f, 0.64f, 0.28f, 1f);
        private static readonly Color PrimaryTextColor = new Color(0.92f, 0.94f, 0.96f, 1f);
        private static readonly Color SecondaryTextColor = new Color(0.66f, 0.72f, 0.78f, 1f);
        private static readonly Color PositiveTextColor = new Color(0.48f, 0.9f, 0.67f, 1f);
        private static readonly Color DisabledButtonColor = new Color(0.2f, 0.22f, 0.25f, 1f);

        private Font defaultFont;
        private LocalClientStatus boundStatus;
        private ApprovedActivityAnalysisViewModel analysisDashboard;

        public void Bind(LocalClientStatus status, ApprovedActivityAnalysisViewModel analysisDashboard = null)
        {
            if (status == null)
            {
                return;
            }

            boundStatus = status;
            this.analysisDashboard = analysisDashboard;
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Rebuild(status);
        }

        private void Rebuild(LocalClientStatus status)
        {
            ClearChildren();
            ConfigureRoot();

            var shell = CreateFrame("Bootstrap Shell", transform, PanelColor);
            ConfigureStretch(shell, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1480f, 960f));
            AddLayout(shell.gameObject, TextAnchor.UpperLeft, 28f, 28f, 28f, 28f, 20f);

            var header = CreateFrame("Header", shell, new Color(0.055f, 0.07f, 0.095f, 1f));
            ConfigureLayoutElement(header.gameObject, -1f, 130f, -1f);
            AddLayout(header.gameObject, TextAnchor.MiddleLeft, 24f, 22f, 24f, 18f, 8f);

            CreateText("Title", header, "TokenForge", 44, FontStyle.Bold, PrimaryTextColor, TextAnchor.MiddleLeft);
            CreateText("Client Status", header, status.ClientStatus, 22, FontStyle.Bold, PositiveTextColor, TextAnchor.MiddleLeft);
            CreateText("Initialized At", header, $"Initialized {status.InitializedAt:yyyy-MM-dd HH:mm:ss zzz}", 16, FontStyle.Normal, SecondaryTextColor, TextAnchor.MiddleLeft);

            var content = CreateFrame("Dashboard Content", shell, new Color(0f, 0f, 0f, 0f));
            ConfigureLayoutElement(content.gameObject, -1f, -1f, 1f);
            AddGrid(content.gameObject);

            foreach (var section in status.Sections)
            {
                CreateSection(content, section);
            }

            if (analysisDashboard != null)
            {
                CreateGitAnalysisPanel(shell);
                CreateAgentAnalysisPanel(shell);
                CreateReviewPanel(shell);
                CreateRecentSessionsPanel(shell);
                CreatePrivacyNoticePanel(shell);
            }

            var debugPanel = CreateFrame("Local Debug Status", shell, new Color(0.05f, 0.06f, 0.08f, 1f));
            ConfigureLayoutElement(debugPanel.gameObject, -1f, 96f, -1f);
            AddLayout(debugPanel.gameObject, TextAnchor.UpperLeft, 20f, 18f, 20f, 16f, 8f);
            CreateText("Debug Title", debugPanel, "Local Debug", 18, FontStyle.Bold, AccentColor, TextAnchor.MiddleLeft);
            CreateText("Debug Lines", debugPanel, BuildDebugText(status), 15, FontStyle.Normal, SecondaryTextColor, TextAnchor.UpperLeft);
        }

        private void ConfigureRoot()
        {
            var rootRect = (RectTransform)transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var image = GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
            }

            image.color = BackgroundColor;
        }

        private void ClearChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private RectTransform CreateFrame(string objectName, Transform parent, Color color)
        {
            var frame = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(parent, false);
            frame.GetComponent<Image>().color = color;
            return frame.GetComponent<RectTransform>();
        }

        private Text CreateText(string objectName, Transform parent, string content, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = defaultFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            var layoutElement = textObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = Mathf.Max(size + 8f, 28f);
            layoutElement.flexibleWidth = 1f;

            return text;
        }

        private void CreateGitAnalysisPanel(Transform parent)
        {
            var gitAnalysisFlow = analysisDashboard.GitFlow;
            var panel = CreateFrame("Git Analysis Panel", parent, new Color(0.05f, 0.06f, 0.08f, 1f));
            ConfigureLayoutElement(panel.gameObject, -1f, 250f, -1f);
            AddLayout(panel.gameObject, TextAnchor.UpperLeft, 20f, 16f, 20f, 16f, 10f);

            CreateText("Git Title", panel, "Git Analysis", 18, FontStyle.Bold, AccentColor, TextAnchor.MiddleLeft);
            CreateText("Git Status", panel, BuildGitStatusText(), 15, FontStyle.Normal, SecondaryTextColor, TextAnchor.UpperLeft);

            var settingsRow = CreateFrame("Git Settings Row", panel, new Color(0f, 0f, 0f, 0f));
            ConfigureLayoutElement(settingsRow.gameObject, -1f, 34f, -1f);
            AddHorizontalLayout(settingsRow.gameObject, 8f);
            var windowInput = CreateInput("Window Days", settingsRow, gitAnalysisFlow.Settings.AnalysisWindowDays.ToString());
            var commitsInput = CreateInput("Max Commits", settingsRow, gitAnalysisFlow.Settings.MaxCommitsToInspect.ToString());
            var uncommittedToggle = CreateToggle("Uncommitted", settingsRow, gitAnalysisFlow.Settings.IncludeUncommittedChanges);
            var commitsToggle = CreateToggle("Commits", settingsRow, gitAnalysisFlow.Settings.IncludeRecentCommits);

            var buttonRow = CreateFrame("Git Button Row", panel, new Color(0f, 0f, 0f, 0f));
            ConfigureLayoutElement(buttonRow.gameObject, -1f, 38f, -1f);
            AddHorizontalLayout(buttonRow.gameObject, 8f);

            CreateButton("Select Repository", buttonRow, true, () => RunDashboardAction(() => gitAnalysisFlow.SelectRepositoryAsync()));
            CreateButton("Analyze Git Activity", buttonRow, CanAnalyzeGit(), () =>
            {
                ApplyGitSettings(windowInput, commitsInput, uncommittedToggle, commitsToggle);
                RunDashboardAction(() => gitAnalysisFlow.AnalyzeAsync());
            });

            var approvedRow = CreateFrame("Git Approved Locations Row", panel, new Color(0f, 0f, 0f, 0f));
            ConfigureLayoutElement(approvedRow.gameObject, -1f, 34f, -1f);
            AddHorizontalLayout(approvedRow.gameObject, 8f);
            var aliasInput = CreateInput("Local alias", approvedRow, "Git repository");
            var approvedDropdown = CreateApprovedLocationDropdown("Approved Git Locations", approvedRow, analysisDashboard.ApprovedGitLocations);
            CreateButton("Approve Selected", approvedRow, gitAnalysisFlow.HasSelectedRepositoryForLocalOnlyApproval, () => RunDashboardAction(() => analysisDashboard.AddCurrentGitSelectionToApprovedLocationsAsync(aliasInput.text)));
            CreateButton("Use Approved", approvedRow, analysisDashboard.ApprovedGitLocations.Any(item => item.Enabled), () =>
            {
                var item = GetSelectedApprovedLocation(analysisDashboard.ApprovedGitLocations, approvedDropdown.value);
                RunDashboardAction(() => analysisDashboard.SelectApprovedGitLocationAsync(item?.LocalId));
            });
            CreateButton("Disable", approvedRow, analysisDashboard.ApprovedGitLocations.Count > 0, () =>
            {
                var item = GetSelectedApprovedLocation(analysisDashboard.ApprovedGitLocations, approvedDropdown.value);
                RunDashboardAction(() => analysisDashboard.DisableApprovedLocationAsync(item?.LocalId));
            });
            CreateButton("Remove", approvedRow, analysisDashboard.ApprovedGitLocations.Count > 0, () =>
            {
                var item = GetSelectedApprovedLocation(analysisDashboard.ApprovedGitLocations, approvedDropdown.value);
                RunDashboardAction(() => analysisDashboard.RemoveApprovedLocationAsync(item?.LocalId));
            });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var devRow = CreateFrame("Git Dev Path Row", panel, new Color(0f, 0f, 0f, 0f));
            ConfigureLayoutElement(devRow.gameObject, -1f, 34f, -1f);
            AddHorizontalLayout(devRow.gameObject, 8f);
            var devInput = CreateInput("Development repository path", devRow, string.Empty);
            CreateButton("Use Dev Path", devRow, true, () =>
            {
                gitAnalysisFlow.SelectDevelopmentRepositoryPath(devInput.text);
                Rebuild(boundStatus);
            });
#endif
        }

        private void CreateAgentAnalysisPanel(Transform parent)
        {
            var panel = CreateFrame("Agent Analysis Panel", parent, new Color(0.05f, 0.06f, 0.08f, 1f));
            ConfigureLayoutElement(panel.gameObject, -1f, 250f, -1f);
            AddLayout(panel.gameObject, TextAnchor.UpperLeft, 20f, 16f, 20f, 16f, 10f);

            CreateText("Agent Title", panel, "AI Agent Log Analysis", 18, FontStyle.Bold, AccentColor, TextAnchor.MiddleLeft);
            CreateText("Agent Status", panel, BuildAgentStatusText(), 15, FontStyle.Normal, SecondaryTextColor, TextAnchor.UpperLeft);

            var settingsRow = CreateFrame("Agent Settings Row", panel, new Color(0f, 0f, 0f, 0f));
            ConfigureLayoutElement(settingsRow.gameObject, -1f, 34f, -1f);
            AddHorizontalLayout(settingsRow.gameObject, 8f);
            var providerDropdown = CreateProviderDropdown(settingsRow, analysisDashboard.SelectedAgentProviderType);
            var windowInput = CreateInput("Window Days", settingsRow, analysisDashboard.AgentSettings.AnalysisWindowDays.ToString());
            var fileLimitInput = CreateInput("Max Files", settingsRow, analysisDashboard.AgentSettings.MaxFilesToScan.ToString());
            var entryLimitInput = CreateInput("Max Entries", settingsRow, analysisDashboard.AgentSettings.MaxLogEntriesToScan.ToString());

            var buttonRow = CreateFrame("Agent Button Row", panel, new Color(0f, 0f, 0f, 0f));
            ConfigureLayoutElement(buttonRow.gameObject, -1f, 38f, -1f);
            AddHorizontalLayout(buttonRow.gameObject, 8f);

            CreateButton("Select Agent Log Folder/File", buttonRow, true, () =>
            {
                ApplyAgentSettings(providerDropdown, windowInput, fileLimitInput, entryLimitInput);
                RunDashboardAction(() => analysisDashboard.SelectAgentLogLocationAsync());
            });
            CreateButton("Analyze Agent Activity", buttonRow, CanAnalyzeAgent(), () =>
            {
                ApplyAgentSettings(providerDropdown, windowInput, fileLimitInput, entryLimitInput);
                RunDashboardAction(() => analysisDashboard.AnalyzeAgentActivityAsync());
            });

            var approvedRow = CreateFrame("Agent Approved Locations Row", panel, new Color(0f, 0f, 0f, 0f));
            ConfigureLayoutElement(approvedRow.gameObject, -1f, 34f, -1f);
            AddHorizontalLayout(approvedRow.gameObject, 8f);
            var aliasInput = CreateInput("Local alias", approvedRow, "Agent logs");
            var approvedDropdown = CreateApprovedLocationDropdown("Approved Agent Locations", approvedRow, analysisDashboard.ApprovedAgentLocations);
            CreateButton("Approve Selected", approvedRow, analysisDashboard.AgentFlow.HasSelectedAgentLogLocationForLocalOnlyApproval, () =>
            {
                ApplyAgentSettings(providerDropdown, windowInput, fileLimitInput, entryLimitInput);
                RunDashboardAction(() => analysisDashboard.AddCurrentAgentSelectionToApprovedLocationsAsync(aliasInput.text));
            });
            CreateButton("Use Approved", approvedRow, analysisDashboard.ApprovedAgentLocations.Any(item => item.Enabled), () =>
            {
                var item = GetSelectedApprovedLocation(analysisDashboard.ApprovedAgentLocations, approvedDropdown.value);
                RunDashboardAction(() => analysisDashboard.SelectApprovedAgentLocationAsync(item?.LocalId));
            });
            CreateButton("Disable", approvedRow, analysisDashboard.ApprovedAgentLocations.Count > 0, () =>
            {
                var item = GetSelectedApprovedLocation(analysisDashboard.ApprovedAgentLocations, approvedDropdown.value);
                RunDashboardAction(() => analysisDashboard.DisableApprovedLocationAsync(item?.LocalId));
            });
            CreateButton("Remove", approvedRow, analysisDashboard.ApprovedAgentLocations.Count > 0, () =>
            {
                var item = GetSelectedApprovedLocation(analysisDashboard.ApprovedAgentLocations, approvedDropdown.value);
                RunDashboardAction(() => analysisDashboard.RemoveApprovedLocationAsync(item?.LocalId));
            });
        }

        private void CreateReviewPanel(Transform parent)
        {
            var panel = CreateFrame("Review Panel", parent, new Color(0.05f, 0.06f, 0.08f, 1f));
            ConfigureLayoutElement(panel.gameObject, -1f, 258f, -1f);
            AddLayout(panel.gameObject, TextAnchor.UpperLeft, 20f, 16f, 20f, 16f, 8f);

            CreateText("Review Title", panel, "Review", 18, FontStyle.Bold, AccentColor, TextAnchor.MiddleLeft);
            CreateText("Review Notice", panel, ApprovedActivityAnalysisViewModel.SafeAggregateNotice + "\n" + ApprovedActivityAnalysisViewModel.RawDataNotice, 15, FontStyle.Bold, PrimaryTextColor, TextAnchor.UpperLeft);
            CreateText("Review Body", panel, BuildReviewText(), 14, FontStyle.Normal, SecondaryTextColor, TextAnchor.UpperLeft);

            var buttonRow = CreateFrame("Review Button Row", panel, new Color(0f, 0f, 0f, 0f));
            ConfigureLayoutElement(buttonRow.gameObject, -1f, 38f, -1f);
            AddHorizontalLayout(buttonRow.gameObject, 8f);

            var gitReady = analysisDashboard.GitFlow.State == GitAnalysisFlowState.ReviewReady;
            var agentReady = analysisDashboard.AgentFlow.State == AgentAnalysisFlowState.ReviewReady;
            CreateButton("Save Session", buttonRow, gitReady, () => RunDashboardAction(() => analysisDashboard.SaveGitSessionAsync()));
            CreateButton("Discard", buttonRow, gitReady, () =>
            {
                analysisDashboard.DiscardGitReview();
                Rebuild(boundStatus);
            });
            CreateButton("Save Session", buttonRow, agentReady && analysisDashboard.AgentFlow.Review.SaveEligible, () => RunDashboardAction(() => analysisDashboard.SaveAgentSessionAsync()));
            CreateButton("Discard", buttonRow, agentReady, () =>
            {
                analysisDashboard.DiscardAgentReview();
                Rebuild(boundStatus);
            });
        }

        private void CreateRecentSessionsPanel(Transform parent)
        {
            var panel = CreateFrame("Recent Safe Sessions Panel", parent, new Color(0.05f, 0.06f, 0.08f, 1f));
            ConfigureLayoutElement(panel.gameObject, -1f, 174f, -1f);
            AddLayout(panel.gameObject, TextAnchor.UpperLeft, 20f, 16f, 20f, 16f, 8f);

            CreateText("Recent Title", panel, "Recent Safe Sessions", 18, FontStyle.Bold, AccentColor, TextAnchor.MiddleLeft);
            CreateText("Recent Sessions", panel, BuildRecentSessionsText(), 14, FontStyle.Normal, SecondaryTextColor, TextAnchor.UpperLeft);
        }

        private void CreatePrivacyNoticePanel(Transform parent)
        {
            var panel = CreateFrame("Privacy Notice Panel", parent, new Color(0.05f, 0.06f, 0.08f, 1f));
            ConfigureLayoutElement(panel.gameObject, -1f, 112f, -1f);
            AddLayout(panel.gameObject, TextAnchor.UpperLeft, 20f, 16f, 20f, 16f, 8f);

            CreateText("Privacy Title", panel, "Privacy Notice", 18, FontStyle.Bold, AccentColor, TextAnchor.MiddleLeft);
            CreateText("Privacy Body", panel, "Analysis runs only after you approve a location and press an analyze button. " + ApprovedActivityAnalysisViewModel.ApprovedLocationsNotice + " " + ApprovedActivityAnalysisViewModel.RawDataNotice, 14, FontStyle.Normal, SecondaryTextColor, TextAnchor.UpperLeft);
        }

        private string BuildGitStatusText()
        {
            var gitAnalysisFlow = analysisDashboard.GitFlow;
            var builder = new StringBuilder();
            builder.Append("State: ");
            builder.Append(gitAnalysisFlow.State);
            builder.Append("   |   ");
            builder.Append(gitAnalysisFlow.SelectionStatus);
            builder.Append("   |   ");
            builder.Append(gitAnalysisFlow.UserMessage);

            if (!string.IsNullOrWhiteSpace(gitAnalysisFlow.ErrorCategory))
            {
                builder.Append("   |   Error: ");
                builder.Append(gitAnalysisFlow.ErrorCategory);
            }

            if (gitAnalysisFlow.Review != null)
            {
                var review = gitAnalysisFlow.Review;
                builder.AppendLine();
                builder.Append("Review: ");
                builder.Append(review.SourceProvider);
                builder.Append(" / ");
                builder.Append(review.SessionAlias);
                builder.Append(" / bucket ");
                builder.Append(review.AnalysisTimeBucket);
                builder.Append(" / files ");
                builder.Append(review.ChangedFilesBucket);
                builder.Append(" / added ");
                builder.Append(review.AddedLinesBucket);
                builder.Append(" / deleted ");
                builder.Append(review.DeletedLinesBucket);
                builder.Append(" / commits ");
                builder.Append(review.CommitCountBucket);
                builder.Append(" / confidence ");
                builder.Append(review.ConfidenceLevel);
                builder.Append(" / warnings ");
                builder.Append(review.PrivacyWarningCount);
                builder.Append(" / stats +EXP ");
                builder.Append(review.DerivedExpGained);

                if (review.ExtensionCategoryBuckets.Count > 0)
                {
                    builder.Append(" / categories ");
                    builder.Append(string.Join(", ", review.ExtensionCategoryBuckets.Take(6).Select(item => item.Category + ":" + item.CountBucket)));
                }
            }

            return builder.ToString();
        }

        private string BuildAgentStatusText()
        {
            var agentFlow = analysisDashboard.AgentFlow;
            var builder = new StringBuilder();
            builder.Append("State: ");
            builder.Append(agentFlow.State);
            builder.Append("   |   ");
            builder.Append(analysisDashboard.AgentSelectionStatus);
            builder.Append("   |   Provider: ");
            builder.Append(analysisDashboard.SelectedAgentProviderType == AgentProviderType.Unknown ? "Unknown/Auto" : analysisDashboard.SelectedAgentProviderType.ToString());
            builder.Append("   |   ");
            builder.Append(agentFlow.UserMessage);

            if (!string.IsNullOrWhiteSpace(agentFlow.ErrorCategory))
            {
                builder.Append("   |   Error: ");
                builder.Append(agentFlow.ErrorCategory);
            }

            if (!string.IsNullOrWhiteSpace(analysisDashboard.AgentPickerErrorCategory))
            {
                builder.Append("   |   Picker: ");
                builder.Append(analysisDashboard.AgentPickerErrorCategory);
            }

            return builder.ToString();
        }

        private string BuildReviewText()
        {
            var builder = new StringBuilder();
            if (analysisDashboard.GitFlow.Review != null)
            {
                var review = analysisDashboard.GitFlow.Review;
                builder.Append("Git: ");
                builder.Append(review.SessionAlias);
                builder.Append(" / day ");
                builder.Append(review.AnalysisTimeBucket);
                builder.Append(" / changes ");
                builder.Append(review.ChangedFilesBucket);
                builder.Append(" / lines +");
                builder.Append(review.AddedLinesBucket);
                builder.Append(" -");
                builder.Append(review.DeletedLinesBucket);
                builder.Append(" / categories ");
                builder.Append(string.Join(", ", review.ExtensionCategoryBuckets.Take(6).Select(item => item.Category + ":" + item.CountBucket)));
                builder.Append(" / confidence ");
                builder.Append(review.ConfidenceLevel);
                builder.Append(" / warning IDs ");
                builder.Append(review.PrivacyWarningCategories.Count == 0 ? "none" : string.Join(", ", review.PrivacyWarningCategories));
                builder.AppendLine();
            }

            if (analysisDashboard.AgentFlow.Review != null)
            {
                var review = analysisDashboard.AgentFlow.Review;
                builder.Append("Agent: ");
                builder.Append(review.ProviderType);
                builder.Append(" / day ");
                builder.Append(review.DayBucket);
                builder.Append(" / sessions ");
                builder.Append(review.SessionCountBucket);
                builder.Append(" / interactions ");
                builder.Append(review.InteractionCountBucket);
                builder.Append(" / tools ");
                builder.Append(string.Join(", ", review.ToolUsageCategoryBuckets.Take(6).Select(item => item.Category + ":" + item.CountBucket)));
                builder.Append(" / languages ");
                builder.Append(string.Join(", ", review.LanguageCategoryBuckets.Take(6).Select(item => item.Category + ":" + item.CountBucket)));
                builder.Append(" / confidence ");
                builder.Append(review.ConfidenceLevel);
                builder.Append(" / warning IDs ");
                builder.Append(review.WarningIds.Count == 0 ? "none" : string.Join(", ", review.WarningIds));
                builder.Append(" / parser ");
                builder.Append(review.AnalyzerVersion);
                builder.Append(" / save ");
                builder.Append(review.SaveEligible ? "eligible" : "blocked");
            }

            return builder.Length == 0 ? "No review ready. Select a location and run analysis to review safe aggregate data before saving." : builder.ToString();
        }

        private string BuildRecentSessionsText()
        {
            if (analysisDashboard.RecentSessions.Count == 0)
            {
                return "No saved safe sessions yet.";
            }

            return string.Join("\n", analysisDashboard.RecentSessions.Select(session =>
            {
                if (session.SourceProvider == "AI_AGENT")
                {
                    return $"{session.DayBucket} / AI agent {session.AgentProviderType} / sessions {session.AgentSessionCountBucket} / interactions {session.AgentInteractionCountBucket} / confidence {session.Confidence}";
                }

                return $"{session.DayBucket} / {session.SourceProvider} / work {session.WorkType} / changes {session.GitChangeCountBucket} / lines +{session.GitAddedLineBucket} -{session.GitDeletedLineBucket} / confidence {session.Confidence}";
            }));
        }

        private bool CanAnalyzeGit()
        {
            var gitAnalysisFlow = analysisDashboard.GitFlow;
            return gitAnalysisFlow.State == GitAnalysisFlowState.Selected ||
                   gitAnalysisFlow.State == GitAnalysisFlowState.Failed;
        }

        private bool CanAnalyzeAgent()
        {
            var state = analysisDashboard.AgentFlow.State;
            return state == AgentAnalysisFlowState.Selected ||
                   state == AgentAnalysisFlowState.Failed;
        }

        private void ApplyGitSettings(InputField windowInput, InputField commitsInput, Toggle uncommittedToggle, Toggle commitsToggle)
        {
            var gitAnalysisFlow = analysisDashboard.GitFlow;
            if (int.TryParse(windowInput.text, out var days))
            {
                gitAnalysisFlow.Settings.AnalysisWindowDays = days;
            }

            if (int.TryParse(commitsInput.text, out var commits))
            {
                gitAnalysisFlow.Settings.MaxCommitsToInspect = commits;
            }

            gitAnalysisFlow.Settings.IncludeUncommittedChanges = uncommittedToggle.isOn;
            gitAnalysisFlow.Settings.IncludeRecentCommits = commitsToggle.isOn;
        }

        private void ApplyAgentSettings(Dropdown providerDropdown, InputField windowInput, InputField fileLimitInput, InputField entryLimitInput)
        {
            analysisDashboard.SelectedAgentProviderType = DropdownIndexToProvider(providerDropdown.value);
            if (int.TryParse(windowInput.text, out var days))
            {
                analysisDashboard.AgentSettings.AnalysisWindowDays = days;
            }

            if (int.TryParse(fileLimitInput.text, out var files))
            {
                analysisDashboard.AgentSettings.MaxFilesToScan = files;
            }

            if (int.TryParse(entryLimitInput.text, out var entries))
            {
                analysisDashboard.AgentSettings.MaxLogEntriesToScan = entries;
            }
        }

        private async void RunDashboardAction(Func<Task> action)
        {
            var task = action();
            Rebuild(boundStatus);

            try
            {
                await task;
            }
            finally
            {
                Rebuild(boundStatus);
            }
        }

        private InputField CreateInput(string objectName, Transform parent, string value)
        {
            var inputObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(parent, false);
            inputObject.GetComponent<Image>().color = new Color(0.08f, 0.095f, 0.12f, 1f);
            ConfigureLayoutElement(inputObject, 180f, 32f, -1f);

            var text = CreateText("Text", inputObject.transform, value, 14, FontStyle.Normal, PrimaryTextColor, TextAnchor.MiddleLeft);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var placeholder = CreateText("Placeholder", inputObject.transform, objectName, 14, FontStyle.Italic, SecondaryTextColor, TextAnchor.MiddleLeft);

            var input = inputObject.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = value;
            return input;
        }

        private Dropdown CreateProviderDropdown(Transform parent, AgentProviderType selectedProvider)
        {
            var dropdownObject = new GameObject("Agent Provider Dropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            dropdownObject.transform.SetParent(parent, false);
            dropdownObject.GetComponent<Image>().color = new Color(0.08f, 0.095f, 0.12f, 1f);
            ConfigureLayoutElement(dropdownObject, 210f, 32f, -1f);

            var caption = CreateText("Label", dropdownObject.transform, "Unknown/Auto", 14, FontStyle.Normal, PrimaryTextColor, TextAnchor.MiddleLeft);
            Stretch(caption.rectTransform, 12f, 0f, 30f, 0f);
            var arrow = CreateText("Arrow", dropdownObject.transform, "v", 14, FontStyle.Bold, SecondaryTextColor, TextAnchor.MiddleCenter);
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(28f, 0f);
            arrow.rectTransform.anchoredPosition = Vector2.zero;

            var template = CreateDropdownTemplate(dropdownObject.transform);
            var itemText = template.Find("Viewport/Content/Item/Item Label").GetComponent<Text>();
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.captionText = caption;
            dropdown.itemText = itemText;
            dropdown.template = template;
            dropdown.options = new System.Collections.Generic.List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("Unknown/Auto"),
                new Dropdown.OptionData("Claude"),
                new Dropdown.OptionData("Codex")
            };
            dropdown.value = ProviderToDropdownIndex(selectedProvider);
            dropdown.onValueChanged.AddListener(value => analysisDashboard.SelectedAgentProviderType = DropdownIndexToProvider(value));
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private Dropdown CreateApprovedLocationDropdown(string objectName, Transform parent, System.Collections.Generic.List<ApprovedLocationDisplayItem> locations)
        {
            var dropdownObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Dropdown));
            dropdownObject.transform.SetParent(parent, false);
            dropdownObject.GetComponent<Image>().color = new Color(0.08f, 0.095f, 0.12f, 1f);
            ConfigureLayoutElement(dropdownObject, 240f, 32f, -1f);

            var caption = CreateText("Label", dropdownObject.transform, "No approved locations", 14, FontStyle.Normal, PrimaryTextColor, TextAnchor.MiddleLeft);
            Stretch(caption.rectTransform, 12f, 0f, 30f, 0f);
            var arrow = CreateText("Arrow", dropdownObject.transform, "v", 14, FontStyle.Bold, SecondaryTextColor, TextAnchor.MiddleCenter);
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(28f, 0f);
            arrow.rectTransform.anchoredPosition = Vector2.zero;

            var template = CreateDropdownTemplate(dropdownObject.transform);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.captionText = caption;
            dropdown.itemText = template.Find("Viewport/Content/Item/Item Label").GetComponent<Text>();
            dropdown.template = template;
            dropdown.options = (locations == null || locations.Count == 0)
                ? new System.Collections.Generic.List<Dropdown.OptionData> { new Dropdown.OptionData("No approved locations") }
                : locations.Select(item => new Dropdown.OptionData(item.DisplayAlias + (item.Enabled ? string.Empty : " (disabled)") + " / " + item.SourceType)).ToList();
            dropdown.value = 0;
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static ApprovedLocationDisplayItem GetSelectedApprovedLocation(System.Collections.Generic.List<ApprovedLocationDisplayItem> locations, int index)
        {
            if (locations == null || index < 0 || index >= locations.Count)
            {
                return null;
            }

            return locations[index];
        }

        private RectTransform CreateDropdownTemplate(Transform parent)
        {
            var template = CreateFrame("Template", parent, new Color(0.08f, 0.095f, 0.12f, 1f));
            template.gameObject.SetActive(false);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -34f);
            template.sizeDelta = new Vector2(0f, 108f);

            var viewport = CreateFrame("Viewport", template, new Color(0f, 0f, 0f, 0f));
            Stretch(viewport, 0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = CreateFrame("Content", viewport, new Color(0f, 0f, 0f, 0f));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 108f);
            AddLayout(content.gameObject, TextAnchor.UpperLeft, 0f, 0f, 0f, 0f, 0f);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content, false);
            ConfigureLayoutElement(item, -1f, 34f, -1f);

            var itemBackground = CreateFrame("Item Background", item.transform, new Color(0.12f, 0.14f, 0.17f, 1f));
            Stretch(itemBackground, 0f, 0f, 0f, 0f);
            var itemLabel = CreateText("Item Label", item.transform, "Option", 14, FontStyle.Normal, PrimaryTextColor, TextAnchor.MiddleLeft);
            Stretch(itemLabel.rectTransform, 12f, 0f, 12f, 0f);

            var toggle = item.GetComponent<Toggle>();
            toggle.targetGraphic = itemBackground.GetComponent<Image>();
            toggle.graphic = itemBackground.GetComponent<Image>();

            var scrollRect = template.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            return template;
        }

        private static int ProviderToDropdownIndex(AgentProviderType providerType)
        {
            switch (providerType)
            {
                case AgentProviderType.Claude: return 1;
                case AgentProviderType.Codex: return 2;
                default: return 0;
            }
        }

        private static AgentProviderType DropdownIndexToProvider(int value)
        {
            switch (value)
            {
                case 1: return AgentProviderType.Claude;
                case 2: return AgentProviderType.Codex;
                default: return AgentProviderType.Unknown;
            }
        }

        private Toggle CreateToggle(string label, Transform parent, bool isOn)
        {
            var toggleObject = new GameObject(label, typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(parent, false);
            ConfigureLayoutElement(toggleObject, 150f, 32f, -1f);
            AddHorizontalLayout(toggleObject, 6f);

            var checkmarkFrame = CreateFrame("Checkmark Frame", toggleObject.transform, new Color(0.08f, 0.095f, 0.12f, 1f));
            ConfigureLayoutElement(checkmarkFrame.gameObject, 28f, 28f, -1f);
            var checkmark = CreateFrame("Checkmark", checkmarkFrame, AccentColor);
            checkmark.anchorMin = new Vector2(0.2f, 0.2f);
            checkmark.anchorMax = new Vector2(0.8f, 0.8f);
            checkmark.offsetMin = Vector2.zero;
            checkmark.offsetMax = Vector2.zero;

            var labelText = CreateText("Label", toggleObject.transform, label, 14, FontStyle.Normal, PrimaryTextColor, TextAnchor.MiddleLeft);
            ConfigureLayoutElement(labelText.gameObject, 110f, 28f, -1f);

            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = checkmarkFrame.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = isOn;
            return toggle;
        }

        private void CreateButton(string label, Transform parent, bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            ConfigureLayoutElement(buttonObject, Mathf.Clamp(label.Length * 9f + 36f, 150f, 280f), 36f, -1f);
            buttonObject.GetComponent<Image>().color = interactable ? AccentColor : DisabledButtonColor;
            var button = buttonObject.GetComponent<Button>();
            button.interactable = interactable;
            button.onClick.AddListener(onClick);
            var text = CreateText("Label", buttonObject.transform, label, 14, FontStyle.Bold, PrimaryTextColor, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6f, 0f, 6f, 0f);
        }

        private void CreateSection(Transform parent, BootstrapSectionStatus section)
        {
            var card = CreateFrame(section.Title, parent, SectionColor);
            AddLayout(card.gameObject, TextAnchor.UpperLeft, 22f, 20f, 22f, 20f, 14f);

            CreateText("Section Title", card, section.Title, 24, FontStyle.Bold, PrimaryTextColor, TextAnchor.MiddleLeft);
            CreateText("Section Status", card, section.Status, 18, FontStyle.Bold, AccentColor, TextAnchor.MiddleLeft);
            CreateText("Section Detail", card, section.Detail, 16, FontStyle.Normal, SecondaryTextColor, TextAnchor.UpperLeft);
        }

        private void ConfigureStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private void AddLayout(GameObject target, TextAnchor childAlignment, float left, float top, float right, float bottom, float spacing)
        {
            var layout = target.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = childAlignment;
            layout.padding = new RectOffset(Mathf.RoundToInt(left), Mathf.RoundToInt(right), Mathf.RoundToInt(top), Mathf.RoundToInt(bottom));
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private void AddHorizontalLayout(GameObject target, float spacing)
        {
            var layout = target.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private void AddGrid(GameObject target)
        {
            var grid = target.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(698f, 214f);
            grid.spacing = new Vector2(24f, 24f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
        }

        private void ConfigureLayoutElement(GameObject target, float preferredWidth, float preferredHeight, float flexibleHeight)
        {
            var layoutElement = target.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f)
            {
                layoutElement.preferredWidth = preferredWidth;
            }

            if (preferredHeight >= 0f)
            {
                layoutElement.preferredHeight = preferredHeight;
            }

            if (flexibleHeight >= 0f)
            {
                layoutElement.flexibleHeight = flexibleHeight;
            }
        }

        private string BuildDebugText(LocalClientStatus status)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < status.DebugLines.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append("   |   ");
                }

                builder.Append(status.DebugLines[i]);
            }

            return builder.ToString();
        }
    }
}
