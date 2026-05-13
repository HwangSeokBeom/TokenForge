using System.Collections.Generic;
using System.Linq;
using TokenForge.Client.Agents;
using TokenForge.Client.Domain;

namespace TokenForge.Client.UI
{
    public sealed class SessionReviewViewModel
    {
        private readonly SessionDeduplicationService deduplicationService;

        public List<AgentWorkSession> CandidateSessions { get; } = new List<AgentWorkSession>();
        public ManualFallbackInput ManualFallbackInput { get; } = new ManualFallbackInput();
        public List<string> ReviewWarnings { get; } = new List<string>();

        public SessionReviewViewModel(SessionDeduplicationService deduplicationService = null)
        {
            this.deduplicationService = deduplicationService ?? new SessionDeduplicationService();
        }

        public void LoadProviderResults(IEnumerable<AgentProviderResult> results)
        {
            CandidateSessions.Clear();
            ReviewWarnings.Clear();
            foreach (var result in results ?? Enumerable.Empty<AgentProviderResult>())
            {
                if (result.Session != null)
                {
                    CandidateSessions.Add(result.Session);
                }

                ReviewWarnings.AddRange(result.Warnings.Select(warning => warning.Message));
                if (result.Error != null && result.Error.IsRecoverable)
                {
                    ReviewWarnings.Add(result.Error.Message);
                }
            }
        }

        public DeduplicationCandidate EvaluateMerge(AgentWorkSession first, AgentWorkSession second, bool userConfirmed)
        {
            return deduplicationService.Evaluate(first, second, userConfirmed);
        }
    }
}
