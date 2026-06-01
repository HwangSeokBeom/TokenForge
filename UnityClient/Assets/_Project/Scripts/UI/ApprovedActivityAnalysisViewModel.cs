using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Agents;
using TokenForge.Client.Auth;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;
using UnityEngine;

namespace TokenForge.Client.UI
{
    public sealed class AgentAnalysisSettings
    {
        public int AnalysisWindowDays { get; set; } = AgentAnalysisInput.DefaultAnalysisWindowDays;
        public int MaxFilesToScan { get; set; } = AgentAnalysisInput.DefaultMaxFilesToScan;
        public int MaxLogEntriesToScan { get; set; } = AgentAnalysisInput.DefaultMaxLogEntriesToScan;

        public AgentAnalysisInput ToInput(string selectedLocationPath, AgentProviderType providerType)
        {
            return new AgentAnalysisInput
            {
                SelectedLocationPath = selectedLocationPath ?? string.Empty,
                ProviderHint = providerType,
                AnalysisWindowDays = AnalysisWindowDays,
                MaxFilesToScan = MaxFilesToScan,
                MaxLogEntriesToScan = MaxLogEntriesToScan
            };
        }
    }

    public sealed class RecentSafeSessionSummary
    {
        public string ClientSessionId { get; set; } = string.Empty;
        public string SourceProvider { get; set; } = string.Empty;
        public string DayBucket { get; set; } = string.Empty;
        public WorkType WorkType { get; set; } = WorkType.Unknown;
        public ProviderConfidence Confidence { get; set; } = ProviderConfidence.Unknown;
        public CountBucket GitChangeCountBucket { get; set; } = CountBucket.Unknown;
        public LineChangeBucket GitAddedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public LineChangeBucket GitDeletedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public AgentProviderType AgentProviderType { get; set; } = AgentProviderType.Unknown;
        public CountBucket AgentSessionCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket AgentInteractionCountBucket { get; set; } = CountBucket.Unknown;
        public List<string> WarningIds { get; set; } = new List<string>();
        public int ExpGained { get; set; }
        public string TopStatCategory { get; set; } = string.Empty;
    }

    public sealed class ApprovedLocationDisplayItem
    {
        public string LocalId { get; set; } = string.Empty;
        public string DisplayAlias { get; set; } = string.Empty;
        public ApprovedLocationSourceType SourceType { get; set; } = ApprovedLocationSourceType.UnknownAuto;
        public bool Enabled { get; set; } = true;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public sealed class RepositoryCompanionDisplayItem
    {
        public string RepositoryHash { get; set; } = string.Empty;
        public string SafeRepositoryAlias { get; set; } = "Repository";
        public CompanionStage Stage { get; set; } = CompanionStage.Egg;
        public CompanionArchetype Archetype { get; set; } = CompanionArchetype.Unknown;
        public int Level { get; set; } = 1;
        public int TotalXp { get; set; }
        public int LifetimeGrowthXp { get; set; }
        public int WeeklyGrowthXp { get; set; }
        public int CurrentXp { get; set; }
        public int XpRequiredForNextLevel { get; set; } = 250;
        public bool CanLevelUp { get; set; }
        public string RecentGrowthSource { get; set; } = "None";
        public int RecentGitXp { get; set; }
        public int RecentAiXp { get; set; }
        public TokenUsageBucket EstimatedTokenActivity { get; set; } = TokenUsageBucket.Unknown;
        public string DominantStat { get; set; } = "Unknown";
        public string SecondaryStat { get; set; } = "Unknown";
        public string EvolutionPath { get; set; } = "Unknown";
        public string NextEvolutionPreview { get; set; } = "Repository Hatchling";
        public int TokenCurrencyBalance { get; set; }
        public string TokenCurrencyName { get; set; } = "Forge Coins";
        public List<string> PurchasedTokenShopItemIds { get; set; } = new List<string>();
        public string Skin { get; set; } = CompanionSkinCatalog.DefaultSkinId;
        public string CompanionId { get; set; } = string.Empty;
        public bool DesktopCompanionEnabled { get; set; } = true;
        public bool HasSavedOverlayPosition { get; set; }
        public float OverlayPositionX { get; set; } = -1f;
        public float OverlayPositionY { get; set; } = -1f;
        public CompanionMotionState MotionState { get; set; } = CompanionMotionState.Idle(string.Empty);
        public bool ApprovedByUser { get; set; }
        public DateTimeOffset? ApprovedAtUtc { get; set; }
        public string LastApprovedActivityBucket { get; set; } = string.Empty;
        public bool Selected { get; set; }
        public bool Archived { get; set; }
    }

    public sealed class CharacterDashboardSummary
    {
        public string CharacterName { get; set; } = "Token";
        public int Level { get; set; } = 1;
        public int TotalExp { get; set; }
        public int CurrentLevelExp { get; set; }
        public int ExpForNextLevel { get; set; } = 1000;
        public string RankTitle { get; set; } = "Local Apprentice";
        public string CurrentRepositoryHash { get; set; } = string.Empty;
        public string CurrentRepositoryAlias { get; set; } = string.Empty;
        public int Code { get; set; }
        public int Focus { get; set; }
        public int Debug { get; set; }
        public int Design { get; set; }
        public int Sync { get; set; }
        public int WeeklyCode { get; set; }
        public int WeeklyFocus { get; set; }
        public int WeeklyDebug { get; set; }
        public int WeeklyDesign { get; set; }
        public int WeeklySync { get; set; }
        public string DominantGrowthPath { get; set; } = "Unknown";
        public string SecondaryGrowthTrait { get; set; } = "Unknown";
        public string CurrentEvolutionBias { get; set; } = "Unknown";
        public string NextEvolutionPreview { get; set; } = "Repository Hatchling";
        public string EggInfluenceText { get; set; } = "아직 부화 전이에요. 최근 Git 성장 성향이 미래 진화 방향에 영향을 줍니다.";
        public string TokenCurrencyName { get; set; } = "Forge Coins";
        public int TokenCurrencyBalance { get; set; }
        public bool TokenUsageTrackingEnabled { get; set; } = true;
        public bool HasSavedRun { get; set; }
        public CompanionState CompanionState { get; set; } = CompanionState.CreateDefault();
        public DesktopCompanionSettings DesktopCompanionSettings { get; set; } = DesktopCompanionSettings.CreateDefault();
        public CompanionDesktopOverlayState DesktopOverlayState { get; set; } = CompanionDesktopOverlayState.Disabled;
        public CompanionMotionState MotionState { get; set; } = CompanionMotionState.Idle(string.Empty);
        public string LatestSafeSessionSummary { get; set; } = "No saved growth yet. Run Analysis on a repository or AI agent log to generate your first XP.";
        public string RecentGrowthSummary { get; set; } = "No growth recorded yet.";
        public string QuestSummary { get; set; } = "Analyze repository: open | Save session: open | Sync progress: login required";
        public string ActivityLogSummary { get; set; } = "No run saved yet.\nConnect a Git repository or AI Agent log, review the safe aggregate, then save it to gain XP.";
        public List<RepositoryCompanionDisplayItem> RepositoryCompanions { get; set; } = new List<RepositoryCompanionDisplayItem>();
    }

    public enum OnboardingStep
    {
        Account,
        AiAgents,
        Git,
        Ready
    }

    public enum ConnectedAgentSourceType
    {
        Cursor,
        ClaudeCode,
        Codex,
        GitHubCopilot,
        GeminiCli,
        OtherManualLogFolder
    }

    public enum AgentSourceSetupState
    {
        NotSelected,
        Selected,
        DetectingLocalSource,
        LocalSourceDetected,
        PermissionRequired,
        ManualImportRequired,
        ReadyToAnalyze,
        AnalysisComplete,
        AnalysisFailedSafely
    }

    public sealed class ConnectedAgentSource
    {
        public ConnectedAgentSourceType SourceType { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool Selected { get; set; }
        public AgentSourceSetupState State { get; set; } = AgentSourceSetupState.NotSelected;
        public string StatusLabel { get; set; } = "Not selected";
        public string SafeLabel { get; set; } = string.Empty;
        public string SafeLocationHash { get; set; } = string.Empty;
        public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Unknown;
        public DateTimeOffset? LastScanTimeUtc { get; set; }
        public int WarningCount { get; set; }
    }

