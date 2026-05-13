using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Growth;

namespace TokenForge.Client.Tests
{
    public sealed class GrowthCalculatorTests
    {
        [Test]
        public void Calculate_BugfixSession_AddsDebugAndStress()
        {
            var calculator = new GrowthCalculator(new GrowthCalculationConfig { EnableDailyCap = false });
            var session = new AgentWorkSession
            {
                WorkType = WorkType.Bugfix,
                TokenUsageBucket = TokenUsageBucket.Medium,
                ResultStatus = ResultStatus.Failed,
                ActionSummary = new AgentActionSummary { FailedCommandCount = 1 },
                GitChangeSummary = new GitChangeSummary { ChangedFileCount = 3 }
            };
            var profile = new CharacterProfile { Level = 1, TotalExp = 0 };

            var result = calculator.Calculate(session, profile);

            Assert.Greater(result.ExpGained, 0);
            Assert.Greater(result.StatDeltas.Debug, 0);
            Assert.Greater(result.StressDelta, 0);
            Assert.AreEqual(EvolutionType.Debugger, result.EvolutionProgressDelta);
        }
    }
}
