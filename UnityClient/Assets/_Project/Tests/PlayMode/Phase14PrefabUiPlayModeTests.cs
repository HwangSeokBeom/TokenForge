#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Auth;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase14PrefabWiringPlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapRootPrefabLoadsAndValidates()
        {
            var fixture = Phase14UiFixture.Create();
            yield return null;

            Assert.IsNotNull(fixture.Root);
            Assert.IsTrue(fixture.Root.ValidateReferences(out var error), error);
            Assert.IsNotNull(fixture.Root.accountPanel.loginButton);
            Assert.IsNotNull(fixture.Root.activityAnalysisPanel.analyzeGitButton);
            Assert.IsNotNull(fixture.Root.safeSyncPanel.syncButton);
            Assert.IsNotNull(fixture.Root.privacyNoticePanel.bodyLabel);

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator BrokenPrefabReferenceFailsSafely()
        {
            var fixture = Phase14UiFixture.Create();
            fixture.Root.accountPanel.statusLabel = null;
            fixture.Root.Bind(fixture.Status, fixture.ViewModel);
            yield return null;

            Assert.IsFalse(fixture.Root.ValidateReferences(out var error));
            Assert.IsTrue(error.Contains(nameof(AccountPanelView)));
            Assert.IsTrue(fixture.Root.validationStatusLabel.text.Contains("UI prefab reference error"));

            fixture.Destroy();
        }
    }

    public sealed class Phase14AccountPanelPlayModeTests
    {
        [UnityTest]
        public IEnumerator AccountPanelButtonStatesAndLoginPrivacy()
        {
            var fixture = Phase14UiFixture.Create();
            yield return null;

            Assert.IsTrue(fixture.Root.accountPanel.loginButton.interactable);
            Assert.IsTrue(fixture.Root.accountPanel.signupButton.interactable);
            Assert.IsFalse(fixture.Root.accountPanel.logoutButton.interactable);

            fixture.Root.accountPanel.emailInput.text = "tester@example.com";
            fixture.Root.accountPanel.passwordInput.text = "password123";
            fixture.Root.accountPanel.loginButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, fixture.Auth.LoginCount);
            Assert.AreEqual(0, fixture.Sync.SyncCount);
            Assert.AreEqual(string.Empty, fixture.Root.accountPanel.passwordInput.text);
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "password123"));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "access-token-secret"));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "refresh-token-secret"));

            fixture.Root.accountPanel.logoutButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, fixture.Auth.LogoutCount);
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.selectGitButton.interactable);

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator AccountPanelDisablesSubmitWhileLoginInProgress()
        {
            var fixture = Phase14UiFixture.Create(slowLogin: true);
            fixture.Root.accountPanel.emailInput.text = "tester@example.com";
            fixture.Root.accountPanel.passwordInput.text = "password123";

            fixture.Root.accountPanel.loginButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(fixture.Root.accountPanel.loginButton.interactable);
            Assert.IsFalse(fixture.Root.accountPanel.signupButton.interactable);

            fixture.Auth.CompleteLogin();
            yield return null;
            yield return null;

            Assert.AreEqual(string.Empty, fixture.Root.accountPanel.passwordInput.text);
            Assert.AreEqual(0, fixture.Sync.SyncCount);

            fixture.Destroy();
        }
    }

    public sealed class Phase14SafeSyncPanelPlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeSyncStatesAndExplicitSyncOnly()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: false);
            yield return null;

            Assert.IsTrue(fixture.Root.safeSyncPanel.healthButton.interactable);
            Assert.IsFalse(fixture.Root.safeSyncPanel.syncButton.interactable);
            Assert.IsFalse(fixture.Root.safeSyncPanel.fetchButton.interactable);

            fixture.Root.safeSyncPanel.healthButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, fixture.Sync.HealthCount);

            fixture.Auth.SetLoggedIn();
            fixture.Root.Render();
            Assert.IsTrue(fixture.Root.safeSyncPanel.syncButton.interactable);
            Assert.IsTrue(fixture.Root.safeSyncPanel.fetchButton.interactable);

            fixture.Root.safeSyncPanel.syncButton.onClick.Invoke();
            fixture.Root.safeSyncPanel.syncButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, fixture.Sync.SyncCount);
            Assert.IsFalse(fixture.Root.safeSyncPanel.syncButton.interactable);

            fixture.Sync.CompleteSync(accepted: 2, rejected: 1);
            yield return null;
            yield return null;

            Assert.IsTrue(fixture.Root.safeSyncPanel.statusLabel.text.Contains("Accepted 2 / rejected 1"));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "{\""));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "access-token-secret"));

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator SafeSyncServerUnavailableRendersSafeMessage()
        {
            var fixture = Phase14UiFixture.Create();
            fixture.Sync.NextHealthResult = SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SERVER_UNAVAILABLE", "{\"token\":\"secret\",\"path\":\"/Users/alice/private\"}");

            fixture.Root.safeSyncPanel.healthButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(fixture.Root.safeSyncPanel.statusLabel.text.Contains("Server"));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "{\"token\""));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "/Users/alice/private"));

            fixture.Destroy();
        }
    }

    public sealed class Phase14ActivityAnalysisPanelPlayModeTests
    {
        [UnityTest]
        public IEnumerator ActivityButtonsApprovedLocationsAndReviewFlow()
        {
            var fixture = Phase14UiFixture.Create();
            yield return null;

            Assert.IsFalse(fixture.Root.activityAnalysisPanel.analyzeGitButton.interactable);
            Assert.IsFalse(fixture.Root.activityAnalysisPanel.analyzeAgentButton.interactable);

            fixture.Root.activityAnalysisPanel.selectGitButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.analyzeGitButton.interactable);

            fixture.Root.approvedLocationsPanel.gitAliasInput.text = "Work repo";
            fixture.Root.approvedLocationsPanel.approveGitButton.onClick.Invoke();
            yield return fixture.WaitUntil(() => fixture.ViewModel.ApprovedGitLocations.Count > 0);
            fixture.Root.Render();
            Assert.IsTrue(
                fixture.Root.approvedLocationsPanel.gitLocationsDropdown.options[0].text.Contains("Work repo"),
                fixture.Root.approvedLocationsPanel.gitLocationsDropdown.options[0].text);
            Assert.IsFalse(fixture.Root.approvedLocationsPanel.gitLocationsDropdown.options[0].text.Contains(fixture.RawRepositoryPath));

            fixture.Root.activityAnalysisPanel.analyzeGitButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsTrue(fixture.Root.reviewPanel.saveGitButton.interactable);
            fixture.Root.reviewPanel.discardGitButton.onClick.Invoke();
            yield return null;
            Assert.IsFalse(fixture.Root.reviewPanel.saveGitButton.interactable);

            fixture.Root.activityAnalysisPanel.agentProviderDropdown.value = 1;
            fixture.Root.activityAnalysisPanel.selectAgentButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.analyzeAgentButton.interactable);
            fixture.Root.approvedLocationsPanel.agentAliasInput.text = "Claude logs";
            fixture.Root.approvedLocationsPanel.approveAgentButton.onClick.Invoke();
            yield return fixture.WaitUntil(() => fixture.ViewModel.ApprovedAgentLocations.Count > 0);
            fixture.Root.Render();
            Assert.IsTrue(
                fixture.Root.approvedLocationsPanel.agentLocationsDropdown.options[0].text.Contains("Claude logs"),
                fixture.Root.approvedLocationsPanel.agentLocationsDropdown.options[0].text);
            Assert.IsFalse(fixture.Root.approvedLocationsPanel.agentLocationsDropdown.options[0].text.Contains(fixture.RawAgentLogPath));

            fixture.Destroy();
        }
    }

    public sealed class Phase14PrivacyUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator PrivacyNoticeAndSessionListsExposeOnlySafeSummaries()
        {
            var fixture = Phase14UiFixture.Create();
            fixture.Repository.Current.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SourceProvider = "AI_AGENT",
                StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
                EndedAt = DateTimeOffset.UtcNow,
                Confidence = ProviderConfidence.High,
                AgentActivitySummary = new AgentActivitySummary
                {
                    ProviderType = AgentProviderType.Codex,
                    DayBucket = "2026-05-15",
                    SessionCountBucket = CountBucket.One,
                    InteractionCountBucket = CountBucket.Small
                }
            });
            yield return fixture.RefreshDashboard();
            fixture.Sync.NextFetchResult = new SafeSyncResult
            {
                IsSuccess = true,
                Status = SafeSyncStatus.Ready,
                RemoteSessions = new List<RemoteSafeSessionSummary>
                {
                    new RemoteSafeSessionSummary
                    {
                        ServerSessionId = "11111111-1111-4111-8111-111111111111",
                        SourceProvider = "CODEX",
                        DayBucket = "2026-05-15",
                        ActivityCategory = "WORK_FEATURE",
                        ChangeCountBucket = "SMALL",
                        LineCountBucket = "SMALL",
                        SessionCountBucket = "ONE",
                        InteractionCountBucket = "SMALL",
                        Confidence = "HIGH",
                        SchemaVersion = 1
                    }
                }
            };
            fixture.Root.safeSyncPanel.fetchButton.onClick.Invoke();
            yield return null;
            yield return null;

            var privacy = fixture.Root.privacyNoticePanel.bodyLabel.text;
            Assert.IsTrue(privacy.Contains("aggregate-only"));
            Assert.IsTrue(privacy.Contains("Approved locations are stored only on this device"));
            Assert.IsTrue(privacy.Contains("Raw local data is never synced"));

            UiTextScanner.AssertNoForbiddenVisibleText(fixture.Root.gameObject, new[]
            {
                "password123",
                "access-token-secret",
                "refresh-token-secret",
                fixture.RawRepositoryPath,
                fixture.RawAgentLogPath,
                "SecretRepo",
                "feature/customer-token",
                "git status --short",
                "please inspect the production auth token flow",
                "the response includes customer secret handling",
                "public class Secret",
                "apiKey"
            });

            fixture.Destroy();
        }
    }

    internal sealed class Phase14UiFixture
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/BootstrapRoot.prefab";

        public BootstrapRootView Root { get; private set; }
        public LocalClientStatus Status { get; private set; }
        public ApprovedActivityAnalysisViewModel ViewModel { get; private set; }
        public FakeAuthSessionService Auth { get; private set; }
        public FakeSafeSyncService Sync { get; private set; }
        public FakeRepository Repository { get; private set; }
        public string RawRepositoryPath { get; private set; }
        public string RawAgentLogPath { get; private set; }

        private GameObject canvasObject;

        public static Phase14UiFixture Create(bool loggedIn = false, bool slowLogin = false)
        {
            var fixture = new Phase14UiFixture();
            fixture.RawRepositoryPath = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName(), "SecretRepo");
            Directory.CreateDirectory(fixture.RawRepositoryPath);
            fixture.RawAgentLogPath = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName(), "SecretRepo", "claude-private.jsonl");
            fixture.Repository = new FakeRepository();
            fixture.Auth = new FakeAuthSessionService(slowLogin);
            if (loggedIn)
            {
                fixture.Auth.SetLoggedIn();
            }

            fixture.Sync = new FakeSafeSyncService();
            var privacy = new PrivacySanitizer();
            fixture.ViewModel = new ApprovedActivityAnalysisViewModel(
                new GitAnalysisFlowController(
                    new FakeRepositoryPicker(fixture.RawRepositoryPath),
                    new GitAggregateAnalyzer(new FakeGitRunner(), privacy),
                    fixture.Repository,
                    null,
                    privacy),
                new AgentAnalysisFlowController(
                    new AgentLogActivityProvider(new AgentActivityAnalyzer(new FakeAgentLogReader(), null, privacy)),
                    fixture.Repository,
                    null,
                    privacy),
                new FakeAgentLogLocationPicker(fixture.RawAgentLogPath),
                fixture.Repository,
                privacy,
                new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName())),
                fixture.Sync,
                fixture.Auth);
            fixture.Status = LocalClientStatus.CreateInitialized();
            fixture.canvasObject = new GameObject("Phase14 Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            fixture.canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "BootstrapRoot prefab missing at " + PrefabPath);
            var instance = UnityEngine.Object.Instantiate(prefab, fixture.canvasObject.transform, false);
            fixture.Root = instance.GetComponent<BootstrapRootView>();
            fixture.Root.Bind(fixture.Status, fixture.ViewModel);
            return fixture;
        }

        public IEnumerator RefreshDashboard()
        {
            var task = ViewModel.RefreshDashboardAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            Root.Bind(Status, ViewModel);
            yield return null;
        }

        public IEnumerator WaitUntil(Func<bool> predicate)
        {
            for (var i = 0; i < 120; i++)
            {
                if (predicate())
                {
                    yield break;
                }

                yield return null;
            }
        }

        public void Destroy()
        {
            if (canvasObject != null)
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }
    }

    internal sealed class FakeAuthSessionService : IAuthSessionService
    {
        private readonly bool slowLogin;
        private TaskCompletionSource<AuthResult> loginCompletion;

        public FakeAuthSessionService(bool slowLogin = false)
        {
            this.slowLogin = slowLogin;
        }

        public int LoginCount { get; private set; }
        public int SignupCount { get; private set; }
        public int LogoutCount { get; private set; }
        public AuthState State { get; private set; } = AuthState.LoggedOut;
        public AuthSession CurrentSession { get; private set; }
        public string ErrorCode { get; private set; } = string.Empty;
        public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
        public bool HasUsableAccessToken => State == AuthState.LoggedIn && CurrentSession != null;

        public void SetBaseUrl(string baseUrl)
        {
            BaseUrl = baseUrl;
        }

        public void SetLoggedIn()
        {
            State = AuthState.LoggedIn;
            CurrentSession = new AuthSession
            {
                UserId = "user-1",
                Email = "tester@example.com",
                DisplayName = "Tester",
                AccessToken = "access-token-secret",
                RefreshToken = "refresh-token-secret",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
            };
        }

        public void CompleteLogin()
        {
            SetLoggedIn();
            loginCompletion?.TrySetResult(AuthResult.Success(State));
        }

        public Task<AuthResult> LoadSessionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AuthResult.Success(State));
        }

        public Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            LoginCount += 1;
            if (slowLogin)
            {
                State = AuthState.LoggingIn;
                loginCompletion = new TaskCompletionSource<AuthResult>();
                return loginCompletion.Task;
            }

            SetLoggedIn();
            CurrentSession.Email = email;
            return Task.FromResult(AuthResult.Success(State));
        }

        public Task<AuthResult> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default)
        {
            SignupCount += 1;
            SetLoggedIn();
            CurrentSession.Email = email;
            CurrentSession.DisplayName = displayName;
            return Task.FromResult(AuthResult.Success(State));
        }

        public Task<AuthResult> LoadCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(State == AuthState.LoggedIn
                ? AuthResult.Success(State)
                : AuthResult.Failure(AuthState.AuthRequired, AuthApiError.AuthRequired, "Log in to use server sync."));
        }

        public Task<AuthResult> LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCount += 1;
            State = AuthState.LoggedOut;
            CurrentSession = null;
            return Task.FromResult(AuthResult.Success(State));
        }

        public Task<string> GetAccessTokenForSyncAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HasUsableAccessToken ? "access-token-secret" : string.Empty);
        }

        public Task<string> ForceRefreshForRetryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Empty);
        }
    }

    internal sealed class FakeSafeSyncService : ISafeSyncService
    {
        private TaskCompletionSource<SafeSyncResult> syncCompletion;

        public int HealthCount { get; private set; }
        public int SyncCount { get; private set; }
        public int FetchCount { get; private set; }
        public SafeSyncStatus Status { get; private set; } = SafeSyncStatus.Idle;
        public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
        public SafeSyncResult NextHealthResult { get; set; } = SafeSyncResult.Success(SafeSyncStatus.Ready);
        public SafeSyncResult NextFetchResult { get; set; } = SafeSyncResult.Success(SafeSyncStatus.Ready);

        public void SetBaseUrl(string baseUrl)
        {
            BaseUrl = baseUrl;
        }

        public Task<SafeSyncResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            HealthCount += 1;
            Status = NextHealthResult.Status;
            return Task.FromResult(NextHealthResult);
        }

        public Task<SafeSyncResult> SyncNowAsync(CancellationToken cancellationToken = default)
        {
            SyncCount += 1;
            Status = SafeSyncStatus.Syncing;
            syncCompletion = new TaskCompletionSource<SafeSyncResult>();
            return syncCompletion.Task;
        }

        public void CompleteSync(int accepted, int rejected)
        {
            Status = SafeSyncStatus.Synced;
            syncCompletion?.TrySetResult(new SafeSyncResult
            {
                IsSuccess = true,
                Status = SafeSyncStatus.Synced,
                AcceptedCount = accepted,
                RejectedCount = rejected
            });
        }

        public Task<SafeSyncResult> FetchRemoteSessionsAsync(CancellationToken cancellationToken = default)
        {
            FetchCount += 1;
            Status = NextFetchResult.Status;
            return Task.FromResult(NextFetchResult);
        }

        public Task<SafeSyncResult> DeleteRemoteSessionAsync(string serverSessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
        }
    }

    internal sealed class FakeRepository : ILocalSaveDataRepository
    {
        public SaveData Current { get; set; } = SaveData.CreateDefault();
        public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            Current = saveData;
            return Task.FromResult(Result.Success());
        }
    }

    internal sealed class FakeRepositoryPicker : IRepositoryPicker
    {
        private readonly string path;
        public FakeRepositoryPicker(string path) => this.path = path;
        public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default) => Task.FromResult(RepositoryPickerResult.Selected(path));
    }

    internal sealed class FakeAgentLogLocationPicker : IAgentLogLocationPicker
    {
        private readonly string path;
        public FakeAgentLogLocationPicker(string path) => this.path = path;
        public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default) => Task.FromResult(AgentLogLocationPickerResult.Selected(path));
    }

    internal sealed class FakeGitRunner : IGitCommandRunner
    {
        public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
        {
            if (arguments == "rev-parse --is-inside-work-tree")
            {
                return Task.FromResult(GitCommandResult.Success("true\n"));
            }

            if (arguments == "status --porcelain")
            {
                return Task.FromResult(GitCommandResult.Success(" M src/ui/View.cs\n"));
            }

            if (arguments == "diff --numstat" || arguments == "diff --cached --numstat")
            {
                return Task.FromResult(GitCommandResult.Success("12\t3\tsrc/ui/View.cs\n"));
            }

            if (arguments.StartsWith("log --since=", StringComparison.Ordinal))
            {
                return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n8\t2\tsrc/app/App.cs\n"));
            }

            return Task.FromResult(GitCommandResult.Failure("unsupported_git_command", "Unsupported test command."));
        }
    }

    internal sealed class FakeAgentLogReader : IAgentLogSourceReader
    {
        public Task<AgentLogReadResult> ReadAsync(AgentAnalysisInput input, CancellationToken cancellationToken)
        {
            var entries = new List<AgentLogEntry>
            {
                new AgentLogEntry
                {
                    Text = "{\"provider\":\"claude\",\"timestamp\":\"2026-05-15T01:00:00Z\",\"session_id\":\"session-1\",\"type\":\"message\",\"role\":\"user\",\"content\":\"safe aggregate fixture\"}",
                    LastWriteTimeUtc = new DateTimeOffset(2026, 5, 15, 1, 0, 0, TimeSpan.Zero)
                }
            };

            return Task.FromResult(AgentLogReadResult.Success(entries, new List<string>()));
        }
    }

    internal static class UiTextScanner
    {
        public static bool VisibleTextContains(GameObject root, string value)
        {
            return root.GetComponentsInChildren<Text>(true).Any(text => text.text != null && text.text.Contains(value));
        }

        public static void AssertNoForbiddenVisibleText(GameObject root, IEnumerable<string> forbiddenValues)
        {
            var visibleText = string.Join("\n", root.GetComponentsInChildren<Text>(true).Select(text => text.text ?? string.Empty));
            foreach (var forbidden in forbiddenValues.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                Assert.IsFalse(visibleText.Contains(forbidden), "Visible UI text exposed forbidden value: " + forbidden);
            }
        }
    }
}
#endif
