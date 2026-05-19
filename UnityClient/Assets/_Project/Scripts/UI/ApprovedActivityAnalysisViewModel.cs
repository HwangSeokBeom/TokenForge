using System;
using System.Collections.Generic;
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
        public string SafeRepositoryAlias { get; set; } = "Local Repository";
        public CompanionStage Stage { get; set; } = CompanionStage.Egg;
        public CompanionArchetype Archetype { get; set; } = CompanionArchetype.Unknown;
        public int Level { get; set; } = 1;
        public int TotalXp { get; set; }
        public string LastApprovedActivityBucket { get; set; } = string.Empty;
        public bool Selected { get; set; }
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
        public string CurrentRepositoryAlias { get; set; } = "Local Repository";
        public int Code { get; set; }
        public int Focus { get; set; }
        public int Debug { get; set; }
        public int Design { get; set; }
        public int Sync { get; set; }
        public bool HasSavedRun { get; set; }
        public CompanionState CompanionState { get; set; } = CompanionState.CreateDefault();
        public DesktopCompanionSettings DesktopCompanionSettings { get; set; } = DesktopCompanionSettings.CreateDefault();
        public CompanionDesktopOverlayState DesktopOverlayState { get; set; } = CompanionDesktopOverlayState.Disabled;
        public string LatestSafeSessionSummary { get; set; } = "No saved run yet. Analyze a repository or AI agent log to generate your first XP.";
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
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.Cursor, DisplayName = "Cursor" },
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.ClaudeCode, DisplayName = "Claude Code" },
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.Codex, DisplayName = "Codex" },
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.GitHubCopilot, DisplayName = "GitHub Copilot" },
            new ConnectedAgentSource { SourceType = ConnectedAgentSourceType.OtherManualLogFolder, DisplayName = "Other / Manual Log Folder", State = AgentSourceSetupState.ManualImportRequired, StatusLabel = "Manual import required" }
        };
    }

    public sealed class ApprovedActivityAnalysisViewModel
    {
        public const string SafeAggregateNotice = "Only safe aggregate data will be saved.";
        public const string RawDataNotice = "Raw paths, prompts, responses, commands, filenames, repository names, and source code are not saved or synced.";
        public const string ApprovedLocationsNotice = "Approved locations are stored only on this device and are never synced.";

        private const int RecentSessionLimit = 8;

        private readonly IAgentLogLocationPicker agentLogLocationPicker;
        private readonly ILocalSaveDataRepository repository;
        private readonly IApprovedLocationSettingsRepository approvedLocationRepository;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly ISafeSyncService safeSyncService;
        private readonly IAuthSessionService authSessionService;
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
            IAuthSessionService authSessionService = null)
        {
            GitFlow = gitFlow ?? throw new ArgumentNullException(nameof(gitFlow));
            AgentFlow = agentFlow ?? throw new ArgumentNullException(nameof(agentFlow));
            this.agentLogLocationPicker = agentLogLocationPicker ?? throw new ArgumentNullException(nameof(agentLogLocationPicker));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.approvedLocationRepository = approvedLocationRepository ?? new ApprovedLocationSettingsRepository();
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.safeSyncService = safeSyncService;
            this.authSessionService = authSessionService;
        }

        public GitAnalysisFlowController GitFlow { get; }
        public AgentAnalysisFlowController AgentFlow { get; }
        public AgentAnalysisSettings AgentSettings { get; } = new AgentAnalysisSettings();
        public OnboardingState Onboarding { get; } = new OnboardingState();
        public AgentProviderType SelectedAgentProviderType { get; set; } = AgentProviderType.Unknown;
        public string AgentSelectionStatus { get; private set; } = "No agent log location selected";
        public string AgentPickerErrorCategory { get; private set; } = string.Empty;
        public List<RecentSafeSessionSummary> RecentSessions { get; private set; } = new List<RecentSafeSessionSummary>();
        public List<ApprovedLocationDisplayItem> ApprovedGitLocations { get; private set; } = new List<ApprovedLocationDisplayItem>();
        public List<ApprovedLocationDisplayItem> ApprovedAgentLocations { get; private set; } = new List<ApprovedLocationDisplayItem>();
        public List<RepositoryCompanionDisplayItem> RepositoryCompanions { get; private set; } = new List<RepositoryCompanionDisplayItem>();
        public List<RemoteSafeSessionSummary> RemoteSafeSessions { get; private set; } = new List<RemoteSafeSessionSummary>();
        public CharacterDashboardSummary CharacterDashboard { get; private set; } = new CharacterDashboardSummary();
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
                if (sourceType == ConnectedAgentSourceType.OtherManualLogFolder)
                {
                    AgentFlow.ClearSelection();
                    AgentSelectionStatus = "No agent log location selected";
                    AgentPickerErrorCategory = string.Empty;
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
            var result = await new MacAgentSourceDetector(providerType).DetectAsync(cancellationToken);
            source.LastScanTimeUtc = result.ScannedAtUtc;
            source.WarningCount = result.WarningIds?.Count ?? 0;

            var candidate = result.BestCandidate();
            if (candidate != null && result.HasUsableCandidate)
            {
                approvedAgentCandidates[sourceType] = candidate;
                source.State = AgentSourceSetupState.ReadyToAnalyze;
                source.SafeLabel = candidate.SafeAlias;
                source.SafeLocationHash = SafeHashUtility.ComputeProjectPathHash(candidate.LocalPath, "TokenForge.AgentLogLocation.v1");
                source.Confidence = candidate.Confidence;
                source.WarningCount += candidate.WarningIds?.Count ?? 0;
                SelectedAgentProviderType = providerType;
                var selection = AgentSettings.ToInput(candidate.LocalPath, providerType);
                selection.SourceKind = candidate.SourceKind;
                selection.SafeSourceAlias = candidate.SafeAlias;
                var selectResult = AgentFlow.SelectApprovedLogLocation(selection);
                source.StatusLabel = selectResult.IsSuccess
                    ? source.DisplayName + " local source detected | Ready to analyze | " + candidate.SafeAlias + " | confidence " + candidate.Confidence
                    : "Analysis failed safely";
                if (!selectResult.IsSuccess)
                {
                    source.State = AgentSourceSetupState.AnalysisFailedSafely;
                    return selectResult;
                }

                return Result.Success();
            }

            if (result.AccessState == AgentSourceAccessState.PermissionRequired)
            {
                source.State = AgentSourceSetupState.PermissionRequired;
                source.StatusLabel = "Permission required";
                AgentPickerErrorCategory = "agent_source_permission_required";
                return Result.Failure(AgentPickerErrorCategory, "Local source requires permission.");
            }

            source.State = AgentSourceSetupState.ManualImportRequired;
            source.StatusLabel = source.DisplayName + " manual import required";
            AgentPickerErrorCategory = "agent_source_manual_import_required";
            return Result.Failure(AgentPickerErrorCategory, "Local source was not detected. Choose a folder to analyze safe aggregates.");
        }

        public async Task<Result<AgentAnalysisReviewModel>> AnalyzeAgentSourceForOnboardingAsync(ConnectedAgentSourceType sourceType, CancellationToken cancellationToken = default)
        {
            var source = Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            if (source == null)
            {
                return Result<AgentAnalysisReviewModel>.Failure("agent_source_unknown", "Agent source is unavailable.");
            }

            if (source.State != AgentSourceSetupState.ReadyToAnalyze && !AgentFlow.HasSelectedAgentLogLocationForLocalOnlyApproval)
            {
                var detect = await DetectAgentSourceForOnboardingAsync(sourceType, cancellationToken);
                if (!detect.IsSuccess && !AgentFlow.HasSelectedAgentLogLocationForLocalOnlyApproval)
                {
                    source.State = AgentSourceSetupState.AnalysisFailedSafely;
                    source.StatusLabel = "Analysis failed safely";
                    return Result<AgentAnalysisReviewModel>.Failure(detect.ErrorCode, detect.ErrorMessage);
                }
            }

            var result = await AnalyzeSelectedAgentActivityAsync(cancellationToken);
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
                }
            }

            return result;
        }

        public async Task<Result> SelectLocalGitRepositoryForOnboardingAsync(CancellationToken cancellationToken = default)
        {
            var result = await GitFlow.SelectRepositoryAsync(cancellationToken);
            if (result.IsSuccess && GitFlow.HasSelectedRepositoryForLocalOnlyApproval)
            {
                Onboarding.GitConnected = true;
                Onboarding.GitSkipped = false;
                Onboarding.GitAccountPlaceholderSelected = false;
                var saveData = await repository.LoadAsync(cancellationToken);
                RepositoryCompanionProfileService.Normalize(saveData);
                var selected = RepositoryCompanionProfileService.GetSelectedProfile(saveData);
                Onboarding.GitSafeAlias = selected?.SafeRepositoryAlias ?? "Local Repository";
                AgentFlow.SetSelectedRepositoryHash(saveData.SelectedRepositoryHash);
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

        public async Task<Result> SelectApprovedGitLocationAsync(string localId, CancellationToken cancellationToken = default)
        {
            var entry = await FindApprovedLocationAsync(localId, ApprovedLocationSourceType.Git, cancellationToken);
            if (entry == null)
            {
                return Result.Failure("approved_location_not_found", "Approved Git location was not found.");
            }

            var result = GitFlow.SelectLocalOnlyApprovedRepositoryPath(entry.LocalPath);
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
            return await AgentFlow.AnalyzeAsync(cancellationToken);
        }

        public async Task<Result<GitAnalysisReviewModel>> AnalyzeGitActivityAsync(CancellationToken cancellationToken = default)
        {
            return await GitFlow.AnalyzeAsync(cancellationToken);
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
            return await UpdateDesktopCompanionSettingsAsync(settings =>
            {
                settings.LastOverlayPositionX = Math.Max(0f, x);
                settings.LastOverlayPositionY = Math.Max(0f, y);
                settings.HasSavedOverlayPosition = true;
                settings.LastOverlayPositionXBucket = BucketForCoordinate(x);
                settings.LastOverlayPositionYBucket = BucketForCoordinate(y);
            }, cancellationToken);
        }

        public Result DiscardGitReview()
        {
            return GitFlow.DiscardPendingReview();
        }

        public Result DiscardAgentReview()
        {
            AgentSelectionStatus = "No agent log location selected";
            AgentPickerErrorCategory = string.Empty;
            return AgentFlow.DiscardPendingReview();
        }

        private async Task<Result<DesktopCompanionSettings>> UpdateDesktopCompanionSettingsAsync(Action<DesktopCompanionSettings> update, CancellationToken cancellationToken)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.DesktopCompanionSettings = saveData.DesktopCompanionSettings ?? DesktopCompanionSettings.CreateDefault();
            update?.Invoke(saveData.DesktopCompanionSettings);
            saveData.DesktopCompanionSettings.SchemaVersion = 1;
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
            return Result<DesktopCompanionSettings>.Success(saveData.DesktopCompanionSettings);
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

            return Result<List<ApprovedLocationDisplayItem>>.Success(safeDisplayItems);
        }

        public async Task RefreshDashboardAsync(CancellationToken cancellationToken = default)
        {
            if (authSessionService != null)
            {
                await LoadAuthSessionAsync(cancellationToken);
            }

            await RefreshApprovedLocationsAsync(cancellationToken);
            await RefreshRecentSessionsAsync(cancellationToken);
            await RefreshSafeSyncLocalStateAsync(cancellationToken);
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
            var profile = saveData.CharacterProfile ?? new CharacterProfile();
            var selectedRepositoryProfile = RepositoryCompanionProfileService.GetSelectedProfile(saveData);
            var companion = CompanionProgressionRules.Normalize(selectedRepositoryProfile?.CompanionState ?? saveData.CompanionState);
            var desktopSettings = saveData.DesktopCompanionSettings ?? DesktopCompanionSettings.CreateDefault();
            var stats = profile.Stats ?? CharacterStats.Zero();
            var companionStats = companion.Stats ?? CompanionStatProfile.Empty();
            var totalExp = Math.Max(0, profile.TotalExp);
            const int expPerLevel = 1000;
            var selectedSessionIds = (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Where(session => string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), saveData.SelectedRepositoryHash, StringComparison.Ordinal))
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
                : "No saved run yet. Analyze a repository or AI agent log to generate your first XP.";
            var growthSummary = latestGrowth == null
                ? "No growth recorded yet."
                : "+" + latestGrowth.ExpGained + " XP | Level " + latestGrowth.LevelBefore + " -> " + latestGrowth.LevelAfter;
            RepositoryCompanions = ToRepositoryCompanionDisplayItems(saveData);
            AgentFlow.SetSelectedRepositoryHash(saveData.SelectedRepositoryHash);

            CharacterDashboard = new CharacterDashboardSummary
            {
                CharacterName = string.IsNullOrWhiteSpace(profile.DisplayName) ? "Token" : profile.DisplayName,
                Level = Math.Max(1, profile.Level),
                TotalExp = totalExp,
                CurrentLevelExp = totalExp % expPerLevel,
                ExpForNextLevel = expPerLevel,
                RankTitle = RankFor(Math.Max(1, profile.Level), profile.CurrentEvolutionType),
                CurrentRepositoryHash = saveData.SelectedRepositoryHash,
                CurrentRepositoryAlias = selectedRepositoryProfile?.SafeRepositoryAlias ?? "Local Repository",
                Code = Math.Max(0, companionStats.CodeStat > 0 ? companionStats.CodeStat : stats.Logic + stats.Architecture + stats.Velocity),
                Focus = Math.Max(0, companionStats.FocusStat > 0 ? companionStats.FocusStat : stats.Efficiency + stats.Stability),
                Debug = Math.Max(0, companionStats.DebugStat > 0 ? companionStats.DebugStat : stats.Debug),
                Design = Math.Max(0, companionStats.DesignStat > 0 ? companionStats.DesignStat : stats.Design + stats.Creativity),
                Sync = Math.Max(0, (saveData.SyncState?.PendingQueueCount ?? 0) + (RemoteSafeSessions?.Count ?? 0)),
                HasSavedRun = hasSavedRun,
                CompanionState = companion,
                DesktopCompanionSettings = desktopSettings,
                DesktopOverlayState = desktopSettings.IsDesktopCompanionEnabled ? CompanionDesktopOverlayState.Fallback : CompanionDesktopOverlayState.Disabled,
                LatestSafeSessionSummary = latestSession,
                RecentGrowthSummary = growthSummary,
                QuestSummary = BuildQuestSummary(hasSavedRun),
                ActivityLogSummary = hasSavedRun
                    ? string.Join("\n", (summaries ?? new List<RecentSafeSessionSummary>())
                        .Where(summary => selectedSessionIds.Contains(summary.ClientSessionId))
                        .Select(BootstrapUiTextFormatter.SafeLocalSessionLabel))
                    : "No run saved yet.\nConnect a Git repository or AI Agent log, review the safe aggregate, then save it to gain XP.",
                RepositoryCompanions = RepositoryCompanions
            };
        }

        private static List<RepositoryCompanionDisplayItem> ToRepositoryCompanionDisplayItems(SaveData saveData)
        {
            saveData = RepositoryCompanionProfileService.Normalize(saveData);
            return (saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
                .Select(profile =>
                {
                    var companion = CompanionProgressionRules.Normalize(profile.CompanionState);
                    return new RepositoryCompanionDisplayItem
                    {
                        RepositoryHash = profile.RepositoryHash,
                        SafeRepositoryAlias = profile.SafeRepositoryAlias,
                        Stage = companion.Stage,
                        Archetype = companion.Archetype,
                        Level = companion.Level,
                        TotalXp = companion.TotalXp,
                        LastApprovedActivityBucket = profile.LastApprovedActivityBucket,
                        Selected = string.Equals(profile.RepositoryHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal)
                    };
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.RepositoryHash))
                .ToList();
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
                case ConnectedAgentSourceType.OtherManualLogFolder: return AgentProviderType.Manual;
                default: return AgentProviderType.Unknown;
            }
        }
    }
}
