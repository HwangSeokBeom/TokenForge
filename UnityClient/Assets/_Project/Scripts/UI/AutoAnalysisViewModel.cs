using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Agents;

namespace TokenForge.Client.UI
{
    public sealed class AutoAnalysisViewModel
    {
        private readonly List<IAgentLogProvider> providers = new List<IAgentLogProvider>();

        public bool IsScanning { get; private set; }
        public List<AgentProviderResult> LatestResults { get; } = new List<AgentProviderResult>();

        public AutoAnalysisViewModel(IEnumerable<IAgentLogProvider> providers)
        {
            if (providers != null)
            {
                this.providers.AddRange(providers);
            }
        }

        public async Task ScanAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            IsScanning = true;
            LatestResults.Clear();
            try
            {
                foreach (var provider in providers)
                {
                    if (!await provider.IsAvailableAsync(context, cancellationToken))
                    {
                        LatestResults.Add(AgentProviderResult.Failure(provider.ProviderId, "provider_unavailable", "Provider is not available for this context."));
                        continue;
                    }

                    LatestResults.Add(await provider.AnalyzeAsync(context, cancellationToken));
                }
            }
            finally
            {
                IsScanning = false;
            }
        }
    }
}