    public sealed class OnboardingState
    {
        public OnboardingStep CurrentStep { get; set; } = OnboardingStep.Account;
        public bool OfflineModeSelected { get; set; }
        public bool GitSkipped { get; set; }
        public bool GitConnected { get; set; }
        public bool GitAccountPlaceholderSelected { get; set; }
        public string GitSafeAlias { get; set; } = string.Empty;
        public List<ConnectedAgentSource> AgentSources { get; } = new List<ConnectedAgentSource>
        {
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.Codex, DisplayName = "Codex" },
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.ClaudeCode, DisplayName = "Claude Code" },
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.Cursor, DisplayName = "Cursor" },
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.GitHubCopilot, DisplayName = "GitHub Copilot" },
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.GeminiCli, DisplayName = "Gemini CLI" },
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.OtherManualLogFolder, DisplayName = "Other / Manual Log Folder", State = AgentSourceSetupState.ManualImportRequired, StatusLabel = "Manual import required" }
        };
    }

    public sealed class ApprovedActivityAnalysisViewModel
    {
        public const string SafeAggregateNotice = "Only safe aggregate data will be saved.";
        public const string RawDataNotice = "Private local details are not saved or synced.";
        public const string ApprovedLocationsNotice = "Approved locations are stored only on this device and are never synced.";

        private const int RecentSessionLimit = 8;

        private readonly IAgentLogLocationPicker agentLogLocationPicker;
        private readonly ILocalSaveDataRepository repository;
        private readonly IApprovedLocationSettingsRepository approvedLocationRepository;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly ISafeSyncService safeSyncService;
        private readonly IAuthSessionService authSessionService;
        private readonly Func<AgentProviderType, IAgentSourceDetector> agentSourceDetectorFactory;
        private int loginInProgress;
        private int signupInProgress;
        private int refreshUserInProgress;
        private int logoutInProgress;
        private int healthInProgress;
        private int syncInProgress;
        private int fetchInProgress;
        private int deleteInProgress;
        private int retryInProgress;
        private int localDeleteInProgress;
        private int tombstoneInProgress;
        private int conflictInProgress;
        private Func<CancellationToken, Task<SafeSyncResult>> pendingSafeSyncConfirmationAction;
        private readonly Dictionary<ConnectedAgentSourceType, AgentSourceCandidate> approvedAgentCandidates = new Dictionary<ConnectedAgentSourceType, AgentSourceCandidate>();

        public ApprovedActivityAnalysisViewModel(
            GitAnalysisFlowController gitFlow,
            AgentAnalysisFlowController agentFlow,
            IAgentLogLocationPicker agentLogLocationPicker,
            ILocalSaveDataRepository repository,
            PrivacySanitizer privacySanitizer = null,
            IApprovedLocationSettingsRepository approvedLocationRepository = null,
            ISafeSyncService safeSyncService = null,
            IAuthSessionService authSessionService = null,
            Func<AgentProviderType, IAgentSourceDetector> agentSourceDetectorFactory = null)
        {
            GitFlow = gitFlow ?? throw new ArgumentNullException(nameof(gitFlow));
            AgentFlow = agentFlow ?? throw new ArgumentNullException(nameof(agentFlow));
            this.agentLogLocationPicker = agentLogLocationPicker ?? throw new ArgumentNullException(nameof(agentLogLocationPicker));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.approvedLocationRepository = approvedLocationRepository ?? new ApprovedLocationSettingsRepository();
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.safeSyncService = safeSyncService;
            this.authSessionService = authSessionService;
            this.agentSourceDetectorFactory = agentSourceDetectorFactory ?? (providerType => new MacAgentSourceDetector(providerType));
        }

        public GitAnalysisFlowController GitFlow { get; }
        public AgentAnalysisFlowController AgentFlow { get; }
        public AgentAnalysisSettings AgentSettings { get; } = new AgentAnalysisSettings();
        public OnboardingState Onboarding { get; } = new OnboardingState();
        public AgentProviderType SelectedAgentProviderType { get; set; } = AgentProviderType.Unknown;
        public string AgentSelectionStatus { get; private set; } = "No agent log location selected";
        public string AgentPickerErrorCategory { get; private set; } = string.Empty;
        public List<RecentSafeSessionSummary> RecentSessions { get; private set; } = new List<RecentSafeSessionSummary>();
        public List<NativeAnalysisRunRecord> RecentNativeAnalysisRuns { get; private set; } = new List<NativeAnalysisRunRecord>();
        public List<ApprovedLocationDisplayItem> ApprovedGitLocations { get; private set; } = new List<ApprovedLocationDisplayItem>();
        public List<ApprovedLocationDisplayItem> ApprovedAgentLocations { get; private set; } = new List<ApprovedLocationDisplayItem>();
        public List<RepositoryCompanionDisplayItem> RepositoryCompanions { get; private set; } = new List<RepositoryCompanionDisplayItem>();
        public List<RemoteSafeSessionSummary> RemoteSafeSessions { get; private set; } = new List<RemoteSafeSessionSummary>();
        public CharacterDashboardSummary CharacterDashboard { get; private set; } = new CharacterDashboardSummary();
        public SaveData CurrentSaveData { get; private set; }
        public PendingNativeActivityReview PendingNativeActivityReview { get; private set; }
        public SafeSyncRetryQueueSummary RetryQueueSummary { get; private set; } = new SafeSyncRetryQueueSummary();
        public SafeSyncConflictSummary ConflictSummary { get; private set; } = new SafeSyncConflictSummary();
        public SafeConflictAuditSummary ConflictAuditSummary { get; private set; } = new SafeConflictAuditSummary();
        public SafeSyncTombstoneSummary TombstoneSummary { get; private set; } = new SafeSyncTombstoneSummary();
        public SafeSyncConfirmationRequest PendingSafeSyncConfirmation { get; private set; }
        public string SafeSyncConfirmationTypedPhrase { get; set; } = string.Empty;
        public bool HasPendingSafeSyncConfirmation => PendingSafeSyncConfirmation != null;
        public SafeSyncStatus SafeSyncStatus { get; private set; } = SafeSyncStatus.Idle;
        public string SafeSyncErrorCode { get; private set; } = string.Empty;
        public string SafeSyncMessage { get; private set; } = SafeUserMessageMapper.FromSync(SafeSyncStatus.Idle).Message;
        public int LastSyncAcceptedCount { get; private set; }
        public int LastSyncRejectedCount { get; private set; }
        public SafeSyncConnectionViewModel SafeSyncConnection => BuildSafeSyncConnection();
        public string SafeSyncBaseUrl => safeSyncService?.BaseUrl ?? SafeSyncApiConfig.DefaultBaseUrl;
        public bool HasSafeSyncService => safeSyncService != null;
        public bool IsSafeSyncRequestInProgress => SafeSyncStatus == SafeSyncStatus.CheckingHealth ||
                                                   SafeSyncStatus == SafeSyncStatus.CheckingServer ||
                                                   SafeSyncStatus == SafeSyncStatus.Syncing ||
                                                   SafeSyncStatus == SafeSyncStatus.Fetching ||
                                                   SafeSyncStatus == SafeSyncStatus.FetchingRemoteSessions ||
                                                   SafeSyncStatus == SafeSyncStatus.DeleteInProgress ||
                                                   SafeSyncStatus == SafeSyncStatus.RetryInProgress ||
                                                   syncInProgress != 0 ||
                                                   fetchInProgress != 0 ||
                                                   deleteInProgress != 0 ||
                                                   retryInProgress != 0 ||
                                                   localDeleteInProgress != 0 ||
                                                   tombstoneInProgress != 0 ||
                                                   conflictInProgress != 0 ||
                                                   healthInProgress != 0;
        public bool CanUseAuthenticatedSafeSync => authSessionService == null || authSessionService.HasUsableAccessToken;
        public AuthState AuthState => authSessionService?.State ?? AuthState.LoggedOut;
        public string AuthErrorCode { get; private set; } = string.Empty;
        public string AuthMessage { get; private set; } = SafeUserMessageMapper.FromAuth(AuthState.LoggedOut).Message;
        public bool HasExplicitAuthMessage { get; private set; }
        public bool HasAuthSessionService => authSessionService != null;
        public bool IsAuthRequestInProgress => AuthState == AuthState.LoggingIn ||
                                               AuthState == AuthState.SigningUp ||
                                               AuthState == AuthState.Refreshing ||
                                               loginInProgress != 0 ||
                                               signupInProgress != 0 ||
                                               refreshUserInProgress != 0 ||
                                               logoutInProgress != 0;
        public bool CanSubmitLogin => HasAuthSessionService && loginInProgress == 0 && signupInProgress == 0;
        public bool CanSubmitSignup => HasAuthSessionService && signupInProgress == 0 && loginInProgress == 0;
        public bool CanRefreshUser => HasAuthSessionService && refreshUserInProgress == 0 && loginInProgress == 0 && signupInProgress == 0;
        public bool CanLogout => HasAuthSessionService && logoutInProgress == 0 && AuthState == AuthState.LoggedIn;
        public string AuthUserSummary
        {
            get
            {
                var session = authSessionService?.CurrentSession;
                if (session == null)
                {
                    return string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(session.DisplayName))
                {
                    return session.DisplayName;
                }

                return !string.IsNullOrWhiteSpace(session.Email) ? session.Email : session.UserId;
            }
        }

        public DateTimeOffset? AuthTokenExpiresAt => authSessionService?.CurrentSession?.AccessTokenExpiresAt;
        public string AuthSessionExpirySummary => SafeUserMessageMapper.SessionExpirySummary(AuthState, AuthTokenExpiresAt);
        public string AuthStatusMessage
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(AuthErrorCode))
                {
                    return SafeUserMessageMapper.FromAuthError(AuthErrorCode);
                }

                if (AuthState == AuthState.LoggedIn)
                {
                    return string.IsNullOrWhiteSpace(AuthUserSummary) ? "Logged in for Safe Sync." : "Logged in as " + AuthUserSummary;
                }

                return SafeUserMessageMapper.FromAuth(AuthState).Message;
            }
        }

        public void SetOnboardingStep(OnboardingStep step)
        {
            Onboarding.CurrentStep = step;
        }

        public void ContinueOffline(string displayName = "")
        {
            Onboarding.OfflineModeSelected = true;
            AuthErrorCode = string.Empty;
            AuthMessage = "Local-only progress. Sync can be enabled later.";
            HasExplicitAuthMessage = true;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                CharacterDashboard.CharacterName = SafeLocalAlias(displayName, "Local Player");
            }

            Onboarding.CurrentStep = OnboardingStep.AiAgents;
        }

        public void SetLocalAuthMessage(string message)
        {
            AuthErrorCode = string.Empty;
            AuthMessage = string.IsNullOrWhiteSpace(message)
                ? SafeUserMessageMapper.FromAuth(AuthState).Message
                : message;
            HasExplicitAuthMessage = true;
        }

        public void SetAgentSourceSelected(ConnectedAgentSourceType sourceType, bool selected)
        {
            var source = Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            if (source == null)
            {
                return;
            }

            source.Selected = selected;
            if (!selected)
            {
                source.SafeLabel = string.Empty;
                source.SafeLocationHash = string.Empty;
                source.Confidence = ConfidenceLevel.Unknown;
                source.WarningCount = 0;
                approvedAgentCandidates.Remove(sourceType);
                source.State = sourceType == ConnectedAgentSourceType.OtherManualLogFolder
                    ? AgentSourceSetupState.ManualImportRequired
                    : AgentSourceSetupState.NotSelected;
                source.StatusLabel = source.State == AgentSourceSetupState.ManualImportRequired ? "Manual import required" : "Not selected";
                AgentFlow.ClearSelection();
                AgentSelectionStatus = "No agent log location selected";
                AgentPickerErrorCategory = string.Empty;
                if (SelectedAgentProviderType == ToAgentProviderType(sourceType))
                {
                    SelectedAgentProviderType = AgentProviderType.Unknown;
                }

                return;
            }

            if (sourceType == ConnectedAgentSourceType.OtherManualLogFolder)
            {
                source.State = string.IsNullOrWhiteSpace(source.SafeLabel)
                    ? AgentSourceSetupState.ManualImportRequired
                    : AgentSourceSetupState.ReadyToAnalyze;
                source.StatusLabel = source.State == AgentSourceSetupState.ReadyToAnalyze
                    ? "Ready to analyze"
                    : "Manual import required";
            }
            else
            {
                source.State = AgentSourceSetupState.Selected;
                source.StatusLabel = source.DisplayName + " selected";
            }
        }

        public void ToggleAgentSourceForOnboarding(ConnectedAgentSourceType sourceType)
        {
            var source = Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            if (source == null)
            {
                return;
            }

            SetAgentSourceSelected(sourceType, !source.Selected);
        }

        public async Task<Result> DetectAgentSourceForOnboardingAsync(ConnectedAgentSourceType sourceType, CancellationToken cancellationToken = default)
        {
            var source = Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            if (source == null)
            {
                return Result.Failure("agent_source_unknown", "Agent source is unavailable.");
            }

            if (sourceType == ConnectedAgentSourceType.OtherManualLogFolder)
            {
                return await SelectManualAgentLogForOnboardingAsync(cancellationToken);
            }

            source.Selected = true;
            source.State = AgentSourceSetupState.DetectingLocalSource;
            source.StatusLabel = "Detecting local source";
            var providerType = ToAgentProviderType(sourceType);
            var result = await agentSourceDetectorFactory(providerType).DetectAsync(cancellationToken);
            source.LastScanTimeUtc = result.ScannedAtUtc;
            source.WarningCount = result.WarningIds?.Count ?? 0;

            var candidate = result.BestCandidate();
            if (candidate != null && result.HasUsableCandidate)
            {
                approvedAgentCandidates[sourceType] = candidate;
                source.Selected = false;
                source.State = AgentSourceSetupState.LocalSourceDetected;
                source.SafeLabel = candidate.SafeAlias;
                source.SafeLocationHash = SafeHashUtility.ComputeProjectPathHash(candidate.LocalPath, "TokenForge.AgentLogLocation.v1");
                source.Confidence = candidate.Confidence;
                source.WarningCount += candidate.WarningIds?.Count ?? 0;
                source.StatusLabel = "Detected source found. Connect to approve " + candidate.SafeAlias + ".";
                await PersistProviderConnectionStateAsync(source, cancellationToken);
                return Result.Success();
            }

            if (result.AccessState == AgentSourceAccessState.PermissionRequired)
            {
                source.Selected = false;
                source.State = AgentSourceSetupState.PermissionRequired;
                source.StatusLabel = "Permission required";
                AgentPickerErrorCategory = "agent_source_permission_required";
                await PersistProviderConnectionStateAsync(source, cancellationToken);
                return Result.Failure(AgentPickerErrorCategory, "Local source requires permission.");
            }

            source.Selected = false;
            source.State = AgentSourceSetupState.ManualImportRequired;
            source.StatusLabel = source.DisplayName + " manual import required";
            AgentPickerErrorCategory = "agent_source_manual_import_required";
            await PersistProviderConnectionStateAsync(source, cancellationToken);
            return Result.Failure(AgentPickerErrorCategory, "Local source was not detected. Choose a folder to analyze safe aggregates.");
        }

        public async Task<Result> ApproveDetectedAgentSourceForOnboardingAsync(ConnectedAgentSourceType sourceType, CancellationToken cancellationToken = default)
        {
            var source = Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            if (source == null)
            {
                return Result.Failure("agent_source_unknown", "Agent source is unavailable.");
            }

            if (!approvedAgentCandidates.TryGetValue(sourceType, out var candidate) || candidate == null || string.IsNullOrWhiteSpace(candidate.LocalPath))
            {
                return Result.Failure("agent_source_no_detected_candidate", "Run Auto Detect or choose a folder before connecting this provider.");
            }

            var providerType = ToAgentProviderType(sourceType);
            SelectedAgentProviderType = providerType;
            var selection = AgentSettings.ToInput(candidate.LocalPath, providerType);
            selection.SourceKind = candidate.SourceKind;
            selection.SafeSourceAlias = candidate.SafeAlias;
            var selectResult = AgentFlow.SelectApprovedLogLocation(selection);
            if (!selectResult.IsSuccess)
            {
                source.Selected = false;
                source.State = AgentSourceSetupState.AnalysisFailedSafely;
                source.StatusLabel = "Analysis failed safely";
                await PersistProviderConnectionStateAsync(source, cancellationToken);
                return selectResult;
            }

            source.Selected = true;
            source.State = AgentSourceSetupState.ReadyToAnalyze;
            source.StatusLabel = "Connected. Ready to analyze.";
            source.SafeLabel = candidate.SafeAlias;
            source.SafeLocationHash = SafeHashUtility.ComputeProjectPathHash(candidate.LocalPath, "TokenForge.AgentLogLocation.v1");
            source.Confidence = candidate.Confidence;
            await AddCurrentAgentSelectionToApprovedLocationsAsync(candidate.SafeAlias, cancellationToken);
            await PersistProviderConnectionStateAsync(source, cancellationToken);
            return Result.Success();
        }

        public async Task<Result<AgentAnalysisReviewModel>> AnalyzeAgentSourceForOnboardingAsync(ConnectedAgentSourceType sourceType, CancellationToken cancellationToken = default)
        {
            var source = Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            if (source == null)
            {
                return Result<AgentAnalysisReviewModel>.Failure("agent_source_unknown", "Agent source is unavailable.");
            }

            if (source.State != AgentSourceSetupState.ReadyToAnalyze && source.State != AgentSourceSetupState.AnalysisComplete)
            {
                AgentPickerErrorCategory = "agent_source_not_ready";
                AgentSelectionStatus = source.DisplayName + " has no verified local source selected.";
                return Result<AgentAnalysisReviewModel>.Failure(
                    AgentPickerErrorCategory,
                    "Detect a local source or choose a folder before analyzing safe aggregates.");
            }

            if (string.IsNullOrWhiteSpace(source.SafeLocationHash) || !AgentFlow.HasSelectedAgentLogLocationForLocalOnlyApproval)
            {
                AgentPickerErrorCategory = "missing_agent_log_location";
                AgentSelectionStatus = source.DisplayName + " has no local source selected.";
                return Result<AgentAnalysisReviewModel>.Failure(
                    AgentPickerErrorCategory,
                    "Select an agent log folder before analyzing.");
            }

            var providerType = ToAgentProviderType(sourceType);
            if (MacAgentSourceDetector.NormalizeProvider(SelectedAgentProviderType) != MacAgentSourceDetector.NormalizeProvider(providerType))
            {
                AgentPickerErrorCategory = "agent_source_not_ready";
                AgentSelectionStatus = source.DisplayName + " source is not the active verified source.";
                return Result<AgentAnalysisReviewModel>.Failure(
                    AgentPickerErrorCategory,
                    "Choose this provider folder before analyzing.");
            }

            source.State = AgentSourceSetupState.DetectingLocalSource;
            source.StatusLabel = "Analysis running";
            await PersistProviderConnectionStateAsync(source, cancellationToken);
            var result = await AnalyzeAgentActivityAsync(cancellationToken);
            if (result.IsSuccess)
            {
                source.State = AgentSourceSetupState.AnalysisComplete;
                source.StatusLabel = "Analysis complete";
            }
            else
            {
                source.State = AgentSourceSetupState.AnalysisFailedSafely;
                source.StatusLabel = "Analysis failed safely";
            }

            await PersistProviderConnectionStateAsync(source, cancellationToken);
            return result;
        }

        public async Task<Result> SelectManualAgentLogForOnboardingAsync(CancellationToken cancellationToken = default)
        {
            return await SelectManualAgentLogForOnboardingAsync(ConnectedAgentSourceType.OtherManualLogFolder, cancellationToken);
        }

        public async Task<Result> SelectManualAgentLogForOnboardingAsync(ConnectedAgentSourceType sourceType, CancellationToken cancellationToken = default)
        {
            if (sourceType != ConnectedAgentSourceType.OtherManualLogFolder)
            {
                SelectedAgentProviderType = ToAgentProviderType(sourceType);
            }

            var result = await SelectAgentLogLocationAsync(cancellationToken);
            if (result.IsSuccess && AgentFlow.HasSelectedAgentLogLocationForLocalOnlyApproval)
            {
                var source = Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
                if (source != null)
                {
                    source.Selected = true;
                    source.State = AgentSourceSetupState.ReadyToAnalyze;
                    source.StatusLabel = "Ready to analyze";
                    source.SafeLabel = sourceType == ConnectedAgentSourceType.OtherManualLogFolder
                        ? "Manual Log Folder 1"
                        : source.DisplayName + " manual log folder";
                    var selectedPath = AgentFlow.GetSelectedAgentLogLocationPathForLocalOnlyApproval();
                    source.SafeLocationHash = SafeHashUtility.ComputeProjectPathHash(selectedPath, "TokenForge.AgentLogLocation.v1");
                    source.Confidence = ConfidenceLevel.Medium;
                    if (sourceType == ConnectedAgentSourceType.OtherManualLogFolder)
                    {
                        SelectedAgentProviderType = AgentProviderType.Manual;
                    }

                    await PersistProviderConnectionStateAsync(source, cancellationToken);
                }
            }

            return result;
        }

        public async Task<Result> DisconnectAgentSourceForOnboardingAsync(ConnectedAgentSourceType sourceType, CancellationToken cancellationToken = default)
        {
            SetAgentSourceSelected(sourceType, false);
            var providerType = ToAgentProviderType(sourceType);
            if (providerType == AgentProviderType.Unknown)
            {
                return Result.Failure("agent_source_unknown", "Agent source is unavailable.");
            }

            var settings = await approvedLocationRepository.LoadAsync(cancellationToken);
            var sourceKind = providerType.ToApprovedLocationSourceType();
            foreach (var location in (settings.Locations ?? new List<ApprovedLocationEntry>())
                         .Where(item => item.SourceType == sourceKind)
                         .ToList())
            {
                await approvedLocationRepository.SetEnabledAsync(location.LocalId, false, cancellationToken);
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.ProviderSettings = saveData.ProviderSettings ?? new List<ProviderSettings>();
            saveData.ProviderSettings.RemoveAll(item => string.Equals(item.ProviderId, providerType.ToString(), StringComparison.OrdinalIgnoreCase));
            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (saveResult.IsSuccess)
            {
                await RefreshApprovedLocationsAsync(cancellationToken);
            }

            return saveResult;
        }

        public async Task<Result> SelectLocalGitRepositoryForOnboardingAsync(CancellationToken cancellationToken = default)
        {
            Debug.Log("INFO [RepositoryAdd][REQUEST] rawPath=picker");
            var result = await GitFlow.SelectRepositoryAsync(cancellationToken);
            if (result.IsSuccess && GitFlow.HasSelectedRepositoryForLocalOnlyApproval)
            {
                Onboarding.GitConnected = true;
                Onboarding.GitSkipped = false;
                Onboarding.GitAccountPlaceholderSelected = false;
                var saveData = await repository.LoadAsync(cancellationToken);
                RepositoryCompanionProfileService.Normalize(saveData);
                var selected = RepositoryCompanionProfileService.GetSelectedProfile(saveData);
                Onboarding.GitSafeAlias = selected?.SafeRepositoryAlias ?? "Git repository";
                AgentFlow.SetSelectedRepositoryHash(saveData.SelectedRepositoryHash);
                UpsertRepositoryConnection(saveData, selected, GitFlow.GetSelectedRepositoryPathForLocalOnlyApproval());
                var saveResult = await repository.SaveAsync(saveData, cancellationToken);
                if (!saveResult.IsSuccess)
                {
                    return saveResult;
                }

                var verify = RepositoryCompanionProfileService.Normalize(await repository.LoadAsync(cancellationToken));
                var found = RepositoryCompanionProfileService.IsConnectedRepository(verify, saveData.SelectedRepositoryHash);
                Debug.Log("INFO [RepositoryStore][VERIFY_AFTER_SAVE] repo=" + saveData.SelectedRepositoryHash + " found=" + found + " connected=" + found);
                await AddCurrentGitSelectionToApprovedLocationsAsync(Onboarding.GitSafeAlias, cancellationToken);
                RefreshCharacterDashboard(saveData, RecentSessions);
            }

            return result;
        }

        public void SelectGitAccountPlaceholder()
        {
            Onboarding.GitConnected = false;
            Onboarding.GitSkipped = false;
            Onboarding.GitAccountPlaceholderSelected = true;
            Onboarding.GitSafeAlias = "Git account not available in this MVP";
        }

        public void SkipGitForOnboarding()
        {
            Onboarding.GitConnected = false;
            Onboarding.GitSkipped = true;
            Onboarding.GitAccountPlaceholderSelected = false;
            Onboarding.GitSafeAlias = "Skipped";
        }

        public void ClearGitForOnboarding()
        {
            Onboarding.GitConnected = false;
            Onboarding.GitSkipped = false;
            Onboarding.GitAccountPlaceholderSelected = false;
            Onboarding.GitSafeAlias = string.Empty;
            GitFlow.ClearSelection();
        }

        public async Task<Result> SelectAgentLogLocationAsync(CancellationToken cancellationToken = default)
        {
            var pickResult = await agentLogLocationPicker.PickAgentLogLocationAsync(cancellationToken);
            if (!pickResult.IsSuccess)
            {
                AgentSelectionStatus = "No agent log location selected";
                AgentPickerErrorCategory = pickResult.IsCancelled ? string.Empty : pickResult.ErrorCode;
                if (pickResult.IsCancelled)
                {
                    return Result.Success();
                }

                return Result.Failure(
                    string.IsNullOrWhiteSpace(pickResult.ErrorCode) ? "agent_log_picker_failed" : pickResult.ErrorCode,
                    "Agent log location selection is unavailable.");
            }

            AgentPickerErrorCategory = string.Empty;
            var providerType = SelectedAgentProviderType == AgentProviderType.Unknown ? AgentProviderType.Manual : SelectedAgentProviderType;
            var input = AgentSettings.ToInput(pickResult.AgentLogLocationPath, providerType);
            input.SourceKind = AgentSourceKind.ManualFolder;
            input.SafeSourceAlias = "Manual Log Folder 1";
            var result = AgentFlow.SelectApprovedLogLocation(input);
            AgentSelectionStatus = result.IsSuccess ? "Agent log location selected" : "No agent log location selected";
            if (result.IsSuccess)
            {
                await AddCurrentAgentSelectionToApprovedLocationsAsync(
                    SelectedAgentProviderType == AgentProviderType.Codex ? "Codex local activity" : "Agent logs",
                    cancellationToken);
            }

            return result;
        }

        public async Task<Result<ApprovedLocationEntry>> AddCurrentGitSelectionToApprovedLocationsAsync(string displayAlias, CancellationToken cancellationToken = default)
        {
            var selectedPath = GitFlow.GetSelectedRepositoryPathForLocalOnlyApproval();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return Result<ApprovedLocationEntry>.Failure("missing_repository_selection", "Select a repository before adding it as an approved local location.");
            }

            var result = await approvedLocationRepository.AddOrUpdateAsync(new ApprovedLocationEntry
            {
                DisplayAlias = SafeLocalAlias(displayAlias, "Git repository"),
                SourceType = ApprovedLocationSourceType.Git,
                LocalPath = selectedPath,
                Enabled = true
            }, cancellationToken);

            if (result.IsSuccess)
            {
                await RefreshApprovedLocationsAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result<ApprovedLocationEntry>> AddCurrentAgentSelectionToApprovedLocationsAsync(string displayAlias, CancellationToken cancellationToken = default)
        {
            var selectedPath = AgentFlow.GetSelectedAgentLogLocationPathForLocalOnlyApproval();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return Result<ApprovedLocationEntry>.Failure("missing_agent_log_location", "Select an agent log location before adding it as an approved local location.");
            }

            var result = await approvedLocationRepository.AddOrUpdateAsync(new ApprovedLocationEntry
            {
                DisplayAlias = SafeLocalAlias(displayAlias, "Agent logs"),
                SourceType = SelectedAgentProviderType.ToApprovedLocationSourceType(),
                LocalPath = selectedPath,
                Enabled = true
            }, cancellationToken);

            if (result.IsSuccess)
            {
                await RefreshApprovedLocationsAsync(cancellationToken);
            }

            return result;
        }

        private static void UpsertRepositoryConnection(SaveData saveData, RepositoryCompanionProfile profile, string selectedPath)
        {
            if (saveData == null || profile == null)
            {
                return;
            }

            saveData.ConnectedProjects = saveData.ConnectedProjects ?? new List<ConnectedProject>();
            var selectedPathHash = string.IsNullOrWhiteSpace(selectedPath)
                ? profile.RepositoryHash
                : RepositoryCompanionProfileService.HashRepositoryPath(selectedPath);
            var pathHash = profile.RepositoryHash;
            if (!string.Equals(selectedPathHash, profile.RepositoryHash, StringComparison.Ordinal))
            {
                Debug.LogWarning("WARN [RepositoryIdentity][MISMATCH] old=" + selectedPathHash + " new=" + profile.RepositoryHash + " reason=profileCanonicalIdentityWins");
            }

            var connection = saveData.ConnectedProjects.FirstOrDefault(item => string.Equals(item.PathHash, pathHash, StringComparison.Ordinal) ||
                                                                               string.Equals(item.ProjectPathHash, pathHash, StringComparison.Ordinal) ||
                                                                               string.Equals(item.PathHash, selectedPathHash, StringComparison.Ordinal) ||
                                                                               string.Equals(item.ProjectPathHash, selectedPathHash, StringComparison.Ordinal));
            Debug.Log("INFO [RepositoryStore][UPSERT_BEGIN] repo=" + pathHash);
            if (connection == null)
            {
                connection = new ConnectedProject();
                saveData.ConnectedProjects.Add(connection);
                Debug.Log("INFO [RepositoryStore][CREATE_NEW] repo=" + pathHash);
            }
            else if (!string.Equals(selectedPathHash, pathHash, StringComparison.Ordinal))
            {
                Debug.Log("INFO [RepositoryIdentity][MIGRATE] from=" + selectedPathHash + " to=" + pathHash + " preservedCompanion=true");
            }
            else
            {
                Debug.Log("INFO [RepositoryStore][RESTORE_EXISTING] repo=" + pathHash + " connectedBefore=" + (!connection.IsArchived) + " archivedBefore=" + connection.IsArchived);
            }

            connection.Id = profile.RepositoryHash;
            connection.DisplayName = profile.SafeRepositoryAlias;
            connection.ApprovedAt = profile.ApprovedAtUtc ?? DateTimeOffset.UtcNow;
            connection.ConnectionSource = "userSelected";
            connection.PathHash = pathHash;
            connection.ProjectPathHash = pathHash;
            connection.ProjectAlias = profile.SafeRepositoryAlias;
            connection.IsGitRepository = true;
            connection.IsActive = string.Equals(saveData.SelectedRepositoryHash, profile.RepositoryHash, StringComparison.Ordinal);
            connection.IsArchived = profile.ArchivedAtUtc != null;
            connection.CompanionId = profile.CompanionId;
            foreach (var other in saveData.ConnectedProjects.Where(item => item != null && !ReferenceEquals(item, connection)))
            {
                other.IsActive = false;
            }

            Debug.Log("INFO [RepositoryStore][SAVE_COMMIT] repo=" + pathHash + " connected=" + (!connection.IsArchived));
            Debug.Log("INFO [RepositorySelection][SET_ACTIVE] repo=" + profile.RepositoryHash + " source=addRepository");
        }

        private async Task<Result> PersistProviderConnectionStateAsync(ConnectedAgentSource source, CancellationToken cancellationToken)
        {
            if (source == null)
            {
                return Result.Failure("agent_source_unknown", "Agent source is unavailable.");
            }

            var providerType = ToAgentProviderType(source.SourceType);
            if (providerType == AgentProviderType.Unknown)
            {
                return Result.Failure("agent_source_unknown", "Agent source is unavailable.");
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.ProviderSettings = saveData.ProviderSettings ?? new List<ProviderSettings>();
            var providerId = providerType.ToString();
            var settings = saveData.ProviderSettings.FirstOrDefault(item => string.Equals(item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
            if (settings == null)
            {
                settings = new ProviderSettings { ProviderId = providerId };
                saveData.ProviderSettings.Add(settings);
            }

            settings.Enabled = source.Selected;
            settings.Selected = source.Selected;
            settings.Detected = source.State == AgentSourceSetupState.LocalSourceDetected ||
                                source.State == AgentSourceSetupState.ReadyToAnalyze ||
                                source.State == AgentSourceSetupState.AnalysisComplete;
            settings.ManualFolderApproved = source.Selected &&
                                            (source.State == AgentSourceSetupState.ReadyToAnalyze ||
                                             source.State == AgentSourceSetupState.AnalysisComplete);
            settings.ConnectionState = source.State.ToString();
            settings.Status = source.Selected && (source.State == AgentSourceSetupState.ReadyToAnalyze || source.State == AgentSourceSetupState.AnalysisComplete)
                ? "connected"
                : source.State == AgentSourceSetupState.LocalSourceDetected
                    ? "detected"
                    : source.State == AgentSourceSetupState.PermissionRequired
                        ? "warning"
                        : source.State == AgentSourceSetupState.AnalysisFailedSafely
                            ? "warning"
                            : "notConfigured";
            settings.DetectedSources = string.IsNullOrWhiteSpace(source.SafeLabel)
                ? new List<string>()
                : new List<string> { source.SafeLabel };
            settings.ApprovedSource = source.Selected ? source.SafeLabel ?? string.Empty : string.Empty;
            settings.Warnings = string.IsNullOrWhiteSpace(source.StatusLabel) || settings.Status == "connected"
                ? new List<string>()
                : new List<string> { source.StatusLabel };
            settings.Confidence = source.Confidence.ToString();
            settings.SafeLocationHash = source.SafeLocationHash ?? string.Empty;
            settings.LastScanAt = source.LastScanTimeUtc;
            settings.ParserVersion = ParserVersionFor(providerType);

            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            return await repository.SaveAsync(saveData, cancellationToken);
        }

        public async Task<Result> SelectApprovedGitLocationAsync(string localId, CancellationToken cancellationToken = default)
        {
            var entry = await FindApprovedLocationAsync(localId, ApprovedLocationSourceType.Git, cancellationToken);
            if (entry == null)
            {
                return Result.Failure("approved_location_not_found", "Approved Git location was not found.");
            }

            var result = await GitFlow.SelectLocalOnlyApprovedRepositoryPathAsync(entry.LocalPath, cancellationToken);
            if (result.IsSuccess)
            {
                var saveData = await repository.LoadAsync(cancellationToken);
                RepositoryCompanionProfileService.Normalize(saveData);
                AgentFlow.SetSelectedRepositoryHash(saveData.SelectedRepositoryHash);
                RefreshCharacterDashboard(saveData, RecentSessions);
            }

            return result;
        }

        public async Task<Result> SelectApprovedAgentLocationAsync(string localId, CancellationToken cancellationToken = default)
        {
            var settings = await approvedLocationRepository.LoadAsync(cancellationToken);
            var entry = (settings.Locations ?? new List<ApprovedLocationEntry>())
                .FirstOrDefault(item => item.LocalId == localId && item.Enabled && item.SourceType != ApprovedLocationSourceType.Git);
            if (entry == null)
            {
                return Result.Failure("approved_location_not_found", "Approved agent log location was not found.");
            }

            SelectedAgentProviderType = entry.SourceType.ToAgentProviderType();
            var input = AgentSettings.ToInput(entry.LocalPath, SelectedAgentProviderType);
            input.SourceKind = AgentSourceKind.ManualFolder;
            input.SafeSourceAlias = entry.DisplayAlias;
            var result = AgentFlow.SelectApprovedLogLocation(input);
            AgentSelectionStatus = result.IsSuccess ? "Approved agent log location selected" : "No agent log location selected";
            return result;
        }

        public async Task<Result> DisableApprovedLocationAsync(string localId, CancellationToken cancellationToken = default)
        {
            var result = await approvedLocationRepository.SetEnabledAsync(localId, false, cancellationToken);
            if (result.IsSuccess)
            {
                await RefreshApprovedLocationsAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result> RemoveApprovedLocationAsync(string localId, CancellationToken cancellationToken = default)
        {
            var result = await approvedLocationRepository.RemoveAsync(localId, cancellationToken);
            if (result.IsSuccess)
            {
                await RefreshApprovedLocationsAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result> SelectRepositoryCompanionProfileAsync(string repositoryHash, CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            RepositoryCompanionProfileService.Normalize(saveData);
            var profile = (saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
                .FirstOrDefault(item => string.Equals(item.RepositoryHash, repositoryHash, StringComparison.Ordinal));
            if (profile == null)
            {
                return Result.Failure("repository_profile_not_found", "Repository profile was not found.");
            }

            if (profile.ArchivedAtUtc != null)
            {
                return Result.Failure("repository_profile_archived", "Archived repositories cannot be selected.");
            }

            if (!RepositoryCompanionProfileService.IsConnectedRepository(saveData, profile.RepositoryHash))
            {
                return Result.Failure("repository_not_connected", "Connect this repository before selecting its companion.");
            }

            saveData.SelectedRepositoryHash = profile.RepositoryHash;
            saveData.CompanionState = CompanionProgressionRules.Normalize(profile.CompanionState);
            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (saveResult.IsSuccess)
            {
                await RestoreSelectedRepositoryPathAsync(saveData.SelectedRepositoryHash, cancellationToken);
                AgentFlow.SetSelectedRepositoryHash(saveData.SelectedRepositoryHash);
                RefreshCharacterDashboard(saveData, RecentSessions);
            }

            return saveResult;
        }

        public async Task<Result> RemoveRepositoryCompanionProfileAsync(string repositoryHash, CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            var result = RepositoryCompanionProfileService.RemoveProfile(saveData, repositoryHash);
            if (!result.IsSuccess)
            {
                return result;
            }

            foreach (var project in saveData.ConnectedProjects ?? new List<ConnectedProject>())
            {
                if (string.Equals(project.PathHash, repositoryHash, StringComparison.Ordinal) ||
                    string.Equals(project.ProjectPathHash, repositoryHash, StringComparison.Ordinal))
                {
                    project.IsActive = false;
                    project.IsArchived = true;
                }
            }

            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (saveResult.IsSuccess)
            {
                AgentFlow.SetSelectedRepositoryHash(saveData.SelectedRepositoryHash);
                RefreshCharacterDashboard(saveData, RecentSessions);
            }

            return saveResult;
        }

        public async Task<Result<AgentAnalysisReviewModel>> AnalyzeAgentActivityAsync(CancellationToken cancellationToken = default)
        {
            var result = await AgentFlow.AnalyzeAsync(cancellationToken);
            if (result.IsSuccess)
            {
                var providerLabel = MacAgentSourceDetector.SafeProviderLabel(SelectedAgentProviderType);
                var persist = await PersistPendingNativeReviewAsync(
                    "aiAgent",
                    AgentFlow.PendingSessionForLocalOnlyApproval,
                    result.Value.DerivedExpGained,
                    result.Value.DerivedStatDeltas,
                    result.Value.ConfidenceLevel.ToString(),
                    result.Value.WarningIds,
                    providerLabel + " session activity analyzed. Review the estimated XP before saving growth.",
                    cancellationToken);
                if (!persist.IsSuccess)
                {
                    AgentFlow.DiscardPendingReview();
                    return Result<AgentAnalysisReviewModel>.Failure(persist.ErrorCode, persist.ErrorMessage);
                }
            }

            return result;
        }

        public async Task<Result> RecordNativeAnalysisRunAsync(string sourceKind, string status, string errorCode, string safeSummary, CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.RecentNativeAnalysisRuns = saveData.RecentNativeAnalysisRuns ?? new List<NativeAnalysisRunRecord>();
            var normalizedStatus = SafeLocalAlias(status, "failed");
            saveData.RecentNativeAnalysisRuns.Insert(0, new NativeAnalysisRunRecord
            {
                RunId = Guid.NewGuid().ToString("N"),
                SourceKind = SafeLocalAlias(sourceKind, "activity"),
                Status = normalizedStatus,
                ErrorCode = SafeLocalAlias(errorCode, "unknown"),
                SafeSummary = SafeLocalAlias(safeSummary, "Analysis failed safely."),
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            saveData.RecentNativeAnalysisRuns = saveData.RecentNativeAnalysisRuns.Take(20).ToList();
            RepositoryCompanionProfileService.RecordTimelineEvent(
                saveData,
                string.Equals(normalizedStatus, "failed", StringComparison.OrdinalIgnoreCase) ? "analysis_failed" : "analysis_completed",
                string.Equals(normalizedStatus, "failed", StringComparison.OrdinalIgnoreCase) ? "Analysis failed" : "Analysis completed",
                SafeLocalAlias(safeSummary, "Analysis run recorded."),
                saveData.SelectedRepositoryHash,
                string.Empty,
                SafeLocalAlias(sourceKind, "activity"),
                0,
                0,
                PendingProviderId(saveData.PendingNativeActivityReview),
                string.Empty,
                string.Empty,
                string.Equals(normalizedStatus, "failed", StringComparison.OrdinalIgnoreCase) ? "error" : "info");
            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (saveResult.IsSuccess)
            {
                RefreshCharacterDashboard(saveData, RecentSessions);
            }

            return saveResult;
        }

        public async Task<Result<PendingNativeActivityReview>> CombinePendingNativeReviewsFromFlowsAsync(CancellationToken cancellationToken = default)
        {
            if (!GitFlow.HasPendingReview || !AgentFlow.HasPendingReview)
            {
                return Result<PendingNativeActivityReview>.Failure("combined_review_requires_git_and_agent", "Both repository and AI agent analysis must finish before creating a combined review.");
            }

            var sessions = new List<AgentWorkSession>
            {
                GitFlow.PendingSessionForLocalOnlyApproval,
                AgentFlow.PendingSessionForLocalOnlyApproval
            }.Where(session => session != null).ToList();
            if (sessions.Count != 2)
            {
                return Result<PendingNativeActivityReview>.Failure("combined_review_missing_session", "A combined review needs both safe sessions.");
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            RepositoryCompanionProfileService.Normalize(saveData);
            foreach (var session in sessions)
            {
                session.GitChangeSummary = session.GitChangeSummary ?? GitChangeSummary.Empty();
                if (string.IsNullOrWhiteSpace(session.GitChangeSummary.ProjectPathHash))
                {
                    session.GitChangeSummary.ProjectPathHash = saveData.SelectedRepositoryHash;
                }
            }

            var totalXp = Math.Max(0, GitFlow.Review?.DerivedExpGained ?? 0) +
                          Math.Max(0, AgentFlow.Review?.DerivedExpGained ?? 0);
            var combinedStats = CharacterStats.Zero();
            combinedStats.Add(GitFlow.Review?.DerivedStatDeltas ?? CharacterStats.Zero());
            combinedStats.Add(AgentFlow.Review?.DerivedStatDeltas ?? CharacterStats.Zero());
            var growthResults = BuildCombinedGrowthResults(saveData, sessions, GitFlow.Review, AgentFlow.Review);
            var warnings = (GitFlow.Review?.PrivacyWarningCategories ?? new List<string>())
                .Concat(AgentFlow.Review?.WarningIds ?? new List<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .Take(8)
                .ToList();

            saveData.PendingNativeActivityReview = new PendingNativeActivityReview
            {
                ReviewId = Guid.NewGuid().ToString("N"),
                SourceKind = "combined",
                RepositoryHash = saveData.SelectedRepositoryHash,
                SafeSummary = "Repository and AI agent activity analyzed together. Review the XP breakdown before saving growth.",
                ActivityCategory = "Combined",
                Confidence = CombinedConfidence(GitFlow.Review?.ConfidenceLevel ?? ConfidenceLevel.Unknown, AgentFlow.Review?.ConfidenceLevel ?? ConfidenceLevel.Unknown),
                CommitCountBucket = GitFlow.Review?.CommitCountBucket ?? CountBucket.Unknown,
                ChangedFileCountBucket = GitFlow.Review?.ChangedFilesBucket ?? CountBucket.Unknown,
                EstimatedXpDelta = totalXp,
                StatDeltas = combinedStats,
                WarningIds = warnings,
                SafeSession = sessions[0],
                GrowthResult = new CharacterGrowthResult
                {
                    SessionId = string.Join("+", sessions.Select(session => session.SessionId)),
                    ExpGained = totalXp,
                    LevelBefore = Math.Max(1, saveData.CharacterProfile?.Level ?? 1),
                    LevelAfter = Math.Max(1, 1 + ((saveData.CharacterProfile?.TotalExp ?? 0) + totalXp) / 1000),
                    StatDeltas = combinedStats
                },
                SafeSessions = sessions,
                GrowthResults = growthResults,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            UpsertActivityReview(saveData, saveData.PendingNativeActivityReview, "pending", null);

            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return Result<PendingNativeActivityReview>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return Result<PendingNativeActivityReview>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            RefreshCharacterDashboard(saveData, RecentSessions);
            return Result<PendingNativeActivityReview>.Success(saveData.PendingNativeActivityReview);
        }

        public async Task<Result<GitAnalysisReviewModel>> AnalyzeGitActivityAsync(CancellationToken cancellationToken = default)
        {
            var result = await GitFlow.AnalyzeAsync(cancellationToken);
            if (result.IsSuccess)
            {
                var persist = await PersistPendingNativeReviewAsync(
                    "repository",
                    GitFlow.PendingSessionForLocalOnlyApproval,
                    result.Value.DerivedExpGained,
                    result.Value.DerivedStatDeltas,
                    result.Value.ConfidenceLevel.ToString(),
                    result.Value.PrivacyWarningCategories,
                    (CharacterDashboard?.CurrentRepositoryAlias ?? "Repository") + " Git changes analyzed. Review the XP breakdown before saving growth.",
                    cancellationToken);
                if (!persist.IsSuccess)
                {
                    GitFlow.DiscardPendingReview();
                    return Result<GitAnalysisReviewModel>.Failure(persist.ErrorCode, persist.ErrorMessage);
                }
            }

            return result;
        }

        public async Task<Result<AgentAnalysisReviewModel>> AnalyzeSelectedAgentActivityAsync(CancellationToken cancellationToken = default)
        {
            if (AgentFlow.HasSelectedAgentLogLocationForLocalOnlyApproval)
            {
                return await AgentFlow.AnalyzeAsync(cancellationToken);
            }

            var selectedAutomatic = Onboarding.AgentSources
                .FirstOrDefault(source => source.Selected && source.SourceType != ConnectedAgentSourceType.OtherManualLogFolder);
            if (selectedAutomatic != null)
            {
                AgentPickerErrorCategory = "agent_source_manual_import_required";
                AgentSelectionStatus = selectedAutomatic.DisplayName + " requires Detect or Choose Folder before analysis.";
                var failure = AgentFlow.MarkBlocked(
                    AgentPickerErrorCategory,
                    "Detect a local source or choose a manual log folder before analyzing safe aggregates.");
                return Result<AgentAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            AgentPickerErrorCategory = "missing_agent_log_location";
            AgentSelectionStatus = "No agent log location selected";
            var missing = AgentFlow.MarkBlocked(
                AgentPickerErrorCategory,
                "Select an agent log folder before analyzing.");
            return Result<AgentAnalysisReviewModel>.Failure(missing.ErrorCode, missing.ErrorMessage);
        }

        public async Task<Result<SaveData>> SaveGitSessionAsync(CancellationToken cancellationToken = default)
        {
            var result = await GitFlow.SaveSessionAsync(cancellationToken);
            if (result.IsSuccess)
            {
                result.Value.PendingNativeActivityReview = null;
                await repository.SaveAsync(result.Value, cancellationToken);
                AgentFlow.SetSelectedRepositoryHash(result.Value?.SelectedRepositoryHash);
                await RefreshRecentSessionsAsync(cancellationToken);
                await RefreshSafeSyncLocalStateAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result<SaveData>> SaveAgentSessionAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            RepositoryCompanionProfileService.Normalize(saveData);
            AgentFlow.SetSelectedRepositoryHash(saveData.SelectedRepositoryHash);
            var result = await AgentFlow.SaveSessionAsync(cancellationToken);
            if (result.IsSuccess)
            {
                result.Value.PendingNativeActivityReview = null;
                await repository.SaveAsync(result.Value, cancellationToken);
                await RefreshRecentSessionsAsync(cancellationToken);
                await RefreshSafeSyncLocalStateAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result<DesktopCompanionSettings>> SetDesktopCompanionEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            return await UpdateDesktopCompanionSettingsAsync(settings => settings.IsDesktopCompanionEnabled = enabled, cancellationToken);
        }

        public async Task<Result<DesktopCompanionSettings>> SetDesktopCompanionClickThroughAsync(bool clickThrough, CancellationToken cancellationToken = default)
        {
            return await UpdateDesktopCompanionSettingsAsync(settings => settings.IsClickThroughEnabled = clickThrough, cancellationToken);
        }

        public async Task<Result<DesktopCompanionSettings>> SetDesktopCompanionMotionModeAsync(CompanionDesktopMotionMode motionMode, CancellationToken cancellationToken = default)
        {
            return await UpdateDesktopCompanionSettingsAsync(settings => settings.MotionMode = motionMode, cancellationToken);
        }

        public async Task<Result<DesktopCompanionSettings>> SetDesktopCompanionVisualThemeAsync(string visualThemeId, CancellationToken cancellationToken = default)
        {
            visualThemeId = CompanionSkinCatalog.Normalize(visualThemeId);
            return await UpdateDesktopCompanionSettingsAsync(settings => settings.VisualThemeId = visualThemeId, cancellationToken);
        }

        public async Task<Result<DesktopCompanionSettings>> SetRepositoryZodiacMascotAsync(string zodiacTypeId, CancellationToken cancellationToken = default)
        {
            zodiacTypeId = RepositoryCompanionProfileService.NormalizeZodiacTypeId(zodiacTypeId, "repository");
            return await UpdateDesktopCompanionSettingsAsync(settings => settings.ZodiacTypeId = zodiacTypeId, cancellationToken);
        }

        public async Task<Result> CompleteFirstRunOnboardingAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.OnboardingPreferences = saveData.OnboardingPreferences ?? new OnboardingPreferences();
            saveData.OnboardingPreferences.FirstRunOnboardingCompleted = true;
            saveData.OnboardingPreferences.CompletedAtUtc = DateTimeOffset.UtcNow;
            saveData.OnboardingPreferences.LastOpenedAtUtc = DateTimeOffset.UtcNow;
            RepositoryCompanionProfileService.RecordTimelineEvent(
                saveData,
                "onboarding_completed",
                "Onboarding completed",
                "First-run onboarding was completed and routed back to Dashboard.",
                saveData.SelectedRepositoryHash,
                string.Empty,
                "onboarding");
            return await repository.SaveAsync(saveData, cancellationToken);
        }

        public async Task<Result> MarkOnboardingOpenedAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.OnboardingPreferences = saveData.OnboardingPreferences ?? new OnboardingPreferences();
            saveData.OnboardingPreferences.LastOpenedAtUtc = DateTimeOffset.UtcNow;
            RepositoryCompanionProfileService.RecordTimelineEvent(
                saveData,
                "app_reopened",
                "Onboarding opened",
                "Onboarding was reopened from the dashboard.",
                saveData.SelectedRepositoryHash,
                string.Empty,
                "onboarding");
            return await repository.SaveAsync(saveData, cancellationToken);
        }

        public async Task<Result> ResetFirstRunOnboardingAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.OnboardingPreferences = saveData.OnboardingPreferences ?? new OnboardingPreferences();
            saveData.OnboardingPreferences.FirstRunOnboardingCompleted = false;
            saveData.OnboardingPreferences.CompletedAtUtc = null;
            saveData.OnboardingPreferences.LastOpenedAtUtc = DateTimeOffset.UtcNow;
            RepositoryCompanionProfileService.RecordTimelineEvent(
                saveData,
                "onboarding_reset",
                "Onboarding reset",
                "First-run onboarding will open again until it is completed.",
                saveData.SelectedRepositoryHash,
                string.Empty,
                "onboarding");
            return await repository.SaveAsync(saveData, cancellationToken);
        }

        public async Task<Result<DesktopCompanionSettings>> ResetDesktopCompanionPositionAsync(CancellationToken cancellationToken = default)
        {
            return await UpdateDesktopCompanionSettingsAsync(settings =>
            {
                settings.LastOverlayPositionXBucket = CountBucket.Unknown;
                settings.LastOverlayPositionYBucket = CountBucket.Unknown;
                settings.LastOverlayPositionX = -1f;
                settings.LastOverlayPositionY = -1f;
                settings.HasSavedOverlayPosition = false;
            }, cancellationToken);
        }

        public async Task<Result<DesktopCompanionSettings>> SaveDesktopCompanionPositionAsync(float x, float y, CancellationToken cancellationToken = default)
        {
            var result = await UpdateDesktopCompanionSettingsAsync(settings =>
            {
                settings.LastOverlayPositionX = Math.Max(0f, x);
                settings.LastOverlayPositionY = Math.Max(0f, y);
                settings.HasSavedOverlayPosition = true;
                settings.LastOverlayPositionXBucket = BucketForCoordinate(x);
                settings.LastOverlayPositionYBucket = BucketForCoordinate(y);
            }, cancellationToken);
            Debug.Log("INFO [CompanionDrag] SaveDataRepository position=(" + Math.Max(0f, x).ToString("0.##") + "," + Math.Max(0f, y).ToString("0.##") + ") saved=" + (result.IsSuccess ? "true" : "false"));
            return result;
        }

        public async Task<Result<DesktopCompanionSettings>> SaveDesktopCompanionPositionForRepositoryAsync(string repositoryId, float x, float y, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                return await SaveDesktopCompanionPositionAsync(x, y, cancellationToken);
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            saveData = RepositoryCompanionProfileService.Normalize(saveData);
            var profile = (saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
                .FirstOrDefault(item => string.Equals(item.RepositoryHash, repositoryId, StringComparison.Ordinal));
            if (profile == null)
            {
                return Result<DesktopCompanionSettings>.Failure("repository_profile_not_found", "Repository profile was not found.");
            }

            var settings = RepositoryCompanionProfileService.CloneDesktopCompanionSettings(profile.DesktopCompanionSettings);
            settings.LastOverlayPositionX = Math.Max(0f, x);
            settings.LastOverlayPositionY = Math.Max(0f, y);
            settings.HasSavedOverlayPosition = true;
            settings.LastOverlayPositionXBucket = BucketForCoordinate(x);
            settings.LastOverlayPositionYBucket = BucketForCoordinate(y);
            profile.DesktopCompanionSettings = settings;
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            if (string.Equals(saveData.SelectedRepositoryHash, repositoryId, StringComparison.Ordinal))
            {
                saveData.DesktopCompanionSettings = RepositoryCompanionProfileService.CloneDesktopCompanionSettings(settings);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            Debug.Log("INFO [OverlayPositionSync][SAVE] repo=" + repositoryId + " position=(" + Math.Max(0f, x).ToString("0.##") + "," + Math.Max(0f, y).ToString("0.##") + ")");
            if (!saveResult.IsSuccess)
            {
                return Result<DesktopCompanionSettings>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            await RefreshRecentSessionsAsync(cancellationToken);
            return Result<DesktopCompanionSettings>.Success(settings);
        }

        public Result DiscardGitReview()
        {
            var result = GitFlow.DiscardPendingReview();
            _ = ClearPersistedPendingNativeReviewAfterDiscardAsync();
            return result;
        }

        public Result DiscardAgentReview()
        {
            AgentSelectionStatus = "No agent log location selected";
            AgentPickerErrorCategory = string.Empty;
            var result = AgentFlow.DiscardPendingReview();
            _ = ClearPersistedPendingNativeReviewAfterDiscardAsync();
            return result;
        }

        private async Task ClearPersistedPendingNativeReviewAfterDiscardAsync()
        {
            var result = await ClearPersistedPendingNativeReviewAsync(CancellationToken.None);
            if (!result.IsSuccess)
            {
                Debug.LogWarning("WARN [Analysis] discard persisted review failed reason=" + result.ErrorCode);
            }
        }

        public async Task<Result<SaveData>> ApprovePendingNativeReviewAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            var pending = saveData.PendingNativeActivityReview;
            if (pending == null || pending.SafeSession == null || pending.GrowthResult == null)
            {
                if (GitFlow.HasPendingReview)
                {
                    return await SaveGitSessionAsync(cancellationToken);
                }

                if (AgentFlow.HasPendingReview)
                {
                    return await SaveAgentSessionAsync(cancellationToken);
                }

                return Result<SaveData>.Failure("missing_pending_review", "No pending review is available.");
            }

            saveData.AppliedNativeReviewIds = saveData.AppliedNativeReviewIds ?? new List<string>();
            var reviewId = string.IsNullOrWhiteSpace(pending.ReviewId) ? pending.SafeSession?.SessionId ?? string.Empty : pending.ReviewId;
            var alreadyApplied = !string.IsNullOrWhiteSpace(reviewId) &&
                saveData.AppliedNativeReviewIds.Contains(reviewId, StringComparer.Ordinal);
            var pendingSessions = PendingReviewSessions(pending);
            var pendingGrowthResults = PendingReviewGrowthResults(pending, pendingSessions);
            var sessionAlreadySaved = pendingSessions.Count > 0 &&
                pendingSessions.All(pendingSession => (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
                    .Any(session => string.Equals(session.SessionId, pendingSession.SessionId, StringComparison.Ordinal) ||
                                    (!string.IsNullOrWhiteSpace(pendingSession.DeduplicationKey) &&
                                     string.Equals(session.DeduplicationKey, pendingSession.DeduplicationKey, StringComparison.Ordinal))));
            if (alreadyApplied || sessionAlreadySaved)
            {
                saveData.PendingNativeActivityReview = null;
                var idempotentValidation = privacySanitizer.ValidateSafeSaveData(saveData);
                if (!idempotentValidation.IsSuccess)
                {
                    return Result<SaveData>.Failure(idempotentValidation.ErrorCode, idempotentValidation.ErrorMessage);
                }

                var idempotentSave = await repository.SaveAsync(saveData, cancellationToken);
                return idempotentSave.IsSuccess
                    ? Result<SaveData>.Success(saveData)
                    : Result<SaveData>.Failure(idempotentSave.ErrorCode, idempotentSave.ErrorMessage);
            }

            saveData.CharacterProfile = saveData.CharacterProfile ?? new CharacterProfile();
            saveData.CharacterProfile.Stats = saveData.CharacterProfile.Stats ?? CharacterStats.Zero();
            saveData.WorkSessionSummaries = saveData.WorkSessionSummaries ?? new List<AgentWorkSession>();
            saveData.GrowthHistory = saveData.GrowthHistory ?? new List<CharacterGrowthResult>();
            for (var index = 0; index < pendingSessions.Count; index++)
            {
                var session = pendingSessions[index];
                if (session == null || string.IsNullOrWhiteSpace(session.SessionId))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(pending.RepositoryHash))
                {
                    saveData.SelectedRepositoryHash = pending.RepositoryHash;
                    session.GitChangeSummary = session.GitChangeSummary ?? GitChangeSummary.Empty();
                    if (string.IsNullOrWhiteSpace(session.GitChangeSummary.ProjectPathHash))
                    {
                        session.GitChangeSummary.ProjectPathHash = pending.RepositoryHash;
                    }
                }

                if ((saveData.WorkSessionSummaries ?? new List<AgentWorkSession>()).Any(existing => string.Equals(existing.SessionId, session.SessionId, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(session.DeduplicationKey) &&
                    (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>()).Any(existing => string.Equals(existing.DeduplicationKey, session.DeduplicationKey, StringComparison.Ordinal)))
                {
                    continue;
                }

                var growth = index < pendingGrowthResults.Count ? pendingGrowthResults[index] : pending.GrowthResult;
                growth = growth ?? new CharacterGrowthResult { SessionId = session.SessionId, StatDeltas = CharacterStats.Zero() };
                growth.SessionId = string.IsNullOrWhiteSpace(growth.SessionId) ? session.SessionId : growth.SessionId;
                saveData.CharacterProfile.TotalExp += Math.Max(0, growth.ExpGained);
                saveData.CharacterProfile.Stats.Add(growth.StatDeltas ?? CharacterStats.Zero());
                saveData.WorkSessionSummaries.Add(session);
                saveData.GrowthHistory.Add(growth);
            }

            saveData.CharacterProfile.Level = Math.Max(saveData.CharacterProfile.Level, 1 + Math.Max(0, saveData.CharacterProfile.TotalExp) / 1000);

            RepositoryCompanionProfileService.Normalize(saveData);
            var hasSelectedRepository = !string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash) &&
                                        RepositoryCompanionProfileService.GetSelectedProfile(saveData) != null;
            if (hasSelectedRepository)
            {
                var repositorySessionIds = saveData.WorkSessionSummaries
                    .Where(session => string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), saveData.SelectedRepositoryHash, StringComparison.Ordinal))
                    .Select(session => session.SessionId)
                    .ToList();
                var profileSession = pendingSessions.LastOrDefault(session => string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), saveData.SelectedRepositoryHash, StringComparison.Ordinal)) ??
                                     pendingSessions.LastOrDefault() ??
                                     pending.SafeSession;
                RepositoryCompanionProfileService.ApplyApprovedGrowth(
                    saveData,
                    profileSession,
                    saveData.WorkSessionSummaries.Where(session => repositorySessionIds.Contains(session.SessionId)).ToList(),
                    saveData.GrowthHistory.Where(growth => repositorySessionIds.Contains(growth.SessionId)).ToList());
                ApplyRepositoryAnalysisCheckpoint(saveData, saveData.SelectedRepositoryHash, profileSession?.GitChangeSummary);
            }
            else
            {
                saveData.CompanionState = CompanionState.CreateDefault();
                Debug.Log("INFO [CompanionProfileGuard] blocked_default_profile_creation reason=no_connected_repository");
            }
            saveData.DailyProgress = saveData.DailyProgress ?? new DailyProgress();
            saveData.DailyProgress.ExpGainedToday += pendingGrowthResults.Sum(growth => Math.Max(0, growth?.ExpGained ?? 0));
            saveData.DailyProgress.SessionsConfirmedToday += Math.Max(1, pendingSessions.Count);
            if (!string.IsNullOrWhiteSpace(reviewId))
            {
                saveData.AppliedNativeReviewIds.Add(reviewId);
            }
            UpsertActivityReview(saveData, pending, "saved", DateTimeOffset.UtcNow);
            saveData.RecentNativeAnalysisRuns = saveData.RecentNativeAnalysisRuns ?? new List<NativeAnalysisRunRecord>();
            saveData.RecentNativeAnalysisRuns.Insert(0, new NativeAnalysisRunRecord
            {
                RunId = Guid.NewGuid().ToString("N"),
                SourceKind = "reviewSaved",
                Status = "saved",
                ErrorCode = string.Empty,
                SafeSummary = "Growth saved · +" + pendingGrowthResults.Sum(growth => Math.Max(0, growth?.ExpGained ?? 0)) + " XP",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            saveData.PendingNativeActivityReview = null;

            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return Result<SaveData>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return Result<SaveData>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            GitFlow.DiscardPendingReview();
            AgentFlow.DiscardPendingReview();
            await RefreshRecentSessionsAsync(cancellationToken);
            await RefreshSafeSyncLocalStateAsync(cancellationToken);
            return Result<SaveData>.Success(saveData);
        }

        public async Task<Result<SaveData>> LevelUpSelectedCompanionAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            RepositoryCompanionProfileService.Normalize(saveData);
            var profile = RepositoryCompanionProfileService.GetSelectedProfile(saveData);
            var companion = profile == null || profile.ArchivedAtUtc != null || string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash)
                ? CompanionProgressionRules.Normalize(saveData.CompanionState)
                : CompanionProgressionRules.Normalize(profile.CompanionState);
            if (companion == null)
            {
                return Result<SaveData>.Failure("no_companion_progress", "Earn XP before leveling up.");
            }

            var previousLevel = companion.Level;
            if (!CompanionProgressionRules.TryLevelUpOnce(companion))
            {
                return Result<SaveData>.Failure("level_up_not_ready", "Earn enough XP before leveling up.");
            }

            if (profile != null && profile.ArchivedAtUtc == null && !string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash))
            {
                profile.CompanionState = CompanionProgressionRules.Normalize(companion);
                profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            saveData.CompanionState = CompanionProgressionRules.Normalize(companion);
            saveData.CharacterProfile = saveData.CharacterProfile ?? new CharacterProfile();
            saveData.CharacterProfile.Level = Math.Max(saveData.CharacterProfile.Level, saveData.CompanionState.Level);
            var targetName = profile == null ? "Agent-only" : profile.SafeRepositoryAlias;
            saveData.RecentNativeAnalysisRuns = saveData.RecentNativeAnalysisRuns ?? new List<NativeAnalysisRunRecord>();
            saveData.RecentNativeAnalysisRuns.Insert(0, new NativeAnalysisRunRecord
            {
                RunId = Guid.NewGuid().ToString("N"),
                SourceKind = "levelUp",
                Status = "saved",
                ErrorCode = string.Empty,
                SafeSummary = "Level Up · Lv " + previousLevel + " -> Lv " + saveData.CompanionState.Level + " · " + targetName,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            saveData.ActivityReviews = saveData.ActivityReviews ?? new List<ActivityReview>();
            saveData.ActivityReviews.Insert(0, new ActivityReview
            {
                Id = Guid.NewGuid().ToString("N"),
                RepositoryId = profile == null ? "agent-only" : profile.RepositoryHash,
                SourceType = "levelUp",
                Status = "saved",
                XpDelta = 0,
                CategoryBreakdown = CharacterStats.Zero(),
                EvidenceSummary = "Level Up · Lv " + previousLevel + " -> Lv " + saveData.CompanionState.Level,
                CreatedAt = DateTimeOffset.UtcNow,
                SavedAt = DateTimeOffset.UtcNow
            });

            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return Result<SaveData>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return Result<SaveData>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            RefreshCharacterDashboard(saveData, RecentSessions);
            return Result<SaveData>.Success(saveData);
        }

        public async Task<Result<TokenShopPurchaseResult>> PurchaseTokenShopItemAsync(string itemId, CancellationToken cancellationToken = default)
        {
            return await PurchaseTokenShopItemAsync(itemId, ShopTargetType.RepositoryCompanion, string.Empty, true, cancellationToken);
        }

        public async Task<Result<TokenShopPurchaseResult>> PurchaseTokenShopItemAsync(
            string itemId,
            ShopTargetType targetType,
            string targetId,
            bool targetConnected,
            CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            var result = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, targetType, targetId, itemId, targetConnected);
            if (!result.IsSuccess)
            {
                UnityEngine.Debug.LogWarning("WARN [TokenShop][PURCHASE] itemId=" + (itemId ?? string.Empty) + " result=blocked reason=" + result.ErrorCode);
                return result;
            }

            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return Result<TokenShopPurchaseResult>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return Result<TokenShopPurchaseResult>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            RefreshCharacterDashboard(saveData, RecentSessions);
            return result;
        }

        public async Task<Result<TokenShopPurchaseResult>> EquipTokenShopItemAsync(
            string itemId,
            ShopTargetType targetType,
            string targetId,
            bool targetConnected,
            CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            var result = RepositoryCompanionProfileService.EquipTokenShopItem(saveData, targetType, targetId, itemId, targetConnected);
            if (!result.IsSuccess)
            {
                UnityEngine.Debug.LogWarning("WARN [TokenShop][EQUIP] itemId=" + (itemId ?? string.Empty) + " result=blocked reason=" + result.ErrorCode);
                return result;
            }

            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return Result<TokenShopPurchaseResult>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return Result<TokenShopPurchaseResult>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            RefreshCharacterDashboard(saveData, RecentSessions);
            return result;
        }

        private static List<AgentWorkSession> PendingReviewSessions(PendingNativeActivityReview pending)
        {
            if (pending == null)
            {
                return new List<AgentWorkSession>();
            }

            var sessions = (pending.SafeSessions ?? new List<AgentWorkSession>())
                .Where(session => session != null)
                .ToList();
            if (sessions.Count == 0 && pending.SafeSession != null)
            {
                sessions.Add(pending.SafeSession);
            }

            return sessions
                .Where(session => !string.IsNullOrWhiteSpace(session.SessionId))
                .GroupBy(session => session.SessionId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        private static List<CharacterGrowthResult> PendingReviewGrowthResults(PendingNativeActivityReview pending, List<AgentWorkSession> sessions)
        {
            sessions = sessions ?? new List<AgentWorkSession>();
            var growthResults = (pending?.GrowthResults ?? new List<CharacterGrowthResult>())
                .Where(growth => growth != null)
                .ToList();
            if (growthResults.Count == 0 && pending?.GrowthResult != null)
            {
                growthResults.Add(pending.GrowthResult);
            }

            if (growthResults.Count == sessions.Count)
            {
                return growthResults;
            }

            return sessions.Select(session => new CharacterGrowthResult
            {
                SessionId = session.SessionId,
                ExpGained = Math.Max(0, pending?.EstimatedXpDelta ?? 0),
                StatDeltas = pending?.StatDeltas ?? CharacterStats.Zero(),
                LevelBefore = pending?.GrowthResult?.LevelBefore ?? 1,
                LevelAfter = pending?.GrowthResult?.LevelAfter ?? 1
            }).ToList();
        }

        private static List<CharacterGrowthResult> BuildCombinedGrowthResults(
            SaveData saveData,
            List<AgentWorkSession> sessions,
            GitAnalysisReviewModel gitReview,
            AgentAnalysisReviewModel agentReview)
        {
            var profile = saveData?.CharacterProfile ?? new CharacterProfile();
            var runningExp = Math.Max(0, profile.TotalExp);
            var levelBefore = Math.Max(1, profile.Level);
            var parts = new[]
            {
                new { Session = sessions.ElementAtOrDefault(0), Xp = Math.Max(0, gitReview?.DerivedExpGained ?? 0), Stats = gitReview?.DerivedStatDeltas ?? CharacterStats.Zero() },
                new { Session = sessions.ElementAtOrDefault(1), Xp = Math.Max(0, agentReview?.DerivedExpGained ?? 0), Stats = agentReview?.DerivedStatDeltas ?? CharacterStats.Zero() }
            };

            var results = new List<CharacterGrowthResult>();
            foreach (var part in parts)
            {
                if (part.Session == null)
                {
                    continue;
                }

                var before = Math.Max(levelBefore, 1 + runningExp / 1000);
                runningExp += part.Xp;
                results.Add(new CharacterGrowthResult
                {
                    SessionId = part.Session.SessionId,
                    ExpGained = part.Xp,
                    LevelBefore = before,
                    LevelAfter = Math.Max(before, 1 + runningExp / 1000),
                    StatDeltas = part.Stats
                });
            }

            return results;
        }

        private static string CombinedConfidence(ConfidenceLevel git, ConfidenceLevel agent)
        {
            if (git == ConfidenceLevel.Low || agent == ConfidenceLevel.Low)
            {
                return ConfidenceLevel.Low.ToString();
            }

            if (git == ConfidenceLevel.Medium || agent == ConfidenceLevel.Medium)
            {
                return ConfidenceLevel.Medium.ToString();
            }

            if (git == ConfidenceLevel.High && agent == ConfidenceLevel.High)
            {
                return ConfidenceLevel.High.ToString();
            }

            return ConfidenceLevel.Unknown.ToString();
        }

        public async Task<Result> DiscardPendingNativeReviewAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            if (saveData.PendingNativeActivityReview != null)
            {
                UpsertActivityReview(saveData, saveData.PendingNativeActivityReview, "discarded", null);
                saveData.PendingNativeActivityReview = null;
                await repository.SaveAsync(saveData, cancellationToken);
            }

            GitFlow.DiscardPendingReview();
            AgentFlow.DiscardPendingReview();
            return await ClearPersistedPendingNativeReviewAsync(cancellationToken);
        }

        private async Task<Result> PersistPendingNativeReviewAsync(
            string sourceKind,
            AgentWorkSession session,
            int estimatedXpDelta,
            CharacterStats statDeltas,
            string confidence,
            IEnumerable<string> warnings,
            string fallbackSummary,
            CancellationToken cancellationToken)
        {
            if (session == null)
            {
                return Result.Failure("missing_pending_session", "No eligible activity found yet.");
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            RepositoryCompanionProfileService.Normalize(saveData);
            var repositoryHash = RepositoryCompanionProfileService.SafeRepositoryHashForSession(session);
            if (string.IsNullOrWhiteSpace(repositoryHash))
            {
                repositoryHash = string.Equals(sourceKind, "aiAgent", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : saveData.SelectedRepositoryHash;
            }

            if (!string.IsNullOrWhiteSpace(repositoryHash))
            {
                session.GitChangeSummary = session.GitChangeSummary ?? GitChangeSummary.Empty();
                session.GitChangeSummary.ProjectPathHash = repositoryHash;
            }

            statDeltas = statDeltas ?? CharacterStats.Zero();
            var profile = saveData.CharacterProfile ?? new CharacterProfile();
            var levelBefore = Math.Max(1, profile.Level);
            var levelAfter = Math.Max(levelBefore, 1 + (Math.Max(0, profile.TotalExp) + Math.Max(0, estimatedXpDelta)) / 1000);
            var growth = new CharacterGrowthResult
            {
                SessionId = session.SessionId,
                ExpGained = Math.Max(0, estimatedXpDelta),
                LevelBefore = levelBefore,
                LevelAfter = levelAfter,
                StatDeltas = statDeltas
            };

            saveData.PendingNativeActivityReview = new PendingNativeActivityReview
            {
                ReviewId = Guid.NewGuid().ToString("N"),
                SourceKind = sourceKind ?? string.Empty,
                RepositoryHash = repositoryHash ?? string.Empty,
                SafeSummary = SafeLocalAlias(fallbackSummary, "Aggregate activity ready for review."),
                ActivityCategory = session.WorkType.ToString(),
                Confidence = string.IsNullOrWhiteSpace(confidence) ? session.Confidence.ToString() : confidence,
                CommitCountBucket = session.GitChangeSummary?.CommitCountBucket ?? CountBucket.Unknown,
                ChangedFileCountBucket = session.GitChangeSummary?.ChangedFileCountBucket ?? CountBucket.Unknown,
                EstimatedXpDelta = Math.Max(0, estimatedXpDelta),
                StatDeltas = statDeltas,
                WarningIds = (warnings ?? Enumerable.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Take(6).ToList(),
                SafeSession = session,
                GrowthResult = growth,
                SafeSessions = new List<AgentWorkSession> { session },
                GrowthResults = new List<CharacterGrowthResult> { growth },
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            UpsertActivityReview(saveData, saveData.PendingNativeActivityReview, "pending", null);

            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (saveResult.IsSuccess)
            {
                RefreshCharacterDashboard(saveData, RecentSessions);
            }

            return saveResult;
        }

        private static void UpsertActivityReview(SaveData saveData, PendingNativeActivityReview pending, string status, DateTimeOffset? savedAt)
        {
            if (saveData == null || pending == null)
            {
                return;
            }

            saveData.ActivityReviews = saveData.ActivityReviews ?? new List<ActivityReview>();
            var id = string.IsNullOrWhiteSpace(pending.ReviewId) ? pending.SafeSession?.SessionId ?? Guid.NewGuid().ToString("N") : pending.ReviewId;
            var review = saveData.ActivityReviews.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (review == null)
            {
                review = new ActivityReview { Id = id, CreatedAt = pending.CreatedAtUtc };
                saveData.ActivityReviews.Insert(0, review);
            }

            review.RepositoryId = pending.RepositoryHash ?? string.Empty;
            if (string.IsNullOrWhiteSpace(review.RepositoryId) &&
                string.Equals(NormalizeActivitySourceType(pending.SourceKind), "aiAgent", StringComparison.Ordinal))
            {
                review.RepositoryId = "agent-only";
            }
            review.SourceType = NormalizeActivitySourceType(pending.SourceKind);
            review.ProviderId = PendingProviderId(pending);
            review.Status = string.IsNullOrWhiteSpace(status) ? "pending" : status;
            review.XpDelta = Math.Max(0, pending.EstimatedXpDelta);
            review.CategoryBreakdown = pending.StatDeltas ?? CharacterStats.Zero();
            review.EvidenceSummary = SafeLocalAlias(pending.SafeSummary, "Aggregate activity ready for review.");
            review.Warnings = (pending.WarningIds ?? new List<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Take(8).ToList();
            review.SavedAt = savedAt;
            saveData.ActivityReviews = saveData.ActivityReviews.Take(100).ToList();
        }

        private static string NormalizeActivitySourceType(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.IndexOf("repository", StringComparison.OrdinalIgnoreCase) >= 0) return "repository";
            if (value.IndexOf("combined", StringComparison.OrdinalIgnoreCase) >= 0) return "mixed";
            if (value.Length > 0) return "aiAgent";
            return "repository";
        }

        private static string PendingProviderId(PendingNativeActivityReview pending)
        {
            var session = pending?.SafeSessions?.FirstOrDefault(item => item?.AgentActivitySummary != null) ??
                          (pending?.SafeSession?.AgentActivitySummary != null ? pending.SafeSession : null);
            var provider = session?.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown;
            if (provider == AgentProviderType.Unknown)
            {
                return string.Empty;
            }

            switch (MacAgentSourceDetector.NormalizeProvider(provider))
            {
                case AgentProviderType.Codex: return "codex";
                case AgentProviderType.ClaudeCode: return "claude";
                case AgentProviderType.Cursor: return "cursor";
                case AgentProviderType.GitHubCopilot: return "copilot";
                case AgentProviderType.GeminiCli: return "gemini";
                case AgentProviderType.Manual: return "manual";
                default: return provider.ToString().ToLowerInvariant();
            }
        }

        private async Task<Result> ClearPersistedPendingNativeReviewAsync(CancellationToken cancellationToken)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.PendingNativeActivityReview = null;
            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (saveResult.IsSuccess)
            {
                RefreshCharacterDashboard(saveData, RecentSessions);
            }

            return saveResult;
        }

        private async Task<Result<DesktopCompanionSettings>> UpdateDesktopCompanionSettingsAsync(Action<DesktopCompanionSettings> update, CancellationToken cancellationToken)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            RepositoryCompanionProfileService.Normalize(saveData);
            var settings = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);
            update?.Invoke(settings);
            settings.SchemaVersion = 2;
            RepositoryCompanionProfileService.SetSelectedDesktopCompanionSettings(saveData, settings);
            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return Result<DesktopCompanionSettings>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return Result<DesktopCompanionSettings>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            RefreshCharacterDashboard(saveData, RecentSessions);
            return Result<DesktopCompanionSettings>.Success(settings);
        }

        private static CountBucket BucketForCoordinate(float coordinate)
        {
            if (coordinate < 0f) return CountBucket.Unknown;
            if (coordinate < 160f) return CountBucket.Small;
            if (coordinate < 640f) return CountBucket.Medium;
            if (coordinate < 1440f) return CountBucket.Large;
            return CountBucket.Huge;
        }

        public async Task<Result<List<RecentSafeSessionSummary>>> RefreshRecentSessionsAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            var growthBySessionId = (saveData.GrowthHistory ?? new List<CharacterGrowthResult>())
                .Where(growth => !string.IsNullOrWhiteSpace(growth.SessionId))
                .GroupBy(growth => growth.SessionId)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
            var summaries = (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .OrderByDescending(session => session.EndedAt)
                .Take(RecentSessionLimit)
                .Select(session => ToRecentSummary(session, growthBySessionId))
                .Where(summary => privacySanitizer.ValidateNoForbiddenFields(summary).IsSuccess)
                .ToList();

            RecentSessions = summaries;
            RefreshCharacterDashboard(saveData, summaries);
            return Result<List<RecentSafeSessionSummary>>.Success(summaries);
        }

        public async Task<Result<List<ApprovedLocationDisplayItem>>> RefreshApprovedLocationsAsync(CancellationToken cancellationToken = default)
        {
            var settings = await approvedLocationRepository.LoadAsync(cancellationToken);
            var safeDisplayItems = (settings.Locations ?? new List<ApprovedLocationEntry>())
                .OrderBy(item => item.SourceType)
                .ThenBy(item => item.DisplayAlias, StringComparer.Ordinal)
                .Select(ToDisplayItem)
                .Where(item => privacySanitizer.ValidateNoForbiddenFields(item).IsSuccess)
                .ToList();

            ApprovedGitLocations = safeDisplayItems
                .Where(item => item.SourceType == ApprovedLocationSourceType.Git)
                .ToList();
            ApprovedAgentLocations = safeDisplayItems
                .Where(item => item.SourceType != ApprovedLocationSourceType.Git)
                .ToList();
            Debug.Log("INFO [RepositoryProjection][RELOAD] source=tabNavigation count=" + ApprovedGitLocations.Count);

            return Result<List<ApprovedLocationDisplayItem>>.Success(safeDisplayItems);
        }

        public async Task RefreshDashboardAsync(CancellationToken cancellationToken = default)
        {
            if (authSessionService != null)
            {
                await LoadAuthSessionAsync(cancellationToken);
            }

            await RefreshApprovedLocationsAsync(cancellationToken);
            await RestoreLocalSelectionsFromApprovedLocationsAsync(cancellationToken);
            await RefreshRecentSessionsAsync(cancellationToken);
            await RefreshSafeSyncLocalStateAsync(cancellationToken);
        }

        public async Task RestoreLocalSelectionsFromApprovedLocationsAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            RepositoryCompanionProfileService.Normalize(saveData);
            var settings = await approvedLocationRepository.LoadAsync(cancellationToken);
            var locations = settings.Locations ?? new List<ApprovedLocationEntry>();
            RestoreProviderSelectionState(saveData.ProviderSettings);

            if (!GitFlow.HasSelectedRepositoryForLocalOnlyApproval && !string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash))
            {
                var gitLocation = locations
                    .Where(item => item.Enabled && item.SourceType == ApprovedLocationSourceType.Git && !string.IsNullOrWhiteSpace(item.LocalPath))
                    .FirstOrDefault(item => string.Equals(
                        RepositoryCompanionProfileService.HashRepositoryPath(item.LocalPath),
                        saveData.SelectedRepositoryHash,
                        StringComparison.Ordinal));
                if (gitLocation != null)
                {
                    await GitFlow.SelectLocalOnlyApprovedRepositoryPathAsync(gitLocation.LocalPath, cancellationToken);
                    Onboarding.GitConnected = true;
                    Onboarding.GitSafeAlias = gitLocation.DisplayAlias;
                }
            }

            if (!AgentFlow.HasSelectedAgentLogLocationForLocalOnlyApproval)
            {
                var restoredProvider = false;
                foreach (var setting in saveData.ProviderSettings ?? new List<ProviderSettings>())
                {
                    if (!setting.Enabled || !setting.Selected)
                    {
                        continue;
                    }

                    var providerType = MacAgentSourceDetector.NormalizeProviderValue(setting.ProviderId);
                    if (providerType == AgentProviderType.Unknown)
                    {
                        continue;
                    }

                    var locationSourceType = providerType.ToApprovedLocationSourceType();
                    var location = locations.FirstOrDefault(item => item.Enabled && item.SourceType == locationSourceType && !string.IsNullOrWhiteSpace(item.LocalPath));
                    if (location == null || !LocalSourceExists(location.LocalPath))
                    {
                        continue;
                    }

                    SelectedAgentProviderType = providerType;
                    var input = AgentSettings.ToInput(location.LocalPath, providerType);
                    input.SourceKind = AgentSourceKind.ManualFolder;
                    input.SafeSourceAlias = location.DisplayAlias;
                    if (AgentFlow.SelectApprovedLogLocation(input).IsSuccess)
                    {
                        var source = Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == ToConnectedAgentSourceType(providerType));
                        if (source != null)
                        {
                            source.Selected = true;
                            source.State = AgentSourceSetupState.ReadyToAnalyze;
                            source.StatusLabel = "Ready to analyze";
                            source.SafeLabel = location.DisplayAlias;
                            source.SafeLocationHash = SafeHashUtility.ComputeProjectPathHash(location.LocalPath, "TokenForge.AgentLogLocation.v1");
                            source.Confidence = ConfidenceLevel.Medium;
                        }

                        restoredProvider = true;
                        break;
                    }
                }

                if (!restoredProvider)
                {
                    foreach (var source in Onboarding.AgentSources.Where(item => item.Selected && IsSourceReadyState(item.State)))
                    {
                        source.Selected = false;
                        source.State = AgentSourceSetupState.PermissionRequired;
                        source.StatusLabel = "No approved source is readable. Choose a folder before analysis.";
                        source.WarningCount = Math.Max(1, source.WarningCount);
                    }
                }
            }
        }

        private void RestoreProviderSelectionState(List<ProviderSettings> providerSettings)
        {
            foreach (var setting in providerSettings ?? new List<ProviderSettings>())
            {
                var providerType = MacAgentSourceDetector.NormalizeProviderValue(setting.ProviderId);
                var sourceType = ToConnectedAgentSourceType(providerType);
                var source = Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
                if (source == null)
                {
                    continue;
                }

                source.Selected = setting.Selected && setting.Enabled;
                source.SafeLocationHash = setting.SafeLocationHash ?? string.Empty;
                source.State = setting.Detected && !source.Selected
                    ? AgentSourceSetupState.LocalSourceDetected
                    : ParseSourceState(setting.ConnectionState, source.Selected);
                source.SafeLabel = source.Selected
                    ? (string.IsNullOrWhiteSpace(setting.ApprovedSource) ? source.SafeLabel : setting.ApprovedSource)
                    : ((setting.DetectedSources ?? new List<string>()).FirstOrDefault() ?? string.Empty);
                source.Confidence = Enum.TryParse(setting.Confidence, true, out ConfidenceLevel confidence)
                    ? confidence
                    : ConfidenceLevel.Unknown;
                source.WarningCount = setting.Warnings?.Count ?? 0;
                if (IsSourceReadyState(source.State) && string.IsNullOrWhiteSpace(source.SafeLocationHash))
                {
                    source.Selected = false;
                    source.State = AgentSourceSetupState.NotSelected;
                    source.SafeLabel = string.Empty;
                    source.Confidence = ConfidenceLevel.Unknown;
                    source.WarningCount = 0;
                }

                source.StatusLabel = source.State == AgentSourceSetupState.LocalSourceDetected && !source.Selected
                    ? "Detected source found. Connect to approve."
                    : source.Selected
                        ? StatusLabelForRestoredState(source.State)
                        : "Not selected";
            }
        }

        private async Task<Result> RestoreSelectedRepositoryPathAsync(string repositoryHash, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(repositoryHash))
            {
                return Result.Failure("missing_repository_hash", "Repository profile is required.");
            }

            var settings = await approvedLocationRepository.LoadAsync(cancellationToken);
            var gitLocation = (settings.Locations ?? new List<ApprovedLocationEntry>())
                .Where(item => item.Enabled && item.SourceType == ApprovedLocationSourceType.Git && !string.IsNullOrWhiteSpace(item.LocalPath))
                .FirstOrDefault(item => string.Equals(
                    RepositoryCompanionProfileService.HashRepositoryPath(item.LocalPath),
                    repositoryHash,
                    StringComparison.Ordinal));
            if (gitLocation == null)
            {
                return Result.Failure("approved_location_not_found", "Approved Git location was not found.");
            }

            return await GitFlow.SelectLocalOnlyApprovedRepositoryPathAsync(gitLocation.LocalPath, cancellationToken);
        }

        private static bool IsSourceReadyState(AgentSourceSetupState state)
        {
            return state == AgentSourceSetupState.ReadyToAnalyze ||
                   state == AgentSourceSetupState.AnalysisComplete ||
                   state == AgentSourceSetupState.LocalSourceDetected;
        }

        private static bool LocalSourceExists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && (Directory.Exists(path) || File.Exists(path));
        }

        public void SetSafeSyncBaseUrl(string baseUrl)
        {
            safeSyncService?.SetBaseUrl(baseUrl);
            authSessionService?.SetBaseUrl(baseUrl);
        }

        public async Task<AuthResult> LoadAuthSessionAsync(CancellationToken cancellationToken = default)
        {
            if (authSessionService == null)
            {
                AuthErrorCode = "AUTH_NOT_CONFIGURED";
                AuthMessage = "Server auth is not available in this build. Continue Offline is available.";
                HasExplicitAuthMessage = true;
                return AuthResult.Failure(AuthState.LoggedOut, AuthErrorCode, "Authentication is not configured.");
            }

            var result = await authSessionService.LoadSessionAsync(cancellationToken);
            SetAuthResult(result);
            return result;
        }

        public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (authSessionService == null)
            {
                AuthErrorCode = "AUTH_NOT_CONFIGURED";
                AuthMessage = "Server login is not available in this build. Continue Offline is available.";
                HasExplicitAuthMessage = true;
                return AuthResult.Failure(AuthState.LoggedOut, AuthErrorCode, "Authentication is not configured.");
            }

            if (Interlocked.Exchange(ref loginInProgress, 1) == 1)
            {
                return SetAuthResult(AuthResult.Failure(AuthState, AuthApiError.AlreadyInProgress, AuthApiError.ToSafeMessage(AuthApiError.AlreadyInProgress)));
            }

            try
            {
                var result = await authSessionService.LoginAsync(email, password, cancellationToken);
                return SetAuthResult(result);
            }
            finally
            {
                Interlocked.Exchange(ref loginInProgress, 0);
            }
        }

        public async Task<AuthResult> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default)
        {
            if (authSessionService == null)
            {
                AuthErrorCode = "AUTH_NOT_CONFIGURED";
                AuthMessage = "Server account creation is not available in this build. Continue Offline is available.";
                HasExplicitAuthMessage = true;
                return AuthResult.Failure(AuthState.LoggedOut, AuthErrorCode, "Authentication is not configured.");
            }

            if (Interlocked.Exchange(ref signupInProgress, 1) == 1)
            {
                return SetAuthResult(AuthResult.Failure(AuthState, AuthApiError.AlreadyInProgress, AuthApiError.ToSafeMessage(AuthApiError.AlreadyInProgress)));
            }

            try
            {
                var result = await authSessionService.SignupAsync(email, password, displayName, cancellationToken);
                return SetAuthResult(result);
            }
            finally
            {
                Interlocked.Exchange(ref signupInProgress, 0);
            }
        }

        public async Task<AuthResult> LoadCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            if (authSessionService == null)
            {
                AuthErrorCode = "AUTH_NOT_CONFIGURED";
                return AuthResult.Failure(AuthState.LoggedOut, AuthErrorCode, "Authentication is not configured.");
            }

            if (Interlocked.Exchange(ref refreshUserInProgress, 1) == 1)
            {
                return SetAuthResult(AuthResult.Failure(AuthState, AuthApiError.AlreadyInProgress, AuthApiError.ToSafeMessage(AuthApiError.AlreadyInProgress)));
            }

            try
            {
                var result = await authSessionService.LoadCurrentUserAsync(cancellationToken);
                return SetAuthResult(result);
            }
            finally
            {
                Interlocked.Exchange(ref refreshUserInProgress, 0);
            }
        }

        public async Task<AuthResult> LogoutAsync(CancellationToken cancellationToken = default)
        {
            if (authSessionService == null)
            {
                AuthErrorCode = "AUTH_NOT_CONFIGURED";
                return AuthResult.Failure(AuthState.LoggedOut, AuthErrorCode, "Authentication is not configured.");
            }

            if (Interlocked.Exchange(ref logoutInProgress, 1) == 1)
            {
                return SetAuthResult(AuthResult.Failure(AuthState, AuthApiError.AlreadyInProgress, AuthApiError.ToSafeMessage(AuthApiError.AlreadyInProgress)));
            }

            try
            {
                var result = await authSessionService.LogoutAsync(cancellationToken);
                RemoteSafeSessions = new List<RemoteSafeSessionSummary>();
                return SetAuthResult(result);
            }
            finally
            {
                Interlocked.Exchange(ref logoutInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> CheckSyncHealthAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            if (Interlocked.Exchange(ref healthInProgress, 1) == 1)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.CheckingHealth, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress)));
            }

            try
            {
            SafeSyncStatus = SafeSyncStatus.CheckingHealth;
            return SetSafeSyncResult(await safeSyncService.CheckHealthAsync(cancellationToken));
            }
            finally
            {
                Interlocked.Exchange(ref healthInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> SyncSafeSessionsAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            if (!CanUseAuthenticatedSafeSync)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.AuthRequired, SafeSyncApiError.AuthRequired, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AuthRequired)));
            }

            if (Interlocked.Exchange(ref syncInProgress, 1) == 1)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.Syncing, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress)));
            }

            try
            {
            SafeSyncStatus = SafeSyncStatus.Syncing;
            return SetSafeSyncResult(await safeSyncService.EnqueueSyncSafeSessionsAsync(cancellationToken));
            }
            finally
            {
                Interlocked.Exchange(ref syncInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> FetchSyncedSessionsAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            if (!CanUseAuthenticatedSafeSync)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.AuthRequired, SafeSyncApiError.AuthRequired, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AuthRequired)));
            }

            if (Interlocked.Exchange(ref fetchInProgress, 1) == 1)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.Fetching, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress)));
            }

            try
            {
            SafeSyncStatus = SafeSyncStatus.Fetching;
            return SetSafeSyncResult(await safeSyncService.FetchRemoteSessionsAsync(cancellationToken));
            }
            finally
            {
                Interlocked.Exchange(ref fetchInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> DeleteRemoteSessionAsync(string serverSessionId, CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            if (!CanUseAuthenticatedSafeSync)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.AuthRequired, SafeSyncApiError.AuthRequired, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AuthRequired)));
            }

            if (Interlocked.Exchange(ref deleteInProgress, 1) == 1)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.DeleteInProgress, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress)));
            }

            try
            {
            SafeSyncStatus = SafeSyncStatus.DeleteInProgress;
            var result = SetSafeSyncResult(await safeSyncService.DeleteRemoteSessionAsync(serverSessionId, cancellationToken));
            if (result.IsSuccess)
            {
                await FetchSyncedSessionsAsync(cancellationToken);
            }

            return result;
            }
            finally
            {
                Interlocked.Exchange(ref deleteInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> DeleteLocalSavedSessionAsync(string clientSessionId, CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            if (Interlocked.Exchange(ref localDeleteInProgress, 1) == 1)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.DeleteInProgress, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress)));
            }

            try
            {
                SafeSyncStatus = SafeSyncStatus.DeleteInProgress;
                await Task.Yield();
                var result = SetSafeSyncResult(await safeSyncService.DeleteLocalSavedSessionAsync(clientSessionId, cancellationToken));
                await RefreshRecentSessionsAsync(cancellationToken);
                return result;
            }
            finally
            {
                Interlocked.Exchange(ref localDeleteInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> RetryPendingSafeSyncAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            if (!CanUseAuthenticatedSafeSync)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.AuthRequired, SafeSyncApiError.AuthRequired, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AuthRequired)));
            }

            if (Interlocked.Exchange(ref retryInProgress, 1) == 1)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.RetryInProgress, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress)));
            }

            try
            {
                SafeSyncStatus = SafeSyncStatus.RetryInProgress;
                return SetSafeSyncResult(await safeSyncService.ProcessAllEligibleRetryEntriesOnceAsync(cancellationToken));
            }
            finally
            {
                Interlocked.Exchange(ref retryInProgress, 0);
            }
        }

        public SafeSyncConfirmationRequest BeginProcessRetryBatchConfirmation()
        {
            return SetPendingConfirmation(
                SafeSyncConfirmationRequestFactory.ForRetryAction(SafeSyncConfirmationActionType.ProcessRetryBatch, RetryQueueSummary),
                token => RetryPendingSafeSyncAsync(token));
        }

        public async Task<SafeSyncResult> ForceRetryEntryAsync(string queueEntryId, bool explicitConfirmation, CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            if (!CanUseAuthenticatedSafeSync)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.AuthRequired, SafeSyncApiError.ForcedRetrySkippedAuthPaused, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ForcedRetrySkippedAuthPaused)));
            }

            if (Interlocked.Exchange(ref retryInProgress, 1) == 1)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.RetryInProgress, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress)));
            }

            try
            {
                SafeSyncStatus = SafeSyncStatus.RetryInProgress;
                return SetSafeSyncResult(await safeSyncService.ForceRetryEntryAsync(queueEntryId, explicitConfirmation, cancellationToken));
            }
            finally
            {
                Interlocked.Exchange(ref retryInProgress, 0);
            }
        }

        public SafeSyncConfirmationRequest BeginForceRetryEntryConfirmation(string queueEntryId)
        {
            var selected = (RetryQueueSummary.SafeEntries ?? new List<SafeSyncRetryQueueEntry>())
                .FirstOrDefault(entry => string.Equals(entry.QueueEntryId, queueEntryId, StringComparison.Ordinal));
            return SetPendingConfirmation(
                SafeSyncConfirmationRequestFactory.ForRetryAction(SafeSyncConfirmationActionType.ForceRetry, RetryQueueSummary, selected),
                token => ForceRetryEntryAsync(queueEntryId, true, token));
        }

        public SafeSyncConfirmationRequest BeginCancelRetryBatchConfirmation(Func<CancellationToken, Task<SafeSyncResult>> action)
        {
            return SetPendingConfirmation(
                SafeSyncConfirmationRequestFactory.ForRetryAction(SafeSyncConfirmationActionType.CancelRetryBatch, RetryQueueSummary),
                action);
        }

        public async Task<SafeSyncResult> EnqueuePendingTombstoneDeletesAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            if (Interlocked.Exchange(ref tombstoneInProgress, 1) == 1)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.RetryInProgress, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress)));
            }

            try
            {
                SafeSyncStatus = SafeSyncStatus.RetryInProgress;
                await Task.Yield();
                return SetSafeSyncResult(await safeSyncService.EnqueuePendingTombstoneDeletesAsync(cancellationToken));
            }
            finally
            {
                Interlocked.Exchange(ref tombstoneInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> ProcessPendingTombstoneDeletesOnceAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            if (!CanUseAuthenticatedSafeSync)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.AuthRequired, SafeSyncApiError.AuthRequired, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AuthRequired)));
            }

            if (Interlocked.Exchange(ref tombstoneInProgress, 1) == 1)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.RetryInProgress, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress)));
            }

            try
            {
                SafeSyncStatus = SafeSyncStatus.DeleteInProgress;
                await Task.Yield();
                return SetSafeSyncResult(await safeSyncService.ProcessPendingTombstoneDeletesOnceAsync(cancellationToken));
            }
            finally
            {
                Interlocked.Exchange(ref tombstoneInProgress, 0);
            }
        }

        public SafeSyncConfirmationRequest BeginProcessTombstoneBatchConfirmation()
        {
            return SetPendingConfirmation(
                SafeSyncConfirmationRequestFactory.ForTombstoneAction(SafeSyncConfirmationActionType.ProcessTombstoneBatch, TombstoneSummary),
                token => ProcessPendingTombstoneDeletesOnceAsync(token));
        }

        public SafeSyncConfirmationRequest BeginCancelTombstoneBatchConfirmation(Func<CancellationToken, Task<SafeSyncResult>> action, string tombstoneId = "")
        {
            var selected = (TombstoneSummary.SafeTombstones ?? new List<SafeSyncTombstone>())
                .FirstOrDefault(item => string.Equals(item.TombstoneId, tombstoneId, StringComparison.Ordinal));
            return SetPendingConfirmation(
                SafeSyncConfirmationRequestFactory.ForTombstoneAction(SafeSyncConfirmationActionType.CancelTombstoneBatch, TombstoneSummary, selected),
                action);
        }

        public async Task<SafeSyncResult> CancelTombstoneAsync(string tombstoneId, CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            return SetSafeSyncResult(await safeSyncService.CancelTombstoneAsync(tombstoneId, cancellationToken));
        }

        public async Task<SafeSyncResult> CancelAllFailedTombstonesAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            return SetSafeSyncResult(await safeSyncService.CancelAllFailedTombstonesAsync(cancellationToken));
        }

        public async Task<SafeSyncResult> ClearResolvedTombstonesAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            return SetSafeSyncResult(await safeSyncService.ClearResolvedTombstonesAsync(cancellationToken));
        }

        public async Task<SafeSyncResult> KeepLocalConflictAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            return await RunConflictActionAsync(() => safeSyncService.KeepLocalConflictAsync(conflictId, cancellationToken));
        }

        public SafeSyncConfirmationRequest BeginKeepLocalConflictConfirmation(string conflictId)
        {
            return BeginConflictConfirmation(conflictId, SafeSyncConfirmationActionType.KeepLocal, token => KeepLocalConflictAsync(conflictId, token));
        }

        public async Task<SafeSyncResult> KeepRemoteConflictAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            return await RunConflictActionAsync(() => safeSyncService.KeepRemoteConflictAsync(conflictId, cancellationToken));
        }

        public SafeSyncConfirmationRequest BeginKeepRemoteConflictConfirmation(string conflictId)
        {
            return BeginConflictConfirmation(conflictId, SafeSyncConfirmationActionType.KeepRemote, token => KeepRemoteConflictAsync(conflictId, token));
        }

        public async Task<SafeSyncResult> MarkConflictResolvedAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            return await RunConflictActionAsync(() => safeSyncService.MarkConflictResolvedAsync(conflictId, cancellationToken));
        }

        public SafeSyncConfirmationRequest BeginMarkConflictResolvedConfirmation(string conflictId)
        {
            return BeginConflictConfirmation(conflictId, SafeSyncConfirmationActionType.MarkConflictResolved, token => MarkConflictResolvedAsync(conflictId, token));
        }

        public async Task<SafeSyncResult> CancelConflictResolutionAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            return await RunConflictActionAsync(() => safeSyncService.CancelConflictResolutionAsync(conflictId, cancellationToken));
        }

        public async Task<SafeConflictMergePreview> PreviewConflictMergePolicyAsync(string conflictId, SafeConflictMergePolicy policy, CancellationToken cancellationToken = default)
        {
            return safeSyncService == null
                ? new SafeConflictMergePreview { ConflictId = conflictId ?? string.Empty, SelectedPolicy = policy, CanApply = false, BlockedReason = "SYNC_NOT_CONFIGURED" }
                : await safeSyncService.GetConflictMergePreviewAsync(conflictId, policy, cancellationToken);
        }

        public async Task<SafeSyncConfirmationRequest> BeginApplyConflictMergePolicyConfirmationAsync(string conflictId, SafeConflictMergePolicy policy, CancellationToken cancellationToken = default)
        {
            var preview = await PreviewConflictMergePolicyAsync(conflictId, policy, cancellationToken);
            if (!preview.CanApply)
            {
                SetSafeSyncResult(SafeSyncResult.Failure(
                    SafeSyncStatus.ConflictDetected,
                    string.IsNullOrWhiteSpace(preview.BlockedReason) ? SafeSyncApiError.MergePolicyPreviewBlocked : preview.BlockedReason,
                    SafeSyncApiError.ToSafeMessage(string.IsNullOrWhiteSpace(preview.BlockedReason) ? SafeSyncApiError.MergePolicyPreviewBlocked : preview.BlockedReason)));
                return null;
            }

            return SetPendingConfirmation(
                SafeSyncConfirmationRequestFactory.ForMergePreview(preview),
                token => ApplyConflictMergePolicyAsync(conflictId, policy, true, token));
        }

        public async Task<SafeSyncResult> ApplyConflictMergePolicyAsync(string conflictId, SafeConflictMergePolicy policy, bool explicitConfirmation, CancellationToken cancellationToken = default)
        {
            return await RunConflictActionAsync(async () =>
            {
                var result = await safeSyncService.ApplyConflictMergePolicyAsync(conflictId, policy, explicitConfirmation, cancellationToken);
                return result.SyncResult;
            });
        }

        public async Task<SafeSyncResult> ClearResolvedConflictAuditHistoryAsync(bool explicitConfirmation, CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            var result = SetSafeSyncResult(await safeSyncService.ClearResolvedConflictAuditHistoryAsync(explicitConfirmation, cancellationToken));
            ConflictAuditSummary = await safeSyncService.GetConflictAuditHistoryAsync(cancellationToken);
            return result;
        }

        public SafeSyncConfirmationRequest BeginClearResolvedConflictAuditHistoryConfirmation()
        {
            return SetPendingConfirmation(
                SafeSyncConfirmationRequestFactory.ForClearResolvedAuditHistory(ConflictAuditSummary),
                token => ClearResolvedConflictAuditHistoryAsync(true, token));
        }

        public SafeSyncConfirmationResult CancelSafeSyncConfirmation()
        {
            var request = PendingSafeSyncConfirmation;
            var result = SafeSyncConfirmationRequestFactory.Cancel(PendingSafeSyncConfirmation);
            PendingSafeSyncConfirmation = null;
            pendingSafeSyncConfirmationAction = null;
            SafeSyncConfirmationTypedPhrase = string.Empty;
            SafeSyncStatus = SafeSyncStatus.Ready;
            SafeSyncErrorCode = string.Empty;
            SafeSyncMessage = request?.CancelResultMessage ?? "Safe Sync action canceled. No changes were applied.";
            return result;
        }

        public async Task<SafeSyncResult> ConfirmSafeSyncConfirmationAsync(string typedPhrase = "", CancellationToken cancellationToken = default)
        {
            var request = PendingSafeSyncConfirmation;
            var action = pendingSafeSyncConfirmationAction;
            if (request == null || action == null)
            {
                var stale = SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.Ready, SafeSyncApiError.MergePolicyConfirmationRequired, "Safe Sync state changed. Review the latest safe summary before trying again."));
                SafeSyncMessage = request?.StaleStateMessage ?? "Safe Sync state changed. Review the latest safe summary before trying again.";
                return stale;
            }

            var result = SafeSyncConfirmationRequestFactory.Confirm(request, string.IsNullOrWhiteSpace(typedPhrase) ? SafeSyncConfirmationTypedPhrase : typedPhrase);
            if (!result.Confirmed)
            {
                var failed = SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.Ready, SafeSyncApiError.MergePolicyConfirmationRequired, request.ValidationErrorMessage));
                SafeSyncMessage = request.ValidationErrorMessage;
                return failed;
            }

            PendingSafeSyncConfirmation = null;
            pendingSafeSyncConfirmationAction = null;
            SafeSyncConfirmationTypedPhrase = string.Empty;
            var syncResult = await action(cancellationToken);
            if (syncResult != null)
            {
                SafeSyncMessage = syncResult.IsSuccess ? request.SuccessResultMessage : request.FailureResultMessage;
            }

            return syncResult;
        }

        public async Task<SafeSyncResult> CancelRetryEntryAsync(string queueEntryId, CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            return SetSafeSyncResult(await safeSyncService.CancelRetryEntryAsync(queueEntryId, cancellationToken));
        }

        public async Task<SafeSyncResult> CancelAllFailedRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            return SetSafeSyncResult(await safeSyncService.CancelAllFailedRetryEntriesAsync(cancellationToken));
        }

        public async Task<SafeSyncResult> ClearSucceededRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            return SetSafeSyncResult(await safeSyncService.ClearSucceededRetryEntriesAsync(cancellationToken));
        }

        public async Task<SafeSyncResult> PauseAllPendingRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            return SetSafeSyncResult(await safeSyncService.PauseAllPendingRetryEntriesAsync(cancellationToken));
        }

        public async Task<SafeSyncResult> ResumeAllPausedRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            return SetSafeSyncResult(await safeSyncService.ResumeAllPausedRetryEntriesAsync(cancellationToken));
        }

        public async Task RefreshSafeSyncLocalStateAsync(CancellationToken cancellationToken = default)
        {
            if (safeSyncService == null)
            {
                RetryQueueSummary = new SafeSyncRetryQueueSummary();
                ConflictSummary = new SafeSyncConflictSummary();
                ConflictAuditSummary = new SafeConflictAuditSummary();
                TombstoneSummary = new SafeSyncTombstoneSummary();
                return;
            }

            RetryQueueSummary = await safeSyncService.GetRetryQueueSummaryAsync(cancellationToken);
            ConflictSummary = await safeSyncService.GetConflictSummaryAsync(cancellationToken);
            ConflictAuditSummary = await safeSyncService.GetConflictAuditHistoryAsync(cancellationToken);
            TombstoneSummary = await safeSyncService.GetTombstoneSummaryAsync(cancellationToken);
        }

        private async Task<SafeSyncResult> RunConflictActionAsync(Func<Task<SafeSyncResult>> action)
        {
            if (safeSyncService == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SYNC_NOT_CONFIGURED", "Safe Sync is not configured."));
            }

            if (Interlocked.Exchange(ref conflictInProgress, 1) == 1)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.ConflictDetected, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress)));
            }

            try
            {
                SafeSyncStatus = SafeSyncStatus.ConflictDetected;
                await Task.Yield();
                return SetSafeSyncResult(await action());
            }
            finally
            {
                Interlocked.Exchange(ref conflictInProgress, 0);
            }
        }

        private SafeSyncConfirmationRequest BeginConflictConfirmation(string conflictId, SafeSyncConfirmationActionType actionType, Func<CancellationToken, Task<SafeSyncResult>> action)
        {
            var conflict = (ConflictSummary.SafeConflicts ?? new List<SafeSyncConflict>())
                .FirstOrDefault(item => string.Equals(item.ConflictId, conflictId, StringComparison.Ordinal));
            return SetPendingConfirmation(SafeSyncConfirmationRequestFactory.ForConflictAction(actionType, conflict), action);
        }

        private SafeSyncConfirmationRequest SetPendingConfirmation(SafeSyncConfirmationRequest request, Func<CancellationToken, Task<SafeSyncResult>> action)
        {
            if (request == null || !SafeSyncConfirmationRequestFactory.ContainsOnlySafeText(request))
            {
                SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.Ready, SafeSyncApiError.PrivacyGuardBlockedPayload, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.PrivacyGuardBlockedPayload)));
                return null;
            }

            PendingSafeSyncConfirmation = request;
            pendingSafeSyncConfirmationAction = action;
            SafeSyncConfirmationTypedPhrase = string.Empty;
            SafeSyncStatus = SafeSyncStatus.Ready;
            SafeSyncErrorCode = string.Empty;
            SafeSyncMessage = "Review the confirmation before applying this Safe Sync action.";
            return request;
        }

        private AuthResult SetAuthResult(AuthResult result)
        {
            result = result ?? AuthResult.Failure(AuthState.Failed, AuthApiError.UnknownAuthError, AuthApiError.ToSafeMessage(AuthApiError.UnknownAuthError));
            AuthErrorCode = result.ErrorCode ?? string.Empty;
            AuthMessage = string.IsNullOrWhiteSpace(AuthErrorCode)
                ? SafeUserMessageMapper.FromAuth(AuthState).Message
                : SafeUserMessageMapper.FromAuthError(AuthErrorCode);
            HasExplicitAuthMessage = true;
            return result;
        }

        private SafeSyncResult SetSafeSyncResult(SafeSyncResult result)
        {
            result = result ?? SafeSyncResult.Failure(SafeSyncStatus.Failed, "SYNC_EMPTY_RESULT", "Safe Sync returned no result.");
            SafeSyncStatus = result.Status;
            SafeSyncErrorCode = result.ErrorCode ?? string.Empty;
            SafeSyncMessage = SafeUserMessageMapper.FromSyncResult(result);
            LastSyncAcceptedCount = result.AcceptedCount;
            LastSyncRejectedCount = result.RejectedCount;
            if (result.RemoteSessions != null)
            {
                RemoteSafeSessions = result.RemoteSessions
                    .Where(summary => privacySanitizer.ValidateNoForbiddenFields(summary).IsSuccess)
                    .ToList();
            }

            if (result.RetryQueueSummary != null)
            {
                RetryQueueSummary = result.RetryQueueSummary;
            }

            if (result.ConflictSummary != null)
            {
                ConflictSummary = result.ConflictSummary;
            }

            if (result.TombstoneSummary != null)
            {
                TombstoneSummary = result.TombstoneSummary;
            }

            return result;
        }

        private SafeSyncConnectionViewModel BuildSafeSyncConnection()
        {
            var pendingCount = (RetryQueueSummary?.PendingCount ?? 0) + (TombstoneSummary?.PendingDeleteCount ?? 0);
            var state = SafeSyncConnectionState.LocalOnly;
            var label = "Local only";

            if (pendingCount > 0 || SafeSyncStatus == SafeSyncStatus.RetryPending)
            {
                state = SafeSyncConnectionState.SyncPending;
                label = "Sync pending";
            }
            else if (SafeSyncStatus == SafeSyncStatus.ServerUnavailable || SafeSyncStatus == SafeSyncStatus.Failed)
            {
                state = SafeSyncConnectionState.ServerUnavailable;
                label = "Server unavailable";
            }
            else if (AuthState == AuthState.LoggedIn ||
                     SafeSyncStatus == SafeSyncStatus.Ready ||
                     SafeSyncStatus == SafeSyncStatus.ServerReady ||
                     SafeSyncStatus == SafeSyncStatus.Synced)
            {
                state = SafeSyncConnectionState.Connected;
                label = "Connected";
            }
            else if (!HasSafeSyncService)
            {
                label = "Local only";
            }

            return new SafeSyncConnectionViewModel
            {
                State = state,
                StatusLabel = label,
                LocalGameplayAvailable = true,
                LastSyncResult = string.IsNullOrWhiteSpace(SafeSyncMessage)
                    ? "No sync attempted."
                    : SafeSyncMessage + " Accepted " + LastSyncAcceptedCount + ", rejected " + LastSyncRejectedCount + "."
            };
        }

        private async Task<ApprovedLocationEntry> FindApprovedLocationAsync(string localId, ApprovedLocationSourceType sourceType, CancellationToken cancellationToken)
        {
            var settings = await approvedLocationRepository.LoadAsync(cancellationToken);
            return (settings.Locations ?? new List<ApprovedLocationEntry>())
                .FirstOrDefault(item => item.LocalId == localId && item.Enabled && item.SourceType == sourceType);
        }

        private static ApprovedLocationDisplayItem ToDisplayItem(ApprovedLocationEntry entry)
        {
            entry = entry ?? new ApprovedLocationEntry();
            return new ApprovedLocationDisplayItem
            {
                LocalId = entry.LocalId,
                DisplayAlias = entry.DisplayAlias,
                SourceType = entry.SourceType,
                Enabled = entry.Enabled,
                UpdatedAt = entry.UpdatedAt
            };
        }

        private static RecentSafeSessionSummary ToRecentSummary(AgentWorkSession session, Dictionary<string, CharacterGrowthResult> growthBySessionId)
        {
            session = session ?? new AgentWorkSession();
            var gitSummary = session.GitChangeSummary ?? GitChangeSummary.Empty();
            var agentSummary = session.AgentActivitySummary ?? AgentActivitySummary.Empty();
            growthBySessionId = growthBySessionId ?? new Dictionary<string, CharacterGrowthResult>(StringComparer.Ordinal);
            growthBySessionId.TryGetValue(session.SessionId ?? string.Empty, out var growth);

            return new RecentSafeSessionSummary
            {
                ClientSessionId = session.SessionId ?? string.Empty,
                SourceProvider = string.IsNullOrWhiteSpace(session.SourceProvider) ? "UNKNOWN" : session.SourceProvider,
                DayBucket = !string.IsNullOrWhiteSpace(agentSummary.DayBucket)
                    ? agentSummary.DayBucket
                    : (!string.IsNullOrWhiteSpace(gitSummary.AnalysisTimeBucket) ? gitSummary.AnalysisTimeBucket : session.EndedAt.UtcDateTime.ToString("yyyy-MM-dd")),
                WorkType = session.WorkType,
                Confidence = session.Confidence,
                GitChangeCountBucket = gitSummary.ChangedFileCountBucket,
                GitAddedLineBucket = gitSummary.AddedLineBucket,
                GitDeletedLineBucket = gitSummary.DeletedLineBucket,
                AgentProviderType = agentSummary.ProviderType,
                AgentSessionCountBucket = agentSummary.SessionCountBucket,
                AgentInteractionCountBucket = agentSummary.InteractionCountBucket,
                WarningIds = (session.Warnings ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal).Take(8).ToList(),
                ExpGained = Math.Max(0, growth?.ExpGained ?? 0),
                TopStatCategory = TopStatCategory(growth?.StatDeltas)
            };
        }

        private void RefreshCharacterDashboard(SaveData saveData, List<RecentSafeSessionSummary> summaries)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            RepositoryCompanionProfileService.Normalize(saveData);
            CurrentSaveData = saveData;
            var profile = saveData.CharacterProfile ?? new CharacterProfile();
            var selectedRepositoryProfile = RepositoryCompanionProfileService.GetSelectedProfile(saveData);
            var hasConnectedRepository = selectedRepositoryProfile != null &&
                                         RepositoryCompanionProfileService.IsConnectedRepository(saveData, selectedRepositoryProfile.RepositoryHash);
            var companion = CompanionProgressionRules.Normalize(selectedRepositoryProfile?.CompanionState ?? saveData.CompanionState);
            var desktopSettings = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);
            var stats = profile.Stats ?? CharacterStats.Zero();
            var companionStats = companion.Stats ?? CompanionStatProfile.Empty();
            var totalExp = hasConnectedRepository ? Math.Max(0, companion.TotalLifetimeXp) : 0;
            var selectedSessionIds = (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Where(session => !string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash) &&
                                  string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), saveData.SelectedRepositoryHash, StringComparison.Ordinal))
                .Select(session => session.SessionId)
                .ToList();
            var latestGrowth = (saveData.GrowthHistory ?? new List<CharacterGrowthResult>())
                .Where(growth => selectedSessionIds.Contains(growth.SessionId))
                .LastOrDefault();
            var hasSavedRun = selectedSessionIds.Count > 0;
            var selectedLatestSummary = (summaries ?? new List<RecentSafeSessionSummary>())
                .FirstOrDefault(summary => selectedSessionIds.Contains(summary.ClientSessionId));
            var latestSession = hasSavedRun
                ? BootstrapUiTextFormatter.SafeLocalSessionLabel(selectedLatestSummary ?? new RecentSafeSessionSummary())
                : "No saved growth yet. Run Analysis on a repository or AI agent log to generate your first XP.";
            var growthSummary = latestGrowth == null
                ? "No growth recorded yet."
                : "+" + latestGrowth.ExpGained + " XP | Level " + latestGrowth.LevelBefore + " -> " + latestGrowth.LevelAfter;
            RepositoryCompanions = ToRepositoryCompanionDisplayItems(saveData);
            var previewOnlyCount = (saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
                .Count(candidate => candidate != null && !RepositoryCompanions.Any(item => string.Equals(item.RepositoryHash, candidate.RepositoryHash, StringComparison.Ordinal)));
            if (!hasConnectedRepository)
            {
                Debug.Log("INFO [DashboardEmptyState] reason=no_connected_repository");
            }
            Debug.Log("INFO [RepositoryProjection] connectedCount=" + RepositoryCompanions.Count +
                      " activeRepositoryId=" + (hasConnectedRepository ? saveData.SelectedRepositoryHash : string.Empty) +
                      " visibleCompanionCount=" + RepositoryCompanions.Count +
                      " previewOnlyCount=" + previewOnlyCount);
            Debug.Log("INFO [RepositoryProjection][CONSISTENT] dashboard=" + (!string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash)) +
                      " repositoriesTab=" + RepositoryCompanions.Count +
                      " sidebar=" + RepositoryCompanions.Count);
            var selectedRepositoryHash = hasConnectedRepository ? saveData.SelectedRepositoryHash : string.Empty;
            var tokenShop = selectedRepositoryProfile?.TokenShop ?? new TokenShopState();
            var bias = CompanionEvolutionPathResolver.Resolve(companion.Stats, companion.Stage);
            var motionState = hasConnectedRepository
                ? BuildMotionStateForRepository(saveData, selectedRepositoryHash, companion)
                : CompanionMotionState.Idle(string.Empty);
            AgentFlow.SetSelectedRepositoryHash(selectedRepositoryHash);
            PendingNativeActivityReview = saveData.PendingNativeActivityReview;
            RecentNativeAnalysisRuns = (saveData.RecentNativeAnalysisRuns ?? new List<NativeAnalysisRunRecord>())
                .OrderByDescending(run => run.CreatedAtUtc)
                .Take(20)
                .ToList();

            CharacterDashboard = new CharacterDashboardSummary
            {
                CharacterName = string.IsNullOrWhiteSpace(profile.DisplayName) ? "Token" : profile.DisplayName,
                Level = Math.Max(1, companion.Level),
                TotalExp = totalExp,
                CurrentLevelExp = hasConnectedRepository ? Math.Max(0, companion.CurrentXp) : 0,
                ExpForNextLevel = Math.Max(1, companion.XpRequiredForNextLevel),
                RankTitle = RankFor(Math.Max(1, companion.Level), profile.CurrentEvolutionType),
                CurrentRepositoryHash = selectedRepositoryHash,
                CurrentRepositoryAlias = hasConnectedRepository ? selectedRepositoryProfile?.SafeRepositoryAlias ?? string.Empty : string.Empty,
                Code = hasConnectedRepository ? Math.Max(0, companionStats.CodeStat > 0 ? companionStats.CodeStat : stats.Logic + stats.Architecture + stats.Velocity) : 0,
                Focus = hasConnectedRepository ? Math.Max(0, companionStats.FocusStat > 0 ? companionStats.FocusStat : stats.Efficiency + stats.Stability) : 0,
                Debug = hasConnectedRepository ? Math.Max(0, companionStats.DebugStat > 0 ? companionStats.DebugStat : stats.Debug) : 0,
                Design = hasConnectedRepository ? Math.Max(0, companionStats.DesignStat > 0 ? companionStats.DesignStat : stats.Design + stats.Creativity) : 0,
                Sync = hasConnectedRepository ? Math.Max(0, companionStats.SyncStat + (saveData.SyncState?.PendingQueueCount ?? 0) + (RemoteSafeSessions?.Count ?? 0)) : 0,
                WeeklyCode = hasConnectedRepository ? Math.Max(0, companion.WeeklyStats?.CodeStat ?? 0) : 0,
                WeeklyFocus = hasConnectedRepository ? Math.Max(0, companion.WeeklyStats?.FocusStat ?? 0) : 0,
                WeeklyDebug = hasConnectedRepository ? Math.Max(0, companion.WeeklyStats?.DebugStat ?? 0) : 0,
                WeeklyDesign = hasConnectedRepository ? Math.Max(0, companion.WeeklyStats?.DesignStat ?? 0) : 0,
                WeeklySync = hasConnectedRepository ? Math.Max(0, companion.WeeklyStats?.SyncStat ?? 0) : 0,
                DominantGrowthPath = hasConnectedRepository ? bias.MainPath : "Unknown",
                SecondaryGrowthTrait = hasConnectedRepository ? bias.SecondaryTrait : "Unknown",
                CurrentEvolutionBias = hasConnectedRepository ? bias.CurrentBias : "Unknown",
                NextEvolutionPreview = hasConnectedRepository ? bias.NextEvolutionPreview : "Repository Hatchling",
                EggInfluenceText = hasConnectedRepository ? bias.EggInfluenceText : "Connect a repository to start shaping a companion.",
                TokenCurrencyName = tokenShop.CurrencyName,
                TokenCurrencyBalance = hasConnectedRepository ? Math.Max(0, tokenShop.CurrencyBalance) : 0,
                TokenUsageTrackingEnabled = tokenShop.TrackLocalAiTokenUsage,
                HasSavedRun = hasConnectedRepository && hasSavedRun,
                CompanionState = hasConnectedRepository ? companion : CompanionState.CreateDefault(),
                DesktopCompanionSettings = desktopSettings,
                DesktopOverlayState = hasConnectedRepository && desktopSettings.IsDesktopCompanionEnabled ? CompanionDesktopOverlayState.Fallback : CompanionDesktopOverlayState.Disabled,
                MotionState = motionState,
                LatestSafeSessionSummary = hasConnectedRepository ? latestSession : "No repository is connected yet.",
                RecentGrowthSummary = hasConnectedRepository ? growthSummary : "Connect a repository to start Git growth.",
                QuestSummary = BuildQuestSummary(hasSavedRun),
                ActivityLogSummary = hasSavedRun
                    ? string.Join("\n", (summaries ?? new List<RecentSafeSessionSummary>())
                        .Where(summary => selectedSessionIds.Contains(summary.ClientSessionId))
                        .Select(BootstrapUiTextFormatter.SafeLocalSessionLabel))
                    : "No run saved yet.\nConnect a Git repository, review the safe aggregate, then save Git growth.",
                RepositoryCompanions = RepositoryCompanions
            };
        }

        private static List<RepositoryCompanionDisplayItem> ToRepositoryCompanionDisplayItems(SaveData saveData)
        {
            saveData = RepositoryCompanionProfileService.Normalize(saveData);
            var connectedProjects = (saveData.ConnectedProjects ?? new List<ConnectedProject>())
                .Where(project => project != null &&
                                  !project.IsArchived &&
                                  project.ApprovedAt != null &&
                                  !RepositoryCompanionProfileService.IsStaleFallbackProject(project))
                .GroupBy(project => string.IsNullOrWhiteSpace(project.Id) ? project.PathHash : project.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            return (saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
                .Where(profile => profile != null &&
                                  profile.ArchivedAtUtc == null &&
                                  connectedProjects.ContainsKey(profile.RepositoryHash))
                .Select(profile =>
                {
                    var companion = CompanionProgressionRules.Normalize(profile.CompanionState);
                    var motionState = BuildMotionStateForRepository(saveData, profile.RepositoryHash, companion);
                    var connection = connectedProjects[profile.RepositoryHash];
                    return new RepositoryCompanionDisplayItem
                    {
                        RepositoryHash = profile.RepositoryHash,
                        SafeRepositoryAlias = string.IsNullOrWhiteSpace(connection.DisplayName) ? profile.SafeRepositoryAlias : connection.DisplayName,
                        Stage = companion.Stage,
                        Archetype = companion.Archetype,
                        Level = companion.Level,
                        TotalXp = companion.TotalXp,
                        LifetimeGrowthXp = companion.TotalLifetimeXp,
                        WeeklyGrowthXp = WeeklyXpForRepository(saveData, profile.RepositoryHash),
                        CurrentXp = companion.CurrentXp,
                        XpRequiredForNextLevel = companion.XpRequiredForNextLevel,
                        CanLevelUp = companion.CanLevelUp,
                        RecentGrowthSource = RecentGrowthSourceForRepository(saveData, profile.RepositoryHash),
                        RecentGitXp = RecentXpForRepository(saveData, profile.RepositoryHash, gitOnly: true),
                        RecentAiXp = RecentXpForRepository(saveData, profile.RepositoryHash, gitOnly: false),
                        EstimatedTokenActivity = EstimatedTokenActivityForRepository(saveData, profile.RepositoryHash),
                        DominantStat = companion.EvolutionBias?.DominantStat.ToString() ?? "Unknown",
                        SecondaryStat = companion.EvolutionBias?.SecondaryStat.ToString() ?? "Unknown",
                        EvolutionPath = companion.EvolutionBias?.CurrentBias ?? "Unknown",
                        NextEvolutionPreview = companion.EvolutionBias?.NextEvolutionPreview ?? "Repository Hatchling",
                        TokenCurrencyName = profile.TokenShop?.CurrencyName ?? "Forge Coins",
                        TokenCurrencyBalance = Math.Max(0, profile.TokenShop?.CurrencyBalance ?? 0),
                        PurchasedTokenShopItemIds = new List<string>(profile.TokenShop?.PurchasedItemIds ?? new List<string>()),
                        Skin = CompanionSkinCatalog.Normalize(profile.DesktopCompanionSettings?.VisualThemeId),
                        CompanionId = profile.CompanionId,
                        DesktopCompanionEnabled = profile.DesktopCompanionSettings?.IsDesktopCompanionEnabled ?? true,
                        HasSavedOverlayPosition = profile.DesktopCompanionSettings?.HasSavedOverlayPosition ?? false,
                        OverlayPositionX = profile.DesktopCompanionSettings?.LastOverlayPositionX ?? -1f,
                        OverlayPositionY = profile.DesktopCompanionSettings?.LastOverlayPositionY ?? -1f,
                        MotionState = motionState,
                        ApprovedByUser = !connection.IsArchived && connection.ApprovedAt != null,
                        ApprovedAtUtc = connection.ApprovedAt,
                        LastApprovedActivityBucket = profile.LastApprovedActivityBucket,
                        Selected = string.Equals(profile.RepositoryHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal),
                        Archived = profile.ArchivedAtUtc != null || connection.IsArchived
                    };
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.RepositoryHash))
                .Where(item => !string.Equals(item.SafeRepositoryAlias, "Local Repository", StringComparison.OrdinalIgnoreCase))
                .Where(item => item.ApprovedByUser && !item.Archived)
                .ToList();
        }

        private static int RecentXpForRepository(SaveData saveData, string repositoryHash, bool gitOnly)
        {
            repositoryHash = repositoryHash ?? string.Empty;
            var sessions = (saveData?.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Where(session => session != null &&
                                  string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), repositoryHash, StringComparison.Ordinal))
                .OrderByDescending(session => session.EndedAt)
                .Take(8)
                .ToList();
            var sessionIds = sessions
                .Where(session => gitOnly
                    ? string.Equals(session.SourceProvider, "GIT", StringComparison.OrdinalIgnoreCase) || session.GitChangeSummary != null
                    : session.AgentActivitySummary != null || (!string.IsNullOrWhiteSpace(session.SourceProvider) && !string.Equals(session.SourceProvider, "GIT", StringComparison.OrdinalIgnoreCase)))
                .Select(session => session.SessionId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);

            return (saveData?.GrowthHistory ?? new List<CharacterGrowthResult>())
                .Where(growth => growth != null && sessionIds.Contains(growth.SessionId))
                .Sum(growth => Math.Max(0, growth.ExpGained));
        }

        private static int WeeklyXpForRepository(SaveData saveData, string repositoryHash)
        {
            repositoryHash = repositoryHash ?? string.Empty;
            var since = DateTimeOffset.UtcNow.AddDays(-7);
            var sessionIds = (saveData?.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Where(session => session != null &&
                                  session.EndedAt >= since &&
                                  string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), repositoryHash, StringComparison.Ordinal))
                .Select(session => session.SessionId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            return (saveData?.GrowthHistory ?? new List<CharacterGrowthResult>())
                .Where(growth => growth != null && sessionIds.Contains(growth.SessionId))
                .Sum(growth => Math.Max(0, growth.ExpGained));
        }

        private static TokenUsageBucket EstimatedTokenActivityForRepository(SaveData saveData, string repositoryHash)
        {
            repositoryHash = repositoryHash ?? string.Empty;
            return (saveData?.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Where(session => session != null &&
                                  string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), repositoryHash, StringComparison.Ordinal))
                .Select(session => session.TokenUsageBucket)
                .OrderByDescending(bucket => (int)bucket)
                .FirstOrDefault();
        }

        private static CompanionMotionState BuildMotionStateForRepository(SaveData saveData, string repositoryHash, CompanionState companion)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            companion = CompanionProgressionRules.Normalize(companion);
            repositoryHash = repositoryHash ?? string.Empty;
            var sessions = (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Where(session => session != null &&
                                  (string.IsNullOrWhiteSpace(repositoryHash) ||
                                   string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), repositoryHash, StringComparison.Ordinal)))
                .OrderByDescending(session => session.EndedAt)
                .Take(8)
                .ToList();
            var sessionIds = sessions.Select(session => session.SessionId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            var growth = (saveData.GrowthHistory ?? new List<CharacterGrowthResult>())
                .Where(item => item != null && sessionIds.Contains(item.SessionId))
                .ToList();
            var git = sessions.FirstOrDefault(session => session.GitChangeSummary != null)?.GitChangeSummary;
            var agent = sessions.FirstOrDefault(session => session.AgentActivitySummary != null)?.AgentActivitySummary;
            var pending = saveData.PendingNativeActivityReview != null &&
                          (string.IsNullOrWhiteSpace(saveData.PendingNativeActivityReview.RepositoryHash) ||
                           string.IsNullOrWhiteSpace(repositoryHash) ||
                           string.Equals(saveData.PendingNativeActivityReview.RepositoryHash, repositoryHash, StringComparison.Ordinal));
            var latestRun = (saveData.RecentNativeAnalysisRuns ?? new List<NativeAnalysisRunRecord>()).FirstOrDefault();
            var forced = CompanionMotionReaction.None;
            if (latestRun != null && latestRun.CreatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-10))
            {
                if (string.Equals(latestRun.SourceKind, "reviewSaved", StringComparison.OrdinalIgnoreCase))
                {
                    forced = CompanionMotionReaction.GrowthSaved;
                }
                else if (string.Equals(latestRun.SourceKind, "levelUp", StringComparison.OrdinalIgnoreCase))
                {
                    forced = CompanionMotionReaction.LevelUp;
                }
            }

            return CompanionMotionStateResolver.Resolve(new CompanionMotionSignal
            {
                RepositoryId = repositoryHash,
                RecentGitChangedFiles = git?.ChangedFileCountBucket ?? CountBucket.Unknown,
                CommitCount = git?.CommitCountBucket ?? CountBucket.Unknown,
                AddedLines = git?.AddedLineBucket ?? LineChangeBucket.Unknown,
                DeletedLines = git?.DeletedLineBucket ?? LineChangeBucket.Unknown,
                RecentRepositoryXp = growth.Where(item => sessions.Any(session => string.Equals(session.SessionId, item.SessionId, StringComparison.Ordinal) &&
                                                                                  string.Equals(session.SourceProvider, "GIT", StringComparison.OrdinalIgnoreCase)))
                    .Sum(item => Math.Max(0, item.ExpGained)),
                AiAgentSessionCount = agent?.SessionCountBucket ?? CountBucket.Unknown,
                AiAgentInteractionCount = agent?.InteractionCountBucket ?? CountBucket.Unknown,
                EstimatedTokenActivity = sessions.Select(session => session.TokenUsageBucket).OrderByDescending(bucket => (int)bucket).FirstOrDefault(),
                AiAgentXp = growth.Where(item => sessions.Any(session => string.Equals(session.SessionId, item.SessionId, StringComparison.Ordinal) &&
                                                                          !string.Equals(session.SourceProvider, "GIT", StringComparison.OrdinalIgnoreCase)))
                    .Sum(item => Math.Max(0, item.ExpGained)),
                HasPendingReview = pending,
                CanLevelUp = companion.CanLevelUp,
                HasWarnings = (saveData.PendingNativeActivityReview?.WarningIds?.Count ?? 0) > 0,
                ForcedReaction = forced
            });
        }

        private static string RecentGrowthSourceForRepository(SaveData saveData, string repositoryHash)
        {
            var sessions = (saveData?.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Where(session => session != null &&
                                  string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), repositoryHash ?? string.Empty, StringComparison.Ordinal))
                .OrderByDescending(session => session.EndedAt)
                .Take(6)
                .ToList();
            var hasGit = sessions.Any(session => string.Equals(session.SourceProvider, "GIT", StringComparison.OrdinalIgnoreCase) || session.GitChangeSummary != null);
            var hasAgent = sessions.Any(session => session.AgentActivitySummary != null || (!string.IsNullOrWhiteSpace(session.SourceProvider) && !string.Equals(session.SourceProvider, "GIT", StringComparison.OrdinalIgnoreCase)));
            if (hasGit && hasAgent) return "Mixed";
            if (hasAgent) return "AI Agent";
            if (hasGit) return "Git";
            return "None";
        }

        private static void ApplyRepositoryAnalysisCheckpoint(SaveData saveData, string repositoryHash, GitChangeSummary summary)
        {
            if (saveData == null || string.IsNullOrWhiteSpace(repositoryHash) || summary == null)
            {
                return;
            }

            var connection = (saveData.ConnectedProjects ?? new List<ConnectedProject>())
                .FirstOrDefault(project => project != null &&
                                           !project.IsArchived &&
                                           (string.Equals(project.Id, repositoryHash, StringComparison.Ordinal) ||
                                            string.Equals(project.PathHash, repositoryHash, StringComparison.Ordinal) ||
                                            string.Equals(project.ProjectPathHash, repositoryHash, StringComparison.Ordinal)));
            if (connection == null)
            {
                return;
            }

            connection.LastAnalyzedAt = DateTimeOffset.UtcNow;
            connection.LastAnalyzedCommit = summary.LastAnalyzedCommit ?? string.Empty;
            connection.FirstCommitAt = summary.FirstCommitAtUtc ?? string.Empty;
            connection.TotalCommitCount = Math.Max(0, summary.TotalCommitsAnalyzed);
            connection.AnalyzedCommitRange = (summary.AnalyzedStartCommit ?? string.Empty) + ".." + (summary.AnalyzedEndCommit ?? string.Empty);
            connection.LastAnalysisMode = summary.AnalysisMode ?? string.Empty;
        }

        private static string TopStatCategory(CharacterStats stats)
        {
            if (stats == null)
            {
                return string.Empty;
            }

            var pairs = new[]
            {
                new { Name = "Code", Value = stats.Logic + stats.Architecture + stats.Velocity },
                new { Name = "Focus", Value = stats.Efficiency + stats.Stability },
                new { Name = "Debug", Value = stats.Debug },
                new { Name = "Design", Value = stats.Design + stats.Creativity },
                new { Name = "Stress", Value = stats.Stress }
            };
            var top = pairs.OrderByDescending(item => item.Value).FirstOrDefault();
            return top != null && top.Value > 0 ? top.Name : string.Empty;
        }

        private string BuildQuestSummary(bool hasSavedRun)
        {
            return "Analyze repository: open | Save session: "
                   + (GitFlow.HasPendingReview || AgentFlow.HasPendingReview ? "ready" : "needs review")
                   + " | Sync progress: "
                   + (CanUseAuthenticatedSafeSync ? "ready" : "login required")
                   + " | History: "
                   + (hasSavedRun ? "available" : "empty");
        }

        private static string RankFor(int level, EvolutionType evolutionType)
        {
            if (level >= 20)
            {
                return "Forge Architect";
            }

            if (level >= 10)
            {
                return "Senior Smith";
            }

            if (evolutionType != EvolutionType.Unknown)
            {
                return evolutionType + " Adept";
            }

            return level >= 5 ? "Code Smith" : "Local Apprentice";
        }

        private static string SafeLocalAlias(string displayAlias, string fallback)
        {
            return string.IsNullOrWhiteSpace(displayAlias) ? fallback : displayAlias.Trim();
        }

        private static AgentProviderType ToAgentProviderType(ConnectedAgentSourceType sourceType)
        {
            switch (sourceType)
            {
                case ConnectedAgentSourceType.Cursor: return AgentProviderType.Cursor;
                case ConnectedAgentSourceType.ClaudeCode: return AgentProviderType.ClaudeCode;
                case ConnectedAgentSourceType.Codex: return AgentProviderType.Codex;
                case ConnectedAgentSourceType.GitHubCopilot: return AgentProviderType.GitHubCopilot;
                case ConnectedAgentSourceType.GeminiCli: return AgentProviderType.GeminiCli;
                case ConnectedAgentSourceType.OtherManualLogFolder: return AgentProviderType.Manual;
                default: return AgentProviderType.Unknown;
            }
        }

        private static ConnectedAgentSourceType ToConnectedAgentSourceType(AgentProviderType providerType)
        {
            switch (MacAgentSourceDetector.NormalizeProvider(providerType))
            {
                case AgentProviderType.Cursor: return ConnectedAgentSourceType.Cursor;
                case AgentProviderType.ClaudeCode: return ConnectedAgentSourceType.ClaudeCode;
                case AgentProviderType.Codex: return ConnectedAgentSourceType.Codex;
                case AgentProviderType.GitHubCopilot: return ConnectedAgentSourceType.GitHubCopilot;
                case AgentProviderType.GeminiCli: return ConnectedAgentSourceType.GeminiCli;
                case AgentProviderType.Manual: return ConnectedAgentSourceType.OtherManualLogFolder;
                default: return ConnectedAgentSourceType.OtherManualLogFolder;
            }
        }

        private static AgentSourceSetupState ParseSourceState(string value, bool selected)
        {
            if (!selected)
            {
                return AgentSourceSetupState.NotSelected;
            }

            return Enum.TryParse(value, true, out AgentSourceSetupState parsed)
                ? parsed
                : AgentSourceSetupState.Selected;
        }

        private static string StatusLabelForRestoredState(AgentSourceSetupState state)
        {
            switch (state)
            {
                case AgentSourceSetupState.ReadyToAnalyze:
                    return "Ready to analyze";
                case AgentSourceSetupState.LocalSourceDetected:
                    return "Detected locally. Choose or verify a folder before analysis.";
                case AgentSourceSetupState.AnalysisComplete:
                    return "Analysis complete";
                case AgentSourceSetupState.PermissionRequired:
                case AgentSourceSetupState.ManualImportRequired:
                    return "Manual folder required";
                case AgentSourceSetupState.AnalysisFailedSafely:
                    return "Analysis failed safely";
                case AgentSourceSetupState.DetectingLocalSource:
                    return "Detecting local source";
                default:
                    return "Selected. Detect or choose folder.";
            }
        }

        private static string ParserVersionFor(AgentProviderType providerType)
        {
            switch (providerType)
            {
                case AgentProviderType.Codex: return CodexAgentLogParser.Version;
                case AgentProviderType.Cursor: return CursorAgentLogParser.Version;
                case AgentProviderType.Claude:
                case AgentProviderType.ClaudeCode: return ClaudeAgentLogParser.Version;
                case AgentProviderType.GitHubCopilot: return GitHubCopilotAgentLogParser.Version;
                case AgentProviderType.Manual:
                case AgentProviderType.GeminiCli: return ManualAgentLogParser.Version;
                default: return UnknownAgentLogParser.Version;
            }
        }
    }
}
