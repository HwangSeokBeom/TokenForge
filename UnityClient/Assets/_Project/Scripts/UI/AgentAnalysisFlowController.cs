using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Agents;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Growth;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;

namespace TokenForge.Client.UI
{
    public enum AgentAnalysisFlowState
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

    public sealed class AgentAnalysisFlowController
    {
        private readonly AgentLogActivityProvider provider;
        private readonly ILocalSaveDataRepository repository;
        private readonly GrowthCalculator growthCalculator;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly ISyncService syncService;
        private readonly IAgentAnalysisLogger logger;

        private AgentAnalysisInput pendingInput;
        private AgentWorkSession pendingSession;
        private string selectedRepositoryHash = string.Empty;

        public AgentAnalysisFlowController(
            AgentLogActivityProvider provider,
            ILocalSaveDataRepository repository,
            GrowthCalculator growthCalculator = null,
            PrivacySanitizer privacySanitizer = null,
            ISyncService syncService = null,
            IAgentAnalysisLogger logger = null)
        {
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.provider = provider ?? new AgentLogActivityProvider(null, this.privacySanitizer);
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.growthCalculator = growthCalculator ?? new GrowthCalculator();
            this.syncService = syncService;
            this.logger = logger;
        }

        public AgentAnalysisFlowState State { get; private set; } = AgentAnalysisFlowState.Idle;
        public AgentAnalysisReviewModel Review { get; private set; }
        public string ErrorCategory { get; private set; } = string.Empty;
        public string UserMessage { get; private set; } = "Select an approved agent log location to begin.";
        public bool HasPendingReview => pendingSession != null && Review != null;
        public bool HasSelectedAgentLogLocationForLocalOnlyApproval => pendingInput != null && !string.IsNullOrWhiteSpace(pendingInput.SelectedLocationPath);
        public AgentWorkSession PendingSessionForLocalOnlyApproval => pendingSession;

        public void SetSelectedRepositoryHash(string repositoryHash)
        {
            selectedRepositoryHash = repositoryHash ?? string.Empty;
        }

        public string GetSelectedAgentLogLocationPathForLocalOnlyApproval()
        {
            return pendingInput?.SelectedLocationPath ?? string.Empty;
        }

        public Result DiscardPendingReview()
        {
            pendingSession = null;
            Review = null;
            State = pendingInput == null || string.IsNullOrWhiteSpace(pendingInput.SelectedLocationPath)
                ? AgentAnalysisFlowState.Idle
                : AgentAnalysisFlowState.Selected;
            ErrorCategory = string.Empty;
            UserMessage = "Pending agent log analysis discarded.";
            logger?.Info("Agent analysis pending review discarded");
            return Result.Success();
        }

        public Result ClearSelection()
        {
            pendingInput = null;
            pendingSession = null;
            Review = null;
            State = AgentAnalysisFlowState.Idle;
            ErrorCategory = string.Empty;
            UserMessage = "Agent log selection cleared.";
            logger?.Info("Agent analysis location cleared");
            return Result.Success();
        }

        public Result MarkBlocked(string category, string message)
        {
            return Fail(category, message);
        }

        public Result SelectApprovedLogLocation(AgentAnalysisInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.SelectedLocationPath))
            {
                return Fail("missing_agent_log_location", "Agent log location is required.");
            }

