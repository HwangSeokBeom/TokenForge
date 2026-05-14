using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Growth;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Git
{
    public sealed class GitAnalysisSessionProvider
    {
        public const string SourceProviderId = "GIT";

        private readonly SaveDataRepository repository;
        private readonly GitAggregateAnalyzer analyzer;
        private readonly GrowthCalculator growthCalculator;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly IGitAnalysisLogger logger;

        public GitAnalysisSessionProvider(
            SaveDataRepository repository,
            GitAggregateAnalyzer analyzer = null,
            GrowthCalculator growthCalculator = null,
            PrivacySanitizer privacySanitizer = null,
            IGitAnalysisLogger logger = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.logger = logger;
            this.analyzer = analyzer ?? new GitAggregateAnalyzer(null, this.privacySanitizer, logger);
            this.growthCalculator = growthCalculator ?? new GrowthCalculator();
        }

        public async Task<Result<SaveData>> AnalyzeAndSaveAsync(GitRepositoryAnalysisInput input, CancellationToken cancellationToken = default)
        {
            var analysisResult = await analyzer.AnalyzeAsync(input, cancellationToken);
            if (!analysisResult.IsSuccess)
            {
                return Result<SaveData>.Failure(analysisResult.ErrorCode, analysisResult.ErrorMessage);
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            var session = CreateSession(analysisResult.Value);
            var sessionValidation = privacySanitizer.ValidateSafeSession(session);
            if (!sessionValidation.IsSuccess)
            {
                logger?.Warning("Git analysis failed category=session_privacy_validation");
                return Result<SaveData>.Failure(sessionValidation.ErrorCode, sessionValidation.ErrorMessage);
            }

            var growthResult = growthCalculator.Calculate(
                session,
                saveData.CharacterProfile,
                saveData.DailyProgress?.ExpGainedToday ?? 0,
                0f);

            ApplyGrowth(saveData.CharacterProfile, growthResult);
            saveData.WorkSessionSummaries.Add(session);
            saveData.GrowthHistory.Add(growthResult);
            saveData.DailyProgress.ExpGainedToday += growthResult.ExpGained;
            saveData.DailyProgress.SessionsConfirmedToday += 1;

            var saveValidation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!saveValidation.IsSuccess)
            {
                logger?.Warning("Git analysis failed category=save_privacy_validation");
                return Result<SaveData>.Failure(saveValidation.ErrorCode, saveValidation.ErrorMessage);
            }

            var saveResult = await repository.SaveAsync(saveData, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return Result<SaveData>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            return Result<SaveData>.Success(saveData);
        }

        public static AgentWorkSession CreateSession(GitChangeSummary summary)
        {
            summary = summary ?? GitChangeSummary.Empty();
            var categoryCounts = (summary.FileCategoryCounts ?? Enumerable.Empty<FileCategoryCount>())
                .GroupBy(item => item.Category)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
            var workType = WorkTypeInferenceService.Infer(categoryCounts, summary.ChangedFileCount);
            if (workType == WorkType.Unknown)
            {
                workType = InferWorkTypeFromExtensionBuckets(summary);
            }

            var confidence = ToProviderConfidence(summary.ConfidenceLevel);
            var endedAt = DateTimeOffset.UtcNow;
            var startedAt = endedAt.AddDays(-Math.Max(1, summary.AnalysisWindowDays));
            var session = new AgentWorkSession
            {
                AgentType = AgentType.GitOnly,
                WorkType = workType,
                StartedAt = startedAt,
                EndedAt = endedAt,
                TokenUsageBucket = TokenUsageBucket.None,
                ActionSummary = new AgentActionSummary
                {
                    FileEditCount = Math.Min(summary.ChangedFileCount, 500),
                    TestRunCount = summary.TestFileChanged ? 1 : 0,
                    BuildRunCount = HasExtensionCategory(summary, "config") ? 1 : 0,
                    DurationBucket = DurationBucket.Unknown
                },
                GitChangeSummary = summary,
                ResultStatus = ResultStatus.Succeeded,
                SourceProvider = SourceProviderId,
                SourceProviders = { SourceProviderId },
                ParserVersion = summary.AnalyzerVersion,
                Confidence = confidence,
                Warnings = (summary.PrivacyWarnings ?? Enumerable.Empty<string>()).Take(8).ToList(),
                UserReviewed = true
            };

            session.DeduplicationKey = $"{summary.ProjectPathHash}:{summary.AnalysisTimeBucket}:{summary.ChangedFileCountBucket}:{summary.CommitCountBucket}";
            return session;
        }

        private static WorkType InferWorkTypeFromExtensionBuckets(GitChangeSummary summary)
        {
            if (HasExtensionCategory(summary, "test")) return WorkType.Test;
            if (HasExtensionCategory(summary, "markdown")) return WorkType.Docs;
            if (HasExtensionCategory(summary, "config") || HasExtensionCategory(summary, "json")) return WorkType.Build;
            return summary.ChangedFileCount > 0 ? WorkType.Mixed : WorkType.Unknown;
        }

        private static bool HasExtensionCategory(GitChangeSummary summary, string category)
        {
            return (summary.ExtensionCategoryBuckets ?? Enumerable.Empty<ExtensionCategoryCount>())
                .Any(item => string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase) && item.Count > 0);
        }

        private static ProviderConfidence ToProviderConfidence(ConfidenceLevel confidence)
        {
            switch (confidence)
            {
                case ConfidenceLevel.High: return ProviderConfidence.High;
                case ConfidenceLevel.Medium: return ProviderConfidence.Medium;
                case ConfidenceLevel.Low: return ProviderConfidence.Low;
                default: return ProviderConfidence.Unknown;
            }
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
