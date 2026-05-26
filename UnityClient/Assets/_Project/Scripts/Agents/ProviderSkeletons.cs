using System.Threading;
using System.Threading.Tasks;

namespace TokenForge.Client.Agents
{
    public abstract class UnsupportedAgentLogProvider : IAgentLogProvider
    {
        public abstract string ProviderId { get; }
        public abstract string ParserVersion { get; }

        public Task<bool> IsAvailableAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<AgentProviderResult> AnalyzeAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(AgentProviderResult.Failure(
                ProviderId,
                "provider_not_implemented",
                "This provider is not enabled for automatic analysis in this build. Use Detect or Choose Folder from the provider catalog."));
        }
    }

    public sealed class ClaudeCodeLogProvider : UnsupportedAgentLogProvider
    {
        public override string ProviderId => "ClaudeCodeLogProvider";
        public override string ParserVersion => ClaudeAgentLogParser.Version;
    }

    public sealed class CodexLogProvider : UnsupportedAgentLogProvider
    {
        public override string ProviderId => "CodexLogProvider";
        public override string ParserVersion => CodexAgentLogParser.Version;
    }

    public sealed class CursorLogProvider : UnsupportedAgentLogProvider
    {
        public override string ProviderId => "CursorLogProvider";
        public override string ParserVersion => CursorAgentLogParser.Version;
    }

    public sealed class WindsurfLogProvider : UnsupportedAgentLogProvider
    {
        public override string ProviderId => "WindsurfLogProvider";
        public override string ParserVersion => UnknownAgentLogParser.Version;
    }
}
