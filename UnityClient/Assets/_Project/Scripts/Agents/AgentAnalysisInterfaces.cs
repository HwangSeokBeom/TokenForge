using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Agents
{
    public interface IAgentAnalysisLogger
    {
        void Info(string message);
        void Warning(string message);
    }

    public interface IAgentLogSourceReader
    {
        Task<AgentLogReadResult> ReadAsync(AgentAnalysisInput input, CancellationToken cancellationToken);
    }

    public interface IAgentLogParser
    {
        AgentProviderType ProviderType { get; }
        string ParserVersion { get; }
        Result<AgentActivitySummary> Parse(AgentAnalysisInput input, IReadOnlyList<AgentLogEntry> entries);
    }

    public interface IAgentActivityAnalyzer
    {
        Task<Result<AgentActivitySummary>> AnalyzeAsync(AgentAnalysisInput input, CancellationToken cancellationToken);
    }
}
