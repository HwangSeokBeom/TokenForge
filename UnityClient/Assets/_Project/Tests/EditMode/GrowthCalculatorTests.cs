using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Growth;

namespace TokenForge.Client.Tests
{
    public sealed class GrowthCalculatorTests
    {
        [Test]
        public void Calculate_DifferentWorkTypes_RewardExpectedPrimaryStats()
        {
            var calculator = new GrowthCalculator(new GrowthCalculationConfig { EnableDailyCap = false });
            var profile = new CharacterProfile { Level = 1, TotalExp = 0 };

            var feature = calculator.Calculate(Session(WorkType.Feature), profile);
            var bugfix = calculator.Calculate(Session(WorkType.Bugfix), profile);
            var uiux = calculator.Calculate(Session(WorkType.UIUX), profile);
            var refactor = calculator.Calculate(Session(WorkType.Refactor), profile);

            Assert.Greater(feature.StatDeltas.Logic, 0);
            Assert.Greater(feature.StatDeltas.Creativity, 0);
            Assert.Greater(feature.StatDeltas.Velocity, 0);
            Assert.Greater(bugfix.StatDeltas.Debug, 0);
            Assert.Greater(bugfix.StatDeltas.Stability, 0);
            Assert.Greater(uiux.StatDeltas.Design, 0);
            Assert.Greater(refactor.StatDeltas.Architecture, 0);
            Assert.Greater(refactor.StatDeltas.Efficiency, 0);
        }

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

        [Test]
        public void Calculate_MiniGameBonus_IsCapped()
        {
            var calculator = new GrowthCalculator(new GrowthCalculationConfig { EnableDailyCap = false });
            var profile = new CharacterProfile { Level = 1, TotalExp = 0 };
            var session = Session(WorkType.Feature);

            var capped = calculator.Calculate(session, profile, 0, 10f);
            var expectedCap = calculator.Calculate(session, profile, 0, 0.15f);

            Assert.AreEqual(expectedCap.ExpGained, capped.ExpGained);
        }

        [Test]
        public void Calculate_TinySession_HasLowReward()
        {
            var calculator = new GrowthCalculator(new GrowthCalculationConfig { EnableDailyCap = false });
            var profile = new CharacterProfile { Level = 1, TotalExp = 0 };
            var tiny = new AgentWorkSession
            {
                WorkType = WorkType.Feature,
                TokenUsageBucket = TokenUsageBucket.Small,
                ResultStatus = ResultStatus.Succeeded,
                ActionSummary = new AgentActionSummary(),
                GitChangeSummary = new GitChangeSummary { ChangedFileCount = 0 }
            };

            var normal = calculator.Calculate(Session(WorkType.Feature), profile);
            var tinyResult = calculator.Calculate(tiny, profile);

            Assert.Less(tinyResult.ExpGained, normal.ExpGained / 2);
            Assert.IsNotEmpty(tinyResult.Warnings);
        }

        private static AgentWorkSession Session(WorkType workType)
        {
            return new AgentWorkSession
            {
                WorkType = workType,
                TokenUsageBucket = TokenUsageBucket.Medium,
                ResultStatus = ResultStatus.Succeeded,
                ActionSummary = new AgentActionSummary { FileEditCount = 4, TestRunCount = 1 },
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 4,
                    AddedLineBucket = LineChangeBucket.Medium,
                    DeletedLineBucket = LineChangeBucket.Small
                }
            };
        }
    }
}
