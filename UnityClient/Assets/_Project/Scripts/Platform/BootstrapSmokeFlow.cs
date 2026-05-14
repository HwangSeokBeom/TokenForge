using System;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Agents;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Growth;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Platform
{
    public sealed class BootstrapSmokeFlow
    {
        private readonly SaveDataRepository repository;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly GrowthCalculator growthCalculator;
        private readonly ISyncService syncService;
        private readonly BackendSyncSmokeFlow backendSyncSmokeFlow;

        public BootstrapSmokeFlow(
            SaveDataRepository repository = null,
            PrivacySanitizer privacySanitizer = null,
            GrowthCalculator growthCalculator = null,
            ISyncService syncService = null,
            BackendSyncSmokeFlow backendSyncSmokeFlow = null)
        {
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.repository = repository ?? new SaveDataRepository(null, this.privacySanitizer);
            this.growthCalculator = growthCalculator ?? new GrowthCalculator();
            this.syncService = syncService;
            this.backendSyncSmokeFlow = backendSyncSmokeFlow;
        }

        public async Task<Result<SaveData>> RunIfEmptyAsync(CancellationToken cancellationToken = default)
        {
            return await RunIfEmptyThenOptionalSyncAsync(true, BootstrapSyncMode.None, cancellationToken);
        }

        public async Task<Result<SaveData>> RunIfEmptyThenOptionalSyncAsync(
            bool runSafeSmokeFlowWhenEmpty,
            BootstrapSyncMode syncMode,
            CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.CharacterProfile = saveData.CharacterProfile ?? new CharacterProfile();

            if (runSafeSmokeFlowWhenEmpty && saveData.WorkSessionSummaries.Count == 0)
            {
                var smokeResult = await CreateSmokeSessionAsync(saveData, cancellationToken);
                if (!smokeResult.IsSuccess)
                {
                    return smokeResult;
                }

                saveData = smokeResult.Value;
            }

            if (syncMode == BootstrapSyncMode.None)
            {
                return Result<SaveData>.Success(saveData);
            }

            Result<SaveData> syncResult;
            if (syncMode == BootstrapSyncMode.RealBackendSmoke)
            {
                syncResult = backendSyncSmokeFlow == null
                    ? Result<SaveData>.Failure("sync_not_configured", "Backend smoke sync is not configured.")
                    : await backendSyncSmokeFlow.RunAsync(cancellationToken);
            }
            else if (syncService == null)
            {
                syncResult = Result<SaveData>.Failure("sync_not_configured", "Bootstrap sync is not configured.");
            }
            else
            {
                syncResult = syncMode == BootstrapSyncMode.PullOnly
                    ? await syncService.PullAsync(cancellationToken)
                    : await syncService.PushThenPullAsync(cancellationToken);
            }

            if (syncResult.IsSuccess)
            {
                return syncResult;
            }

            saveData.SyncState = saveData.SyncState ?? new SyncState();
            saveData.SyncState.LastErrorCode = syncResult.ErrorCode;

            var result = Result<SaveData>.Success(saveData);
            result.Warnings.Add(syncResult.ErrorCode);
            return result;
        }

        private async Task<Result<SaveData>> CreateSmokeSessionAsync(SaveData saveData, CancellationToken cancellationToken)
        {
            var provider = new ManualSessionProvider(new ManualSessionInput
            {
                AgentType = AgentType.ManualFallback,
                WorkType = WorkType.Feature,
                TokenUsageBucket = TokenUsageBucket.Medium,
                ChangedFileCount = 3,
                AddedLineBucket = LineChangeBucket.Medium,
                DeletedLineBucket = LineChangeBucket.Small,
                TestFileChanged = true,
                TestRunDetected = true,
                ResultStatus = ResultStatus.Succeeded,
                Confidence = ProviderConfidence.Low,
                Warnings = { "sample_safe_session" }
            }, privacySanitizer);

            var context = new AgentProviderContext
            {
                ProjectPathHash = privacySanitizer.HashString("bootstrap-sample-project"),
                ScanStartedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
                ScanEndedAt = DateTimeOffset.UtcNow
            };

            var providerResult = await provider.AnalyzeAsync(context, cancellationToken);
            if (!providerResult.IsSuccess || providerResult.Session == null)
            {
                return Result<SaveData>.Failure(
                    providerResult.Error?.Code ?? "bootstrap_provider_failed",
                    providerResult.Error?.Message ?? "Sample session could not be created.");
            }

            var validation = privacySanitizer.ValidateSafeSession(providerResult.Session);
            if (!validation.IsSuccess)
            {
                return Result<SaveData>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var growthResult = growthCalculator.Calculate(providerResult.Session, saveData.CharacterProfile, 0, 0f);
            ApplyGrowth(saveData.CharacterProfile, growthResult);
            saveData.WorkSessionSummaries.Add(providerResult.Session);
            saveData.GrowthHistory.Add(growthResult);
            saveData.DailyProgress.ExpGainedToday += growthResult.ExpGained;
            saveData.DailyProgress.SessionsConfirmedToday += 1;

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return Result<SaveData>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            return Result<SaveData>.Success(saveData);
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
