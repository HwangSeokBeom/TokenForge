using System;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Agents
{
    public sealed class ManualFallbackProvider : IAgentLogProvider
    {
        public const string Id = "ManualFallbackProvider";
        private readonly ManualFallbackInput input;

        public ManualFallbackProvider(ManualFallbackInput input)
        {
            this.input = input ?? new ManualFallbackInput();
        }

        public string ProviderId => Id;
        public string ParserVersion => "manual-fallback-v1";

        public Task<bool> IsAvailableAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<AgentProviderResult> AnalyzeAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            var session = new AgentWorkSession
            {
                AgentType = AgentType.ManualFallback,
                WorkType = input.WorkType,
                StartedAt = context.ScanStartedAt,
                EndedAt = context.ScanEndedAt,
                TokenUsageBucket = input.TokenUsageBucket,
                ResultStatus = input.ResultStatus,
                SourceProvider = ProviderId,
                SourceProviders = { ProviderId },
                ParserVersion = ParserVersion,
                Confidence = ProviderConfidence.Low,
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = Math.Max(0, input.ChangedFileCount),
                    ProjectPathHash = context.ProjectPathHash
                },
                ActionSummary = new AgentActionSummary
                {
                    FileEditCount = Math.Max(0, input.ChangedFileCount),
                    TestRunCount = input.TestRunDetected ? 1 : 0,
                    BuildRunCount = input.BuildRunDetected ? 1 : 0
                },
                UserReviewed = true
            };

            session.DeduplicationKey = $"{context.ProjectPathHash}:{session.StartedAt:yyyyMMddHH}:{session.WorkType}:manual";
            return Task.FromResult(AgentProviderResult.Success(ProviderId, session, ProviderConfidence.Low));
        }
    }
}
