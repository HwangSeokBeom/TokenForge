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
                "Provider skeleton only. Log path, schema, privacy review, and parser version policy require investigation."));
        }
    }

    public sealed class ClaudeCodeLogProvider : UnsupportedAgentLogProvider
    {
        public override string ProviderId => "ClaudeCodeLogProvider";
        public override string ParserVersion => "todo-claude-code-schema";
        // TODO: Investigate Claude Code log locations, schema stability, and fields that may contain prompts/code/output.
    }

    public sealed class CodexLogProvider : UnsupportedAgentLogProvider
    {
        public override string ProviderId => "CodexLogProvider";
        public override string ParserVersion => "todo-codex-schema";
        // TODO: Investigate Codex local session storage, schema versions, and privacy-safe aggregation limits.
    }

    public sealed class CursorLogProvider : UnsupportedAgentLogProvider
    {
        public override string ProviderId => "CursorLogProvider";
        public override string ParserVersion => "todo-cursor-schema";
        // TODO: Investigate Cursor Composer/Agent/Chat persistence and whether user-authorized logs are accessible.
    }

    public sealed class WindsurfLogProvider : UnsupportedAgentLogProvider
    {
        public override string ProviderId => "WindsurfLogProvider";
        public override string ParserVersion => "todo-windsurf-schema";
        // TODO: Investigate Windsurf Cascade/Agent log location and format before implementing a parser.
    }
}
