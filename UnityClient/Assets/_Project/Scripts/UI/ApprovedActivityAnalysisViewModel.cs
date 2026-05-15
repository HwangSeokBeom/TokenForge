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
                                                   syncInProgress != 0 ||
                                                   fetchInProgress != 0 ||
                                                   deleteInProgress != 0 ||
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
            return SetSafeSyncResult(await safeSyncService.SyncNowAsync(cancellationToken));
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
