using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Growth;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;

namespace TokenForge.Client.UI
{
    public enum GitAnalysisFlowState
    {
        Idle,
        Selected,
        Analyzing,
        ReviewReady,
        Saving,
        Saved,
        Syncing,
        Synced,
        Failed
    }

    public sealed class GitAnalysisFlowController
    {
        private readonly IRepositoryPicker repositoryPicker;
        private readonly GitAggregateAnalyzer analyzer;
        private readonly ILocalSaveDataRepository repository;
        private readonly GrowthCalculator growthCalculator;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly ISyncService syncService;
        private readonly IGitAnalysisLogger logger;

        private string selectedRepositoryRootPath = string.Empty;
        private AgentWorkSession pendingSession;

        public GitAnalysisFlowController(
            IRepositoryPicker repositoryPicker,
            GitAggregateAnalyzer analyzer,
            ILocalSaveDataRepository repository,
            GrowthCalculator growthCalculator = null,
            PrivacySanitizer privacySanitizer = null,
            ISyncService syncService = null,
            IGitAnalysisLogger logger = null)
        {
            this.repositoryPicker = repositoryPicker ?? throw new ArgumentNullException(nameof(repositoryPicker));
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.logger = logger;
            this.analyzer = analyzer ?? new GitAggregateAnalyzer(null, this.privacySanitizer, logger);
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.growthCalculator = growthCalculator ?? new GrowthCalculator();
            this.syncService = syncService;
        }

        public GitAnalysisFlowState State { get; private set; } = GitAnalysisFlowState.Idle;
        public GitAnalysisSettings Settings { get; } = new GitAnalysisSettings();
        public GitAnalysisReviewModel Review { get; private set; }
        public string SelectionStatus { get; private set; } = "No repository selected";
        public string ErrorCategory { get; private set; } = string.Empty;
        public string UserMessage { get; private set; } = "Select a repository to begin.";
        public bool CanSync => syncService != null;
        public bool HasPendingReview => pendingSession != null && Review != null;
        public bool HasSelectedRepositoryForLocalOnlyApproval => !string.IsNullOrWhiteSpace(selectedRepositoryRootPath);
        public AgentWorkSession PendingSessionForLocalOnlyApproval => pendingSession;

        public string GetSelectedRepositoryPathForLocalOnlyApproval()
        {
            return selectedRepositoryRootPath;
        }

        public Result DiscardPendingReview()
        {
            pendingSession = null;
            Review = null;
            State = string.IsNullOrWhiteSpace(selectedRepositoryRootPath) ? GitAnalysisFlowState.Idle : GitAnalysisFlowState.Selected;
            ErrorCategory = string.Empty;
            SelectionStatus = string.IsNullOrWhiteSpace(selectedRepositoryRootPath) ? "No repository selected" : "Repository selected";
            UserMessage = "Pending Git analysis discarded.";
            logger?.Info("Git analysis pending review discarded");
            return Result.Success();
        }

        public Result ClearSelection()
        {
            selectedRepositoryRootPath = string.Empty;
            pendingSession = null;
            Review = null;
            State = GitAnalysisFlowState.Idle;
            ErrorCategory = string.Empty;
            SelectionStatus = "No repository selected";
            UserMessage = "Repository selection cleared.";
            logger?.Info("Git repository selection cleared");
            return Result.Success();
        }

        public async Task<Result> SelectRepositoryAsync(CancellationToken cancellationToken = default)
        {
            logger?.Info("Git repository selection started");
            var result = await repositoryPicker.PickRepositoryAsync(cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                selectedRepositoryRootPath = string.Empty;
                SelectionStatus = "No repository selected";
                Review = null;
                pendingSession = null;
                if (result.IsCancelled)
                {
                    State = GitAnalysisFlowState.Idle;
                    UserMessage = "Repository selection cancelled.";
                    ErrorCategory = string.Empty;
                    logger?.Info("Git repository selected=false");
                    return Result.Success();
                }

                return Fail(result.ErrorCode, "Repository selection is unavailable.");
            }

            selectedRepositoryRootPath = result.RepositoryRootPath;
            Review = null;
            pendingSession = null;
            var profileResult = await PersistSelectedRepositoryProfileAsync(selectedRepositoryRootPath, cancellationToken).ConfigureAwait(false);
            if (!profileResult.IsSuccess)
            {
                return Fail(profileResult.ErrorCode, profileResult.ErrorMessage);
            }

            State = GitAnalysisFlowState.Selected;
            ErrorCategory = string.Empty;
            SelectionStatus = profileResult.Value.SafeRepositoryAlias + " selected";
            UserMessage = "Repository selected. Run analysis to review safe aggregate data.";
            logger?.Info("Git repository selected=true");
            return Result.Success();
        }

