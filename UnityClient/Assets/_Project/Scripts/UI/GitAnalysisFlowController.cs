using System;
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

        public string GetSelectedRepositoryPathForLocalOnlyApproval()
        {
            return selectedRepositoryRootPath;
        }

        public Result DiscardPendingReview()
        {
            selectedRepositoryRootPath = string.Empty;
            pendingSession = null;
            Review = null;
            State = GitAnalysisFlowState.Idle;
            ErrorCategory = string.Empty;
            SelectionStatus = "No repository selected";
            UserMessage = "Pending Git analysis discarded.";
            logger?.Info("Git analysis pending review discarded");
            return Result.Success();
        }

        public async Task<Result> SelectRepositoryAsync(CancellationToken cancellationToken = default)
        {
            logger?.Info("Git repository selection started");
            var result = await repositoryPicker.PickRepositoryAsync(cancellationToken);
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
            State = GitAnalysisFlowState.Selected;
            ErrorCategory = string.Empty;
            SelectionStatus = "Repository selected";
            UserMessage = "Repository selected. Run analysis to review safe aggregate data.";
            logger?.Info("Git repository selected=true");
            return Result.Success();
        }

        public Result SelectDevelopmentRepositoryPath(string repositoryRootPath)
        {
            return SelectLocalOnlyApprovedRepositoryPath(repositoryRootPath);
        }

        public Result SelectLocalOnlyApprovedRepositoryPath(string repositoryRootPath)
        {
            logger?.Info("Git repository selection started");
            if (string.IsNullOrWhiteSpace(repositoryRootPath))
            {
                return Fail("missing_repository_path", "Repository folder is required.");
            }

            selectedRepositoryRootPath = repositoryRootPath;
            Review = null;
            pendingSession = null;
            State = GitAnalysisFlowState.Selected;
            ErrorCategory = string.Empty;
            SelectionStatus = "Repository selected";
            UserMessage = "Repository selected. Run analysis to review safe aggregate data.";
            logger?.Info("Git repository selected=true");
            return Result.Success();
        }

        public async Task<Result<GitAnalysisReviewModel>> AnalyzeAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(selectedRepositoryRootPath))
            {
                var failure = Fail("missing_repository_selection", "Select a repository before analyzing.");
                return Result<GitAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            State = GitAnalysisFlowState.Analyzing;
            ErrorCategory = string.Empty;
            UserMessage = "Analyzing repository aggregate activity.";
            logger?.Info("Git analysis flow analysis started");

            var input = Settings.ToInput(selectedRepositoryRootPath);
            try
            {
                var analysisResult = await analyzer.AnalyzeAsync(input, cancellationToken);
                if (!analysisResult.IsSuccess)
                {
                    selectedRepositoryRootPath = string.Empty;
                    var failure = Fail(analysisResult.ErrorCode, "Analysis failed with a safe error category.");
                    return Result<GitAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
                }

                var session = GitAnalysisSessionProvider.CreateSession(analysisResult.Value);
                var sessionValidation = privacySanitizer.ValidateSafeSession(session);
                if (!sessionValidation.IsSuccess)
                {
                    selectedRepositoryRootPath = string.Empty;
                    var failure = Fail(sessionValidation.ErrorCode, "Analysis result failed privacy validation.");
                    return Result<GitAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
                }

                var saveData = await repository.LoadAsync(cancellationToken);
                var growthResult = growthCalculator.Calculate(
                    session,
                    saveData.CharacterProfile,
                    saveData.DailyProgress?.ExpGainedToday ?? 0,
                    0f);

                var review = GitAnalysisReviewModel.From(session, growthResult);
                var reviewValidation = privacySanitizer.ValidateNoForbiddenFields(review);
                if (!reviewValidation.IsSuccess)
                {
                    selectedRepositoryRootPath = string.Empty;
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
            finally
            {
                selectedRepositoryRootPath = string.Empty;
            }
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

            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.CharacterProfile = saveData.CharacterProfile ?? new CharacterProfile();
            saveData.DailyProgress = saveData.DailyProgress ?? new DailyProgress();
            var growthResult = growthCalculator.Calculate(
                pendingSession,
                saveData.CharacterProfile,
                saveData.DailyProgress.ExpGainedToday,
                0f);

            ApplyGrowth(saveData.CharacterProfile, growthResult);
            saveData.WorkSessionSummaries.Add(pendingSession);
            saveData.GrowthHistory.Add(growthResult);
            saveData.DailyProgress.ExpGainedToday += growthResult.ExpGained;
            saveData.DailyProgress.SessionsConfirmedToday += 1;

            var saveValidation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!saveValidation.IsSuccess)
            {
                var failure = Fail(saveValidation.ErrorCode, "Save data failed privacy validation.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
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
            var syncResult = await syncService.PushThenPullAsync(cancellationToken);
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
    }
}