            input.ProviderHint = MacAgentSourceDetector.NormalizeProvider(input.ProviderHint);
            input.SourceKind = input.SourceKind == AgentSourceKind.Unknown ? AgentSourceKind.ManualFolder : input.SourceKind;
            pendingInput = input;
            pendingSession = null;
            Review = null;
            State = AgentAnalysisFlowState.Selected;
            ErrorCategory = string.Empty;
            UserMessage = "Agent log location selected. Run analysis to review safe aggregate data.";
            logger?.Info("Agent analysis location selected=true");
            return Result.Success();
        }

        public async Task<Result<AgentAnalysisReviewModel>> AnalyzeAsync(CancellationToken cancellationToken = default)
        {
            if (pendingInput == null || string.IsNullOrWhiteSpace(pendingInput.SelectedLocationPath))
            {
                var failure = Fail("missing_agent_log_location", "Select an approved agent log location before analyzing.");
                return Result<AgentAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            State = AgentAnalysisFlowState.Analyzing;
            ErrorCategory = string.Empty;
            UserMessage = "Analyzing safe aggregate agent activity.";

            var result = await Task.Run(() => provider.AnalyzeSessionAsync(pendingInput, cancellationToken), cancellationToken);
            if (!result.IsSuccess)
            {
                var failure = Fail(result.ErrorCode, "Agent analysis failed with a safe error category. " + result.ErrorMessage);
                return Result<AgentAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            var sessionValidation = privacySanitizer.ValidateSafeSession(result.Value);
            if (!sessionValidation.IsSuccess)
            {
                var failure = Fail(sessionValidation.ErrorCode, "Agent analysis result failed privacy validation.");
                return Result<AgentAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            var growthResult = growthCalculator.Calculate(
                result.Value,
                saveData.CharacterProfile,
                saveData.DailyProgress?.ExpGainedToday ?? 0,
                0f);

            var saveEligible = result.Value.AgentActivitySummary.ConfidenceLevel != ConfidenceLevel.Low ||
                !(result.Value.AgentActivitySummary.WarningIds ?? new System.Collections.Generic.List<string>()).Contains("agent_log_unsupported_format");
            var review = AgentAnalysisReviewModel.From(result.Value, growthResult, saveEligible);
            var reviewValidation = privacySanitizer.ValidateNoForbiddenFields(review);
            if (!reviewValidation.IsSuccess)
            {
                var failure = Fail(reviewValidation.ErrorCode, "Agent review failed privacy validation.");
                return Result<AgentAnalysisReviewModel>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            pendingSession = result.Value;
            Review = review;
            State = AgentAnalysisFlowState.ReviewReady;
            ErrorCategory = string.Empty;
            UserMessage = "Review the safe aggregate agent summary before saving.";
            logger?.Info("Agent analysis review ready");
            return Result<AgentAnalysisReviewModel>.Success(review);
        }

        public async Task<Result<SaveData>> SaveSessionAsync(CancellationToken cancellationToken = default)
        {
            if (pendingSession == null || Review == null)
            {
                var failure = Fail("missing_agent_review_session", "Analyze agent activity before saving.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            if (!Review.SaveEligible)
            {
                var failure = Fail("agent_review_not_save_eligible", "Agent analysis requires a supported safe summary before saving.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            State = AgentAnalysisFlowState.Saving;
            ErrorCategory = string.Empty;
            UserMessage = "Saving reviewed agent session.";

            var sessionValidation = privacySanitizer.ValidateSafeSession(pendingSession);
            if (!sessionValidation.IsSuccess)
            {
                var failure = Fail(sessionValidation.ErrorCode, "Reviewed agent session failed privacy validation.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.CharacterProfile = saveData.CharacterProfile ?? new CharacterProfile();
            saveData.CompanionState = CompanionProgressionRules.Normalize(saveData.CompanionState);
            RepositoryCompanionProfileService.Normalize(saveData);
            saveData.DailyProgress = saveData.DailyProgress ?? new DailyProgress();
            saveData.WorkSessionSummaries = saveData.WorkSessionSummaries ?? new System.Collections.Generic.List<AgentWorkSession>();
            saveData.GrowthHistory = saveData.GrowthHistory ?? new System.Collections.Generic.List<CharacterGrowthResult>();
            var repositoryHash = string.IsNullOrWhiteSpace(selectedRepositoryHash)
                ? saveData.SelectedRepositoryHash
                : selectedRepositoryHash;
            if (!string.IsNullOrWhiteSpace(repositoryHash))
            {
                pendingSession.GitChangeSummary = pendingSession.GitChangeSummary ?? GitChangeSummary.Empty();
                pendingSession.GitChangeSummary.ProjectPathHash = repositoryHash;
                saveData.SelectedRepositoryHash = repositoryHash;
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
                .Where(session => string.Equals(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session), repositoryHash, StringComparison.Ordinal))
                .Select(session => session.SessionId)
                .ToList();
            var repositorySessions = saveData.WorkSessionSummaries
                .Where(session => repositorySessionIds.Contains(session.SessionId))
                .ToList();
            var repositoryGrowth = saveData.GrowthHistory
                .Where(growth => repositorySessionIds.Contains(growth.SessionId))
                .ToList();
            RepositoryCompanionProfileService.ApplyApprovedGrowth(saveData, pendingSession, repositorySessions, repositoryGrowth);
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

            State = AgentAnalysisFlowState.Saved;
            ErrorCategory = string.Empty;
            UserMessage = "Agent session saved.";
            pendingSession = null;
            logger?.Info("Agent analysis save completed");
            return Result<SaveData>.Success(saveData);
        }

        public async Task<Result<SaveData>> SyncNowAsync(CancellationToken cancellationToken = default)
        {
            if (syncService == null)
            {
                var failure = Fail("sync_not_configured", "Sync is not configured.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            if (State != AgentAnalysisFlowState.Saved && State != AgentAnalysisFlowState.Synced)
            {
                var failure = Fail("sync_requires_saved_session", "Save the reviewed agent session before syncing.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            State = AgentAnalysisFlowState.Syncing;
            ErrorCategory = string.Empty;
            UserMessage = "Syncing safe aggregate data.";
            var syncResult = await syncService.PushThenPullAsync(cancellationToken);
            if (!syncResult.IsSuccess)
            {
                var failure = Fail(syncResult.ErrorCode, "Sync failed with a safe error category.");
                return Result<SaveData>.Failure(failure.ErrorCode, failure.ErrorMessage);
            }

            State = AgentAnalysisFlowState.Synced;
            ErrorCategory = string.Empty;
            UserMessage = "Sync completed.";
            logger?.Info("Agent analysis sync completed");
            return syncResult;
        }

        private Result Fail(string category, string message)
        {
            State = AgentAnalysisFlowState.Failed;
            ErrorCategory = string.IsNullOrWhiteSpace(category) ? "unknown_error" : category;
            UserMessage = message;
            logger?.Warning("Agent analysis flow failed category=" + ErrorCategory);
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
