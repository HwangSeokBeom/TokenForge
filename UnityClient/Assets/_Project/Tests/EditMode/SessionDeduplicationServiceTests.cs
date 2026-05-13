using System;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Tests
{
    public sealed class SessionDeduplicationServiceTests
    {
        [Test]
        public void Evaluate_HighConfidenceOverlap_MergesAutomatically()
        {
            var service = new SessionDeduplicationService();
            var now = DateTimeOffset.UtcNow;
            var first = Session("a", now, now.AddMinutes(20), "hash", 4, ProviderConfidence.High);
            var second = Session("b", now.AddMinutes(5), now.AddMinutes(25), "hash", 5, ProviderConfidence.High);

            var candidate = service.Evaluate(first, second);

            Assert.AreEqual(DeduplicationDecision.MergeAutomatically, candidate.Decision);
            Assert.NotNull(candidate.MergedSession);
            Assert.AreEqual(2, candidate.MergedSession.SourceSessionIds.Count);
        }

        private static AgentWorkSession Session(string id, DateTimeOffset start, DateTimeOffset end, string projectHash, int changedFiles, ProviderConfidence confidence)
        {
            return new AgentWorkSession
            {
                SessionId = id,
                WorkType = WorkType.Refactor,
                StartedAt = start,
                EndedAt = end,
                Confidence = confidence,
                SourceProvider = id,
                SourceProviders = { id },
                GitChangeSummary = new GitChangeSummary
                {
                    ProjectPathHash = projectHash,
                    ChangedFileCount = changedFiles
                },
                ActionSummary = new AgentActionSummary
                {
                    FileEditCount = changedFiles
                }
            };
        }
    }
}
