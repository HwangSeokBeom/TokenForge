using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Agents
{
    public sealed class ManualSessionProvider : IAgentLogProvider
    {
        public const string Id = "ManualSessionProvider";
        private readonly ManualSessionInput input;
        private readonly PrivacySanitizer privacySanitizer;

        public ManualSessionProvider(ManualSessionInput input, PrivacySanitizer privacySanitizer = null)
        {
            this.input = input ?? new ManualSessionInput();
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
        }

        public string ProviderId => Id;
        public string ParserVersion => "manual-session-v1";

        public Task<bool> IsAvailableAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<AgentProviderResult> AnalyzeAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            context = context ?? new AgentProviderContext();

            var projectHash = !string.IsNullOrWhiteSpace(context.ProjectPathHash)
                ? context.ProjectPathHash
                : privacySanitizer.SanitizeProjectPath(context.ProjectRootPath);

            var addedBucket = input.AddedLineBucket != LineChangeBucket.Unknown
                ? input.AddedLineBucket
                : privacySanitizer.BucketLineCount(input.ApproximateAddedLineCount);
            var deletedBucket = input.DeletedLineBucket != LineChangeBucket.Unknown
                ? input.DeletedLineBucket
                : privacySanitizer.BucketLineCount(input.ApproximateDeletedLineCount);
            var tokenBucket = input.TokenUsageBucket != TokenUsageBucket.Unknown
                ? input.TokenUsageBucket
                : privacySanitizer.BucketTokenUsage(input.ApproximateTokenCount);

            var session = new AgentWorkSession
            {
                AgentType = input.AgentType,
                WorkType = input.WorkType,
                StartedAt = context.ScanStartedAt,
                EndedAt = context.ScanEndedAt >= context.ScanStartedAt ? context.ScanEndedAt : context.ScanStartedAt,
                TokenUsageBucket = tokenBucket,
                ResultStatus = input.ResultStatus,
                SourceProvider = ProviderId,
                SourceProviders = { ProviderId },
                ParserVersion = ParserVersion,
                Confidence = input.Confidence,
                Warnings = SafeWarnings(input.Warnings),
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = Math.Max(0, input.ChangedFileCount),
                    AddedLineBucket = addedBucket,
                    DeletedLineBucket = deletedBucket,
                    TestFileChanged = input.TestFileChanged,
                    DocsFileChanged = input.DocsFileChanged,
                    UiFileChanged = input.UiFileChanged,
                    ArchitectureFileChanged = input.ArchitectureFileChanged,
                    FileCategoryCounts = BuildCategoryCounts(input),
                    WorkingTreeStatus = input.ChangedFileCount > 0 ? WorkingTreeStatus.HasChanges : WorkingTreeStatus.Unknown,
                    ProjectPathHash = projectHash
                },
                ActionSummary = new AgentActionSummary
                {
                    FileEditCount = Math.Max(0, input.ChangedFileCount),
                    TestRunCount = input.TestRunDetected ? 1 : 0,
                    BuildRunCount = input.BuildRunDetected ? 1 : 0
                },
                UserReviewed = true
            };

            session.DeduplicationKey = $"{projectHash}:{session.StartedAt:yyyyMMddHH}:{session.WorkType}:manual";
            var validation = privacySanitizer.ValidateSafeSession(session);
            if (!validation.IsSuccess)
            {
                return Task.FromResult(AgentProviderResult.Failure(ProviderId, validation.ErrorCode, validation.ErrorMessage));
            }

            return Task.FromResult(AgentProviderResult.Success(ProviderId, session, input.Confidence));
        }

        private static List<FileCategoryCount> BuildCategoryCounts(ManualSessionInput input)
        {
            var counts = new List<FileCategoryCount>();
            AddCount(counts, FileCategory.Test, input.TestFileChanged);
            AddCount(counts, FileCategory.Docs, input.DocsFileChanged);
            AddCount(counts, FileCategory.UI, input.UiFileChanged);
            AddCount(counts, FileCategory.Architecture, input.ArchitectureFileChanged);
            return counts;
        }

        private static void AddCount(List<FileCategoryCount> counts, FileCategory category, bool changed)
        {
            if (changed)
            {
                counts.Add(new FileCategoryCount { Category = category, Count = 1 });
            }
        }

        private static List<string> SafeWarnings(IEnumerable<string> warnings)
        {
            return warnings == null
                ? new List<string>()
                : warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)).Select(warning => warning.Trim()).ToList();
        }
    }
}
