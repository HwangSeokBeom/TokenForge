using System;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Agents;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Growth;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Platform
{
    public sealed class BootstrapSmokeFlow
    {
        private readonly SaveDataRepository repository;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly GrowthCalculator growthCalculator;

        public BootstrapSmokeFlow(SaveDataRepository repository = null, PrivacySanitizer privacySanitizer = null, GrowthCalculator growthCalculator = null)
        {
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.repository = repository ?? new SaveDataRepository(null, this.privacySanitizer);
            this.growthCalculator = growthCalculator ?? new GrowthCalculator();
        }

        public async Task<Result<SaveData>> RunIfEmptyAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            saveData.CharacterProfile = saveData.CharacterProfile ?? new CharacterProfile();

            if (saveData.WorkSessionSummaries.Count > 0)
            {
                return Result<SaveData>.Success(saveData);
            }

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
