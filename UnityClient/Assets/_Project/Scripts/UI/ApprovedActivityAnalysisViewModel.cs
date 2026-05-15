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
    }

    public sealed class ApprovedLocationDisplayItem
    {
        public string LocalId { get; set; } = string.Empty;
        public string DisplayAlias { get; set; } = string.Empty;
        public ApprovedLocationSourceType SourceType { get; set; } = ApprovedLocationSourceType.UnknownAuto;
        public bool Enabled { get; set; } = true;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
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
        public AgentProviderType SelectedAgentProviderType { get; set; } = AgentProviderType.Unknown;
        public string AgentSelectionStatus { get; private set; } = "No agent log location selected";
        public string AgentPickerErrorCategory { get; private set; } = string.Empty;
        public List<RecentSafeSessionSummary> RecentSessions { get; private set; } = new List<RecentSafeSessionSummary>();
        public List<ApprovedLocationDisplayItem> ApprovedGitLocations { get; private set; } = new List<ApprovedLocationDisplayItem>();
        public List<ApprovedLocationDisplayItem> ApprovedAgentLocations { get; private set; } = new List<ApprovedLocationDisplayItem>();
        public List<RemoteSafeSessionSummary> RemoteSafeSessions { get; private set; } = new List<RemoteSafeSessionSummary>();
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
            var result = AgentFlow.SelectApprovedLogLocation(AgentSettings.ToInput(pickResult.AgentLogLocationPath, SelectedAgentProviderType));
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

            return GitFlow.SelectLocalOnlyApprovedRepositoryPath(entry.LocalPath);
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
            var result = AgentFlow.SelectApprovedLogLocation(AgentSettings.ToInput(entry.LocalPath, SelectedAgentProviderType));
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

        public async Task<Result<AgentAnalysisReviewModel>> AnalyzeAgentActivityAsync(CancellationToken cancellationToken = default)
        {
            return await AgentFlow.AnalyzeAsync(cancellationToken);
        }

        public async Task<Result<SaveData>> SaveGitSessionAsync(CancellationToken cancellationToken = default)
        {
            var result = await GitFlow.SaveSessionAsync(cancellationToken);
            if (result.IsSuccess)
            {
                await RefreshRecentSessionsAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result<SaveData>> SaveAgentSessionAsync(CancellationToken cancellationToken = default)
        {
            var result = await AgentFlow.SaveSessionAsync(cancellationToken);
            if (result.IsSuccess)
            {
                await RefreshRecentSessionsAsync(cancellationToken);
            }

            return result;
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

        public async Task<Result<List<RecentSafeSessionSummary>>> RefreshRecentSessionsAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            var summaries = (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .OrderByDescending(session => session.EndedAt)
                .Take(RecentSessionLimit)
                .Select(ToRecentSummary)
                .Where(summary => privacySanitizer.ValidateNoForbiddenFields(summary).IsSuccess)
                .ToList();

            RecentSessions = summaries;
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
            var result = SafeSyncConfirmationRequestFactory.Cancel(PendingSafeSyncConfirmation);
            PendingSafeSyncConfirmation = null;
            pendingSafeSyncConfirmationAction = null;
            SafeSyncConfirmationTypedPhrase = string.Empty;
            return result;
        }

        public async Task<SafeSyncResult> ConfirmSafeSyncConfirmationAsync(string typedPhrase = "", CancellationToken cancellationToken = default)
        {
            var request = PendingSafeSyncConfirmation;
            var action = pendingSafeSyncConfirmationAction;
            if (request == null || action == null)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.Ready, SafeSyncApiError.MergePolicyConfirmationRequired, "Open a confirmation before applying this Safe Sync action."));
            }

            var result = SafeSyncConfirmationRequestFactory.Confirm(request, string.IsNullOrWhiteSpace(typedPhrase) ? SafeSyncConfirmationTypedPhrase : typedPhrase);
            if (!result.Confirmed)
            {
                return SetSafeSyncResult(SafeSyncResult.Failure(SafeSyncStatus.Ready, SafeSyncApiError.MergePolicyConfirmationRequired, "Typed confirmation did not match."));
            }

            PendingSafeSyncConfirmation = null;
            pendingSafeSyncConfirmationAction = null;
            SafeSyncConfirmationTypedPhrase = string.Empty;
            return await action(cancellationToken);
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

        private static RecentSafeSessionSummary ToRecentSummary(AgentWorkSession session)
        {
            session = session ?? new AgentWorkSession();
            var gitSummary = session.GitChangeSummary ?? GitChangeSummary.Empty();
            var agentSummary = session.AgentActivitySummary ?? AgentActivitySummary.Empty();

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
                WarningIds = (session.Warnings ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal).Take(8).ToList()
            };
        }

        private static string SafeLocalAlias(string displayAlias, string fallback)
        {
            return string.IsNullOrWhiteSpace(displayAlias) ? fallback : displayAlias.Trim();
        }
    }
}
