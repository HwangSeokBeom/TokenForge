using System.Threading;
using System.Threading.Tasks;

namespace TokenForge.Client.Agents
{
    public interface IAgentLogProvider
    {
        string ProviderId { get; }
        string ParserVersion { get; }
        Task<bool> IsAvailableAsync(AgentProviderContext context, CancellationToken cancellationToken);
        Task<AgentProviderResult> AnalyzeAsync(AgentProviderContext context, CancellationToken cancellationToken);
    }
}
