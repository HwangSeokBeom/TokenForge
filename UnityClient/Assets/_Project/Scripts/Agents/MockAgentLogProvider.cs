using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Agents
{
    public sealed class MockAgentLogProvider : IAgentLogProvider
    {
        public const string Id = "MockAgentLogProvider";

        public string ProviderId => Id;
        public string ParserVersion => "mock-v1";

        public Task<bool> IsAvailableAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<AgentProviderResult> AnalyzeAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            var session = new AgentWorkSession
            {
                AgentType = AgentType.Mock,
                WorkType = WorkType.Refactor,
                StartedAt = context.ScanStartedAt,
                EndedAt = context.ScanEndedAt,
                TokenUsageBucket = TokenUsageBucket.Medium,
                ResultStatus = ResultStatus.Succeeded,
                SourceProvider = ProviderId,
                SourceProviders = { ProviderId },
                ParserVersion = ParserVersion,
                Confidence = ProviderConfidence.High,
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 4,
                    AddedLineBucket = LineChangeBucket.Small,
                    DeletedLineBucket = LineChangeBucket.Small,
                    ProjectPathHash = context.ProjectPathHash
                },
                ActionSummary = new AgentActionSummary
                {
                    PromptCount = 2,
                    ToolCallCount = 8,
                    FileEditCount = 4,
                    CommandRunCount = 2,
                    TestRunCount = 1,
                    DurationBucket = DurationBucket.FifteenTo60Minutes
                }
            };
            session.DeduplicationKey = $"{context.ProjectPathHash}:{session.StartedAt:yyyyMMddHH}:mock";
            return Task.FromResult(AgentProviderResult.Success(ProviderId, session, ProviderConfidence.High));
        }
    }
}