        public Result SelectDevelopmentRepositoryPath(string repositoryRootPath)
        {
            return Fail("async_repository_selection_required", "Repository selection must run asynchronously.");
        }

        public Task<Result> SelectDevelopmentRepositoryPathAsync(string repositoryRootPath, CancellationToken cancellationToken = default)
        {
            return SelectLocalOnlyApprovedRepositoryPathAsync(repositoryRootPath, cancellationToken);
        }

        public Task<Result> SelectLocalOnlyApprovedRepositoryPathAsync(string repositoryRootPath, CancellationToken cancellationToken = default)
        {
            return SelectLocalOnlyApprovedRepositoryPathInternalAsync(repositoryRootPath, cancellationToken);
        }

        public Task<Result<GitAnalysisReviewModel>> AnalyzeAsync(CancellationToken cancellationToken = default)
        {
            return AnalyzeAsync(null, cancellationToken);
        }

        public async Task<Result<GitAnalysisReviewModel>> AnalyzeAsync(GitAnalysisMode? requestedAnalysisMode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(selectedRepositoryRootPath))
            {
                var failure = Fail("NoActiveRepository", "Connect a repository first.");
                return Result<GitAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            State = GitAnalysisFlowState.Analyzing;
            ErrorCategory = string.Empty;
            UserMessage = "Analyzing repository aggregate activity.";
            logger?.Info("Git analysis flow analysis started");

            var saveDataBeforeAnalysis = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
            RepositoryCompanionProfileService.Normalize(saveDataBeforeAnalysis);
            var selectedRepositoryHash = RepositoryCompanionProfileService.HashRepositoryPath(selectedRepositoryRootPath);
            var connection = FindConnectedProject(saveDataBeforeAnalysis, selectedRepositoryHash);
            var analysisMode = requestedAnalysisMode ?? (connection == null || string.IsNullOrWhiteSpace(connection.LastAnalyzedCommit)
                ? GitAnalysisMode.FullBaseline
                : GitAnalysisMode.Incremental);
            var input = Settings.ToInput(selectedRepositoryRootPath, analysisMode, connection?.LastAnalyzedCommit ?? string.Empty);
            var analysisResult = await Task.Run(() => analyzer.AnalyzeAsync(input, cancellationToken), cancellationToken).ConfigureAwait(false);
            if (!analysisResult.IsSuccess)
            {
                var failure = Fail(analysisResult.ErrorCode, SafeGitFailureMessage(analysisResult.ErrorCode, analysisResult.ErrorMessage));
                return Result<GitAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            var session = GitAnalysisSessionProvider.CreateSession(analysisResult.Value);
            var sessionValidation = privacySanitizer.ValidateSafeSession(session);
            if (!sessionValidation.IsSuccess)
            {
                var failure = Fail(sessionValidation.ErrorCode, "Analysis result failed privacy validation.");
                return Result<GitAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            var saveData = saveDataBeforeAnalysis;
            var growthResult = growthCalculator.Calculate(
                session,
                saveData.CharacterProfile,
                saveData.DailyProgress?.ExpGainedToday ?? 0,
                0f);

            var review = GitAnalysisReviewModel.From(session, growthResult);
            var reviewValidation = privacySanitizer.ValidateNoForbiddenFields(review);
            if (!reviewValidation.IsSuccess)
            {
                var failure = Fail(reviewValidation.ErrorCode, "Review failed privacy validation.");
                return Result<GitAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            pendingSession = session;
            Review = review;
            State = GitAnalysisFlowState.ReviewReady;
            SelectionStatus = "Repository analyzed";
            UserMessage = "Review the safe aggregate summary before saving.";
            logger?.Info("Git analysis flow review ready");
            return Result<GitAnalysisReviewModel>.Success(review);
        }

        public async Task<Result<SaveData>> SaveSessionAsync(CancellationToken cancellationToken = default)
        {
            if (pendingSession == null)
            {
                var failure = Fail("missing_review_session", "Analyze a repository before saving.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            State = GitAnalysisFlowState.Saving;
            ErrorCategory = string.Empty;
            UserMessage = "Saving reviewed session.";

            var sessionValidation = privacySanitizer.ValidateSafeSession(pendingSession);
            if (!sessionValidation.IsSuccess)
            {
                var failure = Fail(sessionValidation.ErrorCode, "Reviewed session failed privacy validation.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            var saveData = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
            saveData.CharacterProfile = saveData.CharacterProfile ?? new CharacterProfile();
            saveData.CompanionState = CompanionProgressionRules.Normalize(saveData.CompanionState);
            saveData.DailyProgress = saveData.DailyProgress ?? new DailyProgress();
            saveData.WorkSessionSummaries = saveData.WorkSessionSummaries ?? new System.Collections.Generic.List<AgentWorkSession>();
            saveData.GrowthHistory = saveData.GrowthHistory ?? new System.Collections.Generic.List<CharacterGrowthResult>();
            var selectedRepositoryHash = RepositoryCompanionProfileService.SafeRepositoryHashForSession(pendingSession);
            if (string.IsNullOrWhiteSpace(selectedRepositoryHash))
            {
                selectedRepositoryHash = RepositoryCompanionProfileService.HashRepositoryPath(selectedRepositoryRootPath);
                pendingSession.GitChangeSummary = pendingSession.GitChangeSummary ?? GitChangeSummary.Empty();
                pendingSession.GitChangeSummary.ProjectPathHash = selectedRepositoryHash;
            }

            RepositoryCompanionProfileService.Normalize(saveData);
            saveData.SelectedRepositoryHash = selectedRepositoryHash;
            if (!string.IsNullOrWhiteSpace(pendingSession.DeduplicationKey) &&
                saveData.WorkSessionSummaries.Any(session => string.Equals(session.DeduplicationKey, pendingSession.DeduplicationKey, StringComparison.Ordinal)))
            {
                pendingSession = null;
                State = GitAnalysisFlowState.Saved;
                UserMessage = "Analysis range was already saved.";
                return Result<SaveData>.Success(saveData);
            }

            var growthResult = growthCalculator.Calculate(
                pendingSession,
                saveData.CharacterProfile,
                saveData.DailyProgress.ExpGainedToday,
                0f);

            ApplyGrowth(saveData.CharacterProfile, growthResult);
            saveData.WorkSessionSummaries.Add(pendingSession);
            saveData.GrowthHistory.Add(growthResult);
            var repositorySessionIds = saveData.WorkSessionSummaries
                .Where(session => string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), selectedRepositoryHash, StringComparison.Ordinal))
                .Select(session => session.SessionId)
                .ToList();
            var repositorySessions = saveData.WorkSessionSummaries
                .Where(session => repositorySessionIds.Contains(session.SessionId))
                .ToList();
            var repositoryGrowth = saveData.GrowthHistory
                .Where(growth => repositorySessionIds.Contains(growth.SessionId))
                .ToList();
            RepositoryCompanionProfileService.ApplyApprovedGrowth(saveData, pendingSession, repositorySessions, repositoryGrowth);
            ApplyRepositoryAnalysisCheckpoint(saveData, selectedRepositoryHash, pendingSession.GitChangeSummary);
            saveData.DailyProgress.ExpGainedToday += growthResult.ExpGained;
            saveData.DailyProgress.SessionsConfirmedToday += 1;

            var saveValidation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!saveValidation.IsSuccess)
            {
                var failure = Fail(saveValidation.ErrorCode, "Save data failed privacy validation.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken).ConfigureAwait(false);
            if (!saveResult.IsSuccess)
            {
                var failure = Fail(saveResult.ErrorCode, "Save failed with a safe error category.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            State = GitAnalysisFlowState.Saved;
            ErrorCategory = string.Empty;
            UserMessage = "Session saved.";
            pendingSession = null;
            logger?.Info("Git analysis save completed");
            return Result<SaveData>.Success(saveData);
        }

        public async Task<Result<SaveData>> SyncNowAsync(CancellationToken cancellationToken = default)
        {
            if (syncService == null)
            {
                var failure = Fail("sync_not_configured", "Sync is not configured.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            if (State != GitAnalysisFlowState.Saved && State != GitAnalysisFlowState.Synced)
            {
                var failure = Fail("sync_requires_saved_session", "Save the reviewed session before syncing.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            State = GitAnalysisFlowState.Syncing;
            ErrorCategory = string.Empty;
            UserMessage = "Syncing safe aggregate data.";
            var syncResult = await syncService.PushThenPullAsync(cancellationToken).ConfigureAwait(false);
            if (!syncResult.IsSuccess)
            {
                var failure = Fail(syncResult.ErrorCode, "Sync failed with a safe error category.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            State = GitAnalysisFlowState.Synced;
            ErrorCategory = string.Empty;
            UserMessage = "Sync completed.";
            logger?.Info("Git analysis sync completed");
            return syncResult;
        }

        private Result Fail(string category, string message)
        {
            State = GitAnalysisFlowState.Failed;
            ErrorCategory = string.IsNullOrWhiteSpace(category) ? "unknown_error" : category;
            UserMessage = message;
            logger?.Warning("Git analysis flow failed category=" + ErrorCategory);
            return Result.Failure(ErrorCategory, message);
        }

        private async Task<Result<RepositoryCompanionProfile>> PersistSelectedRepositoryProfileAsync(string repositoryRootPath, CancellationToken cancellationToken)
        {
            var saveData = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
            var profileResult = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repositoryRootPath);
            if (!profileResult.IsSuccess)
            {
                return profileResult;
            }

            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return Result<RepositoryCompanionProfile>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken).ConfigureAwait(false);
            return saveResult.IsSuccess
                ? profileResult
                : Result<RepositoryCompanionProfile>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
        }

        private static ConnectedProject FindConnectedProject(SaveData saveData, string repositoryHash)
        {
            return (saveData?.ConnectedProjects ?? new System.Collections.Generic.List<ConnectedProject>())
                .FirstOrDefault(project => project != null &&
                                           !project.IsArchived &&
                                           (string.Equals(project.Id, repositoryHash, StringComparison.Ordinal) ||
                                            string.Equals(project.PathHash, repositoryHash, StringComparison.Ordinal) ||
                                            string.Equals(project.ProjectPathHash, repositoryHash, StringComparison.Ordinal)));
        }

        private static void ApplyRepositoryAnalysisCheckpoint(SaveData saveData, string repositoryHash, GitChangeSummary summary)
        {
            var connection = FindConnectedProject(saveData, repositoryHash);
            if (connection == null || summary == null)
            {
                return;
            }

            connection.LastAnalyzedAt = DateTimeOffset.UtcNow;
            connection.LastAnalyzedCommit = summary.LastAnalyzedCommit ?? string.Empty;
            connection.FirstCommitAt = summary.FirstCommitAtUtc ?? string.Empty;
            connection.TotalCommitCount = Math.Max(0, summary.TotalCommitsAnalyzed);
            connection.AnalyzedCommitRange = (summary.AnalyzedStartCommit ?? string.Empty) + ".." + (summary.AnalyzedEndCommit ?? string.Empty);
            connection.LastAnalysisMode = summary.AnalysisMode ?? string.Empty;
            connection.LastAnalysisScope = AnalysisScopeLabel(summary);
        }

        private static string AnalysisScopeLabel(GitChangeSummary summary)
        {
            if (summary == null)
            {
                return "Not analyzed";
            }

            var mode = NormalizedAnalysisMode(summary.AnalysisMode);
            if (string.Equals(mode, "fullbaseline", StringComparison.OrdinalIgnoreCase))
            {
                var start = string.IsNullOrWhiteSpace(summary.FirstCommitAtUtc) ? "initial commit" : DateOnly(summary.FirstCommitAtUtc);
                return "Full history · " + start + " → now";
            }

            if (string.Equals(mode, "incremental", StringComparison.OrdinalIgnoreCase))
            {
                return "Since last analysis · " + Math.Max(0, summary.IncrementalCommitCount) + " commits";
            }

            return "Recent range · " + Math.Max(1, summary.AnalysisWindowDays) + " days";
        }

        private static string DateOnly(string value)
        {
            return DateTimeOffset.TryParse(value, out var parsed)
                ? parsed.UtcDateTime.ToString("yyyy-MM-dd")
                : value;
        }

        private static string NormalizedAnalysisMode(string value)
        {
            return (value ?? string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Trim();
        }

        private async Task<Result> SelectLocalOnlyApprovedRepositoryPathInternalAsync(string repositoryRootPath, CancellationToken cancellationToken)
        {
            logger?.Info("Git repository selection started");
            if (string.IsNullOrWhiteSpace(repositoryRootPath))
            {
                return Fail("RepositoryPathMissing", "Repository path is missing. Reconnect required.");
            }

            if (!Directory.Exists(repositoryRootPath))
            {
                return Fail("RepositoryFolderNotFound", "Repository folder was not found. Reconnect required.");
            }

            var validation = await analyzer.ValidateRepositoryRootAsync(repositoryRootPath, cancellationToken).ConfigureAwait(false);
            if (!validation.IsSuccess)
            {
                return Fail(validation.ErrorCode, SafeGitFailureMessage(validation.ErrorCode, validation.ErrorMessage));
            }

            var canonicalRoot = (validation.Output ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(canonicalRoot) || !Directory.Exists(canonicalRoot))
            {
                return Fail("RepositoryFolderNotFound", "Repository folder was not found. Reconnect required.");
            }

            var profileResult = await PersistSelectedRepositoryProfileAsync(canonicalRoot, cancellationToken).ConfigureAwait(false);
            if (!profileResult.IsSuccess)
            {
                return Fail(profileResult.ErrorCode, profileResult.ErrorMessage);
            }

            selectedRepositoryRootPath = canonicalRoot;
            Review = null;
            pendingSession = null;
            State = GitAnalysisFlowState.Selected;
            ErrorCategory = string.Empty;
            SelectionStatus = profileResult.Value.SafeRepositoryAlias + " selected";
            UserMessage = "Repository selected. Run analysis to review safe aggregate data.";
            logger?.Info("Git repository selected=true");
            return Result.Success();
        }

        private static void ApplyGrowth(CharacterProfile profile, CharacterGrowthResult growthResult)
        {
            profile.TotalExp += growthResult.ExpGained;
            profile.Level = growthResult.LevelAfter;
            profile.Stats.Add(growthResult.StatDeltas);
            if (growthResult.EvolutionProgressDelta != EvolutionType.Unknown)
            {
                profile.CurrentEvolutionType = growthResult.EvolutionProgressDelta;
            }
        }

        private static string SafeGitFailureMessage(string errorCode, string errorMessage)
        {
            switch (errorCode)
            {
                case "ProcessTimeout":
                case "git_timeout": return "Git command timed out.";
                case "NotAGitRepository":
                case "not_git_repository": return "This folder is not a Git repository.";
                case "RepositoryFolderNotFound":
                case "path_not_found":
                case "invalid_repository_path": return "Repository folder was not found. Reconnect required.";
                case "PermissionDenied":
                case "permission_denied": return "Permission denied while reading repository folder.";
                case "git_unavailable":
                case "GitExecutableNotFound":
                case "git_executable_not_found": return "Git executable was not found. Install Xcode Command Line Tools or Git.";
                case "git_cancelled": return "Git command timed out.";
                case "GitCommandFailed":
                case "git_command_failed": return "Git command failed. Check repository state and try again.";
                case "RepositoryPathMissing":
                case "missing_repository_path":
                case "NoActiveRepository":
                case "missing_repository_selection": return "Connect a repository first.";
                default:
                    return string.IsNullOrWhiteSpace(errorMessage) ? "Analysis failed safely." : errorMessage;
            }
        }
    }
}
